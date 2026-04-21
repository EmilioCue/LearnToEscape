package com.learntoescape.client;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.MediaType;
import org.springframework.retry.annotation.Backoff;
import org.springframework.retry.annotation.Recover;
import org.springframework.retry.annotation.Retryable;
import org.springframework.stereotype.Service;
import org.springframework.web.client.ResourceAccessException;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;

import com.learntoescape.dto.ollama.OllamaRequest;
import com.learntoescape.dto.ollama.OllamaResponse;

/**
 * Cliente HTTP de alto nivel para el runtime local de Ollama.
 *
 * <p>Encapsula la comunicación con el endpoint {@code /api/generate} utilizando
 * el moderno {@link RestClient} (Spring Framework 6.1 / Spring Boot 3.2+) en
 * lugar de {@code RestTemplate} o {@code WebClient}, y aplica una política de
 * resiliencia basada en {@code spring-retry}:
 * <ul>
 *   <li>Hasta <b>3 intentos</b> ante fallos transitorios de red / HTTP.</li>
 *   <li><b>Backoff exponencial</b>: 2000 ms inicial, multiplicador 2.
 *       Secuencia aproximada de esperas: 2 s → 4 s.</li>
 *   <li>Si todos los reintentos fallan, los métodos {@link #recover} mapean
 *       la causa a {@link LlmConnectionException}, garantizando que la capa
 *       superior <em>nunca</em> reciba {@code null}.</li>
 * </ul>
 *
 * <p>El método es idempotente desde la perspectiva del cliente (generar la
 * misma sala varias veces no produce efectos secundarios en Ollama), por lo
 * que reintentar es seguro.</p>
 */
@Service
public class OllamaClient {

	private static final Logger log = LoggerFactory.getLogger(OllamaClient.class);

	private final RestClient restClient;

	/**
	 * @param ollamaApiUrl URL absoluta del endpoint {@code /api/generate}
	 *                     leída desde la propiedad {@code ollama.api.url}.
	 */
	public OllamaClient(@Value("${ollama.api.url}") String ollamaApiUrl) {
		this.restClient = RestClient.builder()
				.baseUrl(ollamaApiUrl)
				.defaultHeader(org.springframework.http.HttpHeaders.CONTENT_TYPE, MediaType.APPLICATION_JSON_VALUE)
				.defaultHeader(org.springframework.http.HttpHeaders.ACCEPT, MediaType.APPLICATION_JSON_VALUE)
				.build();
		log.info("OllamaClient inicializado con endpoint={}", ollamaApiUrl);
	}

	/**
	 * Envía {@code request} al endpoint {@code /api/generate} de Ollama y
	 * devuelve el texto generado (campo {@code response} de la respuesta),
	 * que en nuestro pipeline es un JSON serializado pendiente de parsear.
	 *
	 * <p>La invocación está bajo {@link Retryable}: los fallos transitorios
	 * (timeouts, conexión rehusada, 5xx) disparan un reintento con backoff
	 * exponencial. La lógica de recuperación está en {@link #recover}.</p>
	 *
	 * @param request payload ya construido por el orquestador de prompts.
	 * @return texto generado por el LLM, nunca {@code null}.
	 * @throws LlmConnectionException si se agotan los reintentos (vía {@code @Recover}).
	 */
	@Retryable(
			retryFor = { ResourceAccessException.class, RestClientException.class },
			maxAttempts = 3,
			backoff = @Backoff(delay = 2000L, multiplier = 2.0)
	)
	public String generate(OllamaRequest request) {
		log.info("Iniciando petición a Ollama (model={}, stream={}, format={})",
				request.model(), request.stream(), request.format());

		try {
			OllamaResponse response = restClient.post()
					.contentType(MediaType.APPLICATION_JSON)
					.body(request)
					.retrieve()
					.body(OllamaResponse.class);

			if (response == null || response.response() == null || response.response().isBlank()) {
				// Un cuerpo vacío es, a efectos prácticos, un fallo de protocolo;
				// lo convertimos en RestClientException para que la política
				// de retry lo considere recuperable.
				throw new RestClientException("Respuesta vacía o incompleta desde Ollama");
			}

			log.info("Petición completada (done={}, chars={})",
					response.done(), response.response().length());
			return response.response();

		} catch (ResourceAccessException ex) {
			log.warn("Fallo de conexión con Ollama, reintentando... ({})", ex.getMessage());
			throw ex;
		} catch (RestClientException ex) {
			log.warn("Fallo HTTP al invocar Ollama, reintentando... ({})", ex.getMessage());
			throw ex;
		}
	}

	/**
	 * Recover específico para fallos de red / timeout agotados los reintentos.
	 */
	@Recover
	public String recover(ResourceAccessException ex, OllamaRequest request) {
		log.error("Agotados los reintentos a Ollama por fallo de red (model={}): {}",
				request.model(), ex.getMessage());
		throw new LlmConnectionException(
				"No se pudo contactar con el servicio Ollama tras varios intentos", ex);
	}

	/**
	 * Recover genérico para cualquier otro error del cliente REST
	 * (5xx, 4xx propagado, respuesta vacía, etc.).
	 */
	@Recover
	public String recover(RestClientException ex, OllamaRequest request) {
		log.error("Agotados los reintentos a Ollama por error HTTP (model={}): {}",
				request.model(), ex.getMessage());
		throw new LlmConnectionException(
				"Fallo en la comunicación con el servicio Ollama tras varios intentos", ex);
	}
}
