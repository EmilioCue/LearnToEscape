package com.learntoescape.client;

/**
 * Excepción de dominio que señala que la comunicación con el LLM (Ollama)
 * ha fallado de forma irrecuperable tras agotar la política de reintentos.
 *
 * <p>Se lanza desde los métodos {@code @Recover} del cliente para que las
 * capas superiores (controllers / servicios de dominio) puedan mapearla a
 * una respuesta HTTP apropiada (p.ej. {@code 502 Bad Gateway} o
 * {@code 503 Service Unavailable}) sin confundirla con errores de negocio.</p>
 *
 * <p>Es {@link RuntimeException} a propósito: no queremos obligar a declararla
 * en firmas que se propagan a través de proxies AOP de Spring Retry.</p>
 */
public class LlmConnectionException extends RuntimeException {

	public LlmConnectionException(String message) {
		super(message);
	}

	public LlmConnectionException(String message, Throwable cause) {
		super(message, cause);
	}
}
