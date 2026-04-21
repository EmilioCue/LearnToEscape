package com.learntoescape.dto.ollama;

import com.fasterxml.jackson.annotation.JsonInclude;

/**
 * Cuerpo de la petición al endpoint {@code /api/generate} de Ollama.
 *
 * <p>Se fuerza {@code stream = false} y {@code format = "json"} vía la factoría
 * {@link #of(String, String, String)} para garantizar que la respuesta llegue
 * completa y en JSON válido, que es lo que espera nuestro parser.</p>
 *
 * @param model  Nombre del modelo Ollama (p.ej. {@code "llama3.1"}, {@code "qwen2.5"}).
 * @param prompt Prompt de usuario con la tarea concreta.
 * @param system Prompt de sistema (rol/estilo/restricciones).
 * @param stream Si es {@code true} Ollama hace streaming por chunks; nosotros usamos {@code false}.
 * @param format Formato de salida exigido; {@code "json"} activa el modo JSON estricto.
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record OllamaRequest(
		String model,
		String prompt,
		String system,
		boolean stream,
		String format
) {

	/**
	 * Factoría conveniente con los defaults del proyecto:
	 * {@code stream = false} y {@code format = "json"}.
	 */
	public static OllamaRequest of(String model, String prompt, String system) {
		return new OllamaRequest(model, prompt, system, false, "json");
	}
}
