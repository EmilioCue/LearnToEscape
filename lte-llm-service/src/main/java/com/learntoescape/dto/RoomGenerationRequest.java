package com.learntoescape.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;

/**
 * Payload que envía el cliente Unity al endpoint de generación de salas.
 *
 * <p>Campos en {@code camelCase} para ser compatibles con la serialización
 * por defecto de Newtonsoft.Json en el cliente Unity.</p>
 *
 * @param topic       Temática educativa sobre la que se generarán los puzzles.
 * @param puzzleCount Número de puzzles deseados (entre 1 y 5, inclusive).
 * @param difficulty  Dificultad pedagógica solicitada (p.ej. {@code "easy"}, {@code "medium"}, {@code "hard"}).
 * @param context     Contexto o instrucciones adicionales opcionales para afinar la generación.
 */
public record RoomGenerationRequest(

		@NotBlank(message = "topic is required")
		String topic,

		@Min(value = 1, message = "puzzleCount must be at least 1")
		@Max(value = 5, message = "puzzleCount must be at most 5")
		int puzzleCount,

		@NotBlank(message = "difficulty is required")
		String difficulty,

		String context
) {
}
