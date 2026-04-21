package com.learntoescape.exception;

import java.net.URI;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

import com.learntoescape.client.LlmConnectionException;
import com.learntoescape.service.LlmGenerationException;

/**
 * Traducción centralizada de excepciones a respuestas HTTP siguiendo el
 * estándar <a href="https://www.rfc-editor.org/rfc/rfc7807">RFC 7807</a>
 * mediante {@link ProblemDetail}.
 *
 * <p>Responsabilidades:
 * <ul>
 *   <li>Normalizar la forma del error para que el cliente Unity pueda
 *       deserializarlo siempre contra el mismo contrato.</li>
 *   <li>Diferenciar códigos HTTP por naturaleza del fallo (cliente vs.
 *       infra vs. modelo vs. bug inesperado) sin filtrar detalles
 *       sensibles (stack traces, mensajes internos).</li>
 *   <li>Centralizar el logging: cada handler decide si un error es
 *       "esperado" (validación, saturación de IA) y va a {@code warn},
 *       o "inesperado" y va a {@code error}.</li>
 * </ul>
 *
 * <p>Los tipos {@code type} son URIs opacas dentro del espacio
 * {@code urn:lte:*}; están estandarizados aquí para poder documentarlos
 * en el futuro (OpenAPI) sin romper a los consumidores.</p>
 */
@RestControllerAdvice
public class GlobalExceptionHandler {

	private static final Logger log = LoggerFactory.getLogger(GlobalExceptionHandler.class);

	private static final URI TYPE_VALIDATION  = URI.create("urn:lte:error:validation");
	private static final URI TYPE_LLM_CONN    = URI.create("urn:lte:error:llm-connection");
	private static final URI TYPE_LLM_GEN     = URI.create("urn:lte:error:llm-generation");
	private static final URI TYPE_INTERNAL    = URI.create("urn:lte:error:internal");

	/**
	 * Errores de validación de Bean Validation sobre el {@code @RequestBody}.
	 *
	 * <p>Devuelve 400 y enumera en {@code properties.errors} cada campo
	 * junto a su mensaje, para que Unity pueda marcar el input concreto
	 * que falló (p. ej. "puzzleCount must be at most 5").</p>
	 */
	@ExceptionHandler(MethodArgumentNotValidException.class)
	public ProblemDetail handleValidation(MethodArgumentNotValidException ex) {
		List<Map<String, String>> fieldErrors = ex.getBindingResult().getFieldErrors().stream()
				.map(GlobalExceptionHandler::toFieldErrorMap)
				.toList();

		log.warn("Petición rechazada por validación: {}", fieldErrors);

		ProblemDetail problem = ProblemDetail.forStatusAndDetail(
				HttpStatus.BAD_REQUEST,
				"La petición contiene campos inválidos. Revisa la lista de errores."
		);
		problem.setType(TYPE_VALIDATION);
		problem.setTitle("Validation Failed");
		problem.setProperty("timestamp", Instant.now().toString());
		problem.setProperty("errors", fieldErrors);
		return problem;
	}

	/**
	 * Fallo de infraestructura con Ollama (red/HTTP) tras agotar reintentos
	 * en {@code OllamaClient}. El servicio externo no responde.
	 */
	@ExceptionHandler(LlmConnectionException.class)
	public ProblemDetail handleLlmConnection(LlmConnectionException ex) {
		log.warn("LLM no disponible: {}", ex.getMessage());

		ProblemDetail problem = ProblemDetail.forStatusAndDetail(
				HttpStatus.SERVICE_UNAVAILABLE,
				"No se pudo contactar con el motor de IA local"
		);
		problem.setType(TYPE_LLM_CONN);
		problem.setTitle("LLM Service Unavailable");
		problem.setProperty("timestamp", Instant.now().toString());
		return problem;
	}

	/**
	 * El LLM respondió pero incumplió el contrato JSON tras agotar los
	 * reintentos de parseo en {@code EscapeRoomGenerationService}.
	 */
	@ExceptionHandler(LlmGenerationException.class)
	public ProblemDetail handleLlmGeneration(LlmGenerationException ex) {
		log.warn("LLM devolvió contenido inválido: {}", ex.getMessage());

		ProblemDetail problem = ProblemDetail.forStatusAndDetail(
				HttpStatus.BAD_GATEWAY,
				"La IA generó una respuesta inválida tras múltiples intentos"
		);
		problem.setType(TYPE_LLM_GEN);
		problem.setTitle("Invalid LLM Output");
		problem.setProperty("timestamp", Instant.now().toString());
		return problem;
	}

	/**
	 * Red de seguridad para cualquier excepción no contemplada.
	 *
	 * <p>NUNCA se expone el stacktrace o el mensaje original al cliente: eso
	 * podría revelar rutas internas, versiones de librerías o datos sensibles.
	 * Se loguea con nivel {@code error} incluyendo el stacktrace para
	 * diagnóstico en servidor.</p>
	 */
	@ExceptionHandler(Exception.class)
	public ProblemDetail handleUnexpected(Exception ex) {
		log.error("Error inesperado no contemplado en los handlers específicos", ex);

		ProblemDetail problem = ProblemDetail.forStatusAndDetail(
				HttpStatus.INTERNAL_SERVER_ERROR,
				"Ha ocurrido un error interno inesperado. Inténtalo de nuevo más tarde."
		);
		problem.setType(TYPE_INTERNAL);
		problem.setTitle("Internal Server Error");
		problem.setProperty("timestamp", Instant.now().toString());
		return problem;
	}

	/**
	 * Helper: proyecta un {@link FieldError} a un mapa plano serializable en
	 * JSON para formar parte de {@code properties.errors} del ProblemDetail.
	 * Se usa {@link LinkedHashMap} para mantener un orden estable en la salida.
	 */
	private static Map<String, String> toFieldErrorMap(FieldError fieldError) {
		Map<String, String> entry = new LinkedHashMap<>();
		entry.put("field", fieldError.getField());
		entry.put("message", fieldError.getDefaultMessage());
		Object rejected = fieldError.getRejectedValue();
		entry.put("rejectedValue", rejected == null ? "null" : rejected.toString());
		return entry;
	}
}
