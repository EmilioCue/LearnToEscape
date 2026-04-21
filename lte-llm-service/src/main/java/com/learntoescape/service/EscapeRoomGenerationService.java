package com.learntoescape.service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.retry.annotation.Backoff;
import org.springframework.retry.annotation.Recover;
import org.springframework.retry.annotation.Retryable;
import org.springframework.stereotype.Service;

import tools.jackson.core.JacksonException;
import tools.jackson.databind.ObjectMapper;

import com.learntoescape.client.OllamaClient;
import com.learntoescape.dto.RoomDTO;
import com.learntoescape.dto.RoomGenerationRequest;
import com.learntoescape.dto.ollama.OllamaRequest;
import com.learntoescape.util.PromptFactory;

/**
 * Orquestador del pipeline de generación de salas educativas.
 *
 * <p>Responsabilidades:
 * <ol>
 *   <li>Componer el {@link OllamaRequest} usando la {@link PromptFactory} y el
 *       modelo configurado por propiedades.</li>
 *   <li>Invocar al {@link OllamaClient} (que ya aporta resiliencia de red).</li>
 *   <li>Sanear la respuesta del LLM (quita <em>code fences</em> markdown y
 *       recorta cualquier texto previo/posterior al objeto JSON) antes de
 *       deserializar.</li>
 *   <li>Deserializar estrictamente a {@link RoomDTO} con Jackson y aplicar
 *       reintentos si el modelo <em>alucina</em> un JSON malformado.</li>
 * </ol>
 *
 * <p>Separación de preocupaciones respecto al cliente:
 * <ul>
 *   <li>{@code OllamaClient#generate} reintenta <b>fallos de infraestructura</b>
 *       (red/HTTP) — ver {@code LlmConnectionException}.</li>
 *   <li>Este servicio reintenta <b>fallos de contenido</b> (JSON inválido) —
 *       ver {@link LlmGenerationException}.</li>
 * </ul>
 * De esta forma cada capa reintenta lo que <em>sabe</em> reintentar y los fallos
 * se mapean a excepciones de dominio distintas, lo que permite métricas y
 * códigos HTTP diferenciados en la capa web.</p>
 */
@Service
public class EscapeRoomGenerationService {

	private static final Logger log = LoggerFactory.getLogger(EscapeRoomGenerationService.class);

	private final OllamaClient ollamaClient;
	private final ObjectMapper objectMapper;
	private final String model;

	public EscapeRoomGenerationService(
			OllamaClient ollamaClient,
			ObjectMapper objectMapper,
			@Value("${ollama.model:qwen2.5:1.5b}") String model
	) {
		this.ollamaClient = ollamaClient;
		this.objectMapper = objectMapper;
		this.model = model;
		log.info("EscapeRoomGenerationService inicializado (model={})", model);
	}

	/**
	 * Pipeline completo de generación:
	 * <pre>
	 *   RoomGenerationRequest --&gt; OllamaRequest --&gt; LLM --&gt; String JSON --&gt; RoomDTO
	 * </pre>
	 *
	 * <p>Está anotado con {@link Retryable} exclusivamente para
	 * {@link JacksonException}: si el modelo devuelve texto pero <em>no</em>
	 * un JSON válido, reintentamos hasta 3 veces con backoff corto (el prompt es
	 * estable, así que basta con volver a invocarlo). Cualquier excepción
	 * propagada desde {@code OllamaClient} (p.ej. {@code LlmConnectionException})
	 * se propaga intacta y NO dispara retry aquí, porque esa resiliencia ya se
	 * aplicó en la capa inferior.</p>
	 *
	 * <p><b>Nota sobre Jackson 3.x</b>: en esta versión (la que usa Spring Boot 4)
	 * la histórica {@code JsonProcessingException} se ha unificado en
	 * {@link JacksonException}, que cubre tanto errores de parsing como de
	 * databind. Es la clase base que el usuario espera cuando habla de
	 * "{@code JsonProcessingException} de Jackson".</p>
	 *
	 * @param request parámetros del cliente Unity.
	 * @return sala generada y estructuralmente válida.
	 * @throws LlmGenerationException si se agotan los reintentos (vía {@link #recover}).
	 */
	@Retryable(
			retryFor = JacksonException.class,
			maxAttempts = 3,
			backoff = @Backoff(delay = 500L, multiplier = 2.0)
	)
	public RoomDTO generateValidatedRoom(RoomGenerationRequest request) {
		OllamaRequest ollamaRequest = OllamaRequest.of(
				model,
				PromptFactory.buildUserPrompt(
						request.topic(),
						request.puzzleCount(),
						request.difficulty(),
						request.context()
				),
				PromptFactory.buildSystemPrompt()
		);

		String rawResponse = ollamaClient.generate(ollamaRequest);
		String sanitizedJson = sanitizeJson(rawResponse);

		try {
			RoomDTO room = objectMapper.readValue(sanitizedJson, RoomDTO.class);
			log.info("Sala generada correctamente (theme='{}', pin='{}')",
					room.theme(),
					room.puzzle4_console() != null ? room.puzzle4_console().pin() : "?");
			return room;
		} catch (JacksonException ex) {
			log.warn("JSON malformado devuelto por la IA. Reintentando... (cause='{}', preview='{}')",
					ex.getMessage(), preview(sanitizedJson));
			throw ex;
		}
	}

	/**
	 * Último recurso cuando los 3 intentos de parseo fallan. Centraliza el
	 * logging de error y traduce el fallo técnico de Jackson a una excepción
	 * de dominio que la capa web puede mapear a un HTTP apropiado.
	 */
	@Recover
	public RoomDTO recover(JacksonException ex, RoomGenerationRequest request) {
		log.error("Fallo al generar un JSON válido tras 3 intentos (topic='{}', puzzles={}, difficulty='{}'): {}",
				request.topic(), request.puzzleCount(), request.difficulty(), ex.getMessage());
		throw new LlmGenerationException(
				"Fallo al generar un JSON válido tras 3 intentos", ex);
	}

	/**
	 * Limpieza defensiva de la respuesta del LLM:
	 * <ul>
	 *   <li>Elimina envoltorios de <i>code fences</i> tipo {@code ```json … ```}.</li>
	 *   <li>Recorta cualquier texto antes del primer {@code '{'} y después del
	 *       último {@code '}'} para tolerar prosa accidental del modelo.</li>
	 * </ul>
	 * Si tras limpiar no queda un fragmento con aspecto de objeto JSON, se
	 * devuelve el input original: {@code ObjectMapper} lanzará
	 * {@link JsonProcessingException} y el retry hará el resto.
	 */
	private static String sanitizeJson(String raw) {
		if (raw == null) {
			return "";
		}
		String trimmed = raw.strip();

		if (trimmed.startsWith("```")) {
			int firstNewline = trimmed.indexOf('\n');
			if (firstNewline != -1) {
				trimmed = trimmed.substring(firstNewline + 1);
			}
			int closingFence = trimmed.lastIndexOf("```");
			if (closingFence != -1) {
				trimmed = trimmed.substring(0, closingFence);
			}
			trimmed = trimmed.strip();
		}

		int firstBrace = trimmed.indexOf('{');
		int lastBrace = trimmed.lastIndexOf('}');
		if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace) {
			return trimmed.substring(firstBrace, lastBrace + 1);
		}
		return trimmed;
	}

	/** Recorta un fragmento para logs, evitando volcar respuestas gigantes. */
	private static String preview(String text) {
		if (text == null) {
			return "";
		}
		int max = 200;
		return text.length() <= max ? text : text.substring(0, max) + "…";
	}
}
