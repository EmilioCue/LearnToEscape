package com.learntoescape.dto.ollama;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;

/**
 * Respuesta del endpoint {@code /api/generate} de Ollama en modo no-streaming.
 *
 * <p>Ollama devuelve muchos más campos (timings, contexto, counts…) que no
 * necesitamos; {@link JsonIgnoreProperties} hace que el deserializador los
 * ignore sin romperse.</p>
 *
 * @param response Texto generado por el modelo (en nuestro caso, JSON serializado).
 * @param done     {@code true} cuando Ollama ha terminado de generar la respuesta.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
public record OllamaResponse(
		String response,
		boolean done
) {
}
