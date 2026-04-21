package com.learntoescape.service;

/**
 * Excepción de dominio que señala que el LLM ha fallado en producir un
 * artefacto estructuralmente válido (JSON parseable como {@code RoomDTO})
 * tras agotar la política de reintentos.
 *
 * <p>Se diferencia de {@code LlmConnectionException} en la capa de cliente:
 * aquí la red funcionó correctamente y el modelo devolvió texto, pero ese
 * texto <em>no</em> cumple el contrato JSON. Ambos errores probablemente
 * mapeen a {@code 502 Bad Gateway} en un {@code @ControllerAdvice}, pero
 * mantenerlas separadas permite métricas y alertas diferenciadas
 * (saturación de infraestructura vs. regresión de calidad del modelo).</p>
 */
public class LlmGenerationException extends RuntimeException {

	public LlmGenerationException(String message) {
		super(message);
	}

	public LlmGenerationException(String message, Throwable cause) {
		super(message, cause);
	}
}
