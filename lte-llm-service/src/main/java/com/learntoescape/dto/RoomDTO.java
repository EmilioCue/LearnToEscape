package com.learntoescape.dto;

import java.util.List;

import com.fasterxml.jackson.annotation.JsonInclude;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

/**
 * Contrato estricto de una sala educativa compuesta por EXACTAMENTE 4 puzzles
 * de tipos predefinidos. Cada tipo de puzzle tiene una mecánica específica y
 * un esquema cerrado; el LLM no tiene libertad para inventar nuevos tipos.
 *
 * <p>El esquema se modela con <i>records</i> anidados para que Jackson
 * deserialice de forma inmutable y Bean Validation (propagado con
 * {@link Valid}) audite toda la estructura en una sola pasada.</p>
 *
 * <p>Los nombres de campo con <i>snake_case</i> (p.ej. {@code puzzle1_matrix})
 * replican exactamente las claves del JSON producido por el modelo, evitando
 * mapeos {@code @JsonProperty} y manteniendo el DTO autoexplicativo.</p>
 *
 * @param theme           Temática educativa de la sala.
 * @param puzzle1_matrix  Puzzle de clasificación en 2 categorías.
 * @param puzzle2_router  Puzzle de secuencia ordenada de 5 pasos.
 * @param puzzle3_link    Puzzle de emparejar conceptos con definiciones.
 * @param puzzle4_console Puzzle final de deducción con PIN de 4 dígitos.
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record RoomDTO(

		@NotBlank(message = "room.theme is required")
		String theme,

		@NotNull(message = "room.puzzle1_matrix is required")
		@Valid
		Puzzle1Matrix puzzle1_matrix,

		@NotNull(message = "room.puzzle2_router is required")
		@Valid
		Puzzle2Router puzzle2_router,

		@NotNull(message = "room.puzzle3_link is required")
		@Valid
		Puzzle3Link puzzle3_link,

		@NotNull(message = "room.puzzle4_console is required")
		@Valid
		Puzzle4Console puzzle4_console
) {

	/**
	 * Puzzle 1: clasificación de ítems en exactamente 2 categorías.
	 *
	 * @param categories lista de <b>exactamente 2</b> etiquetas de categoría.
	 * @param items      ítems a clasificar; cada uno referencia una categoría por índice.
	 */
	public record Puzzle1Matrix(

			@NotNull(message = "puzzle1_matrix.categories is required")
			@Size(min = 2, max = 2, message = "puzzle1_matrix.categories must contain exactly 2 elements")
			List<@NotBlank(message = "puzzle1_matrix.categories entries must not be blank") String> categories,

			@NotNull(message = "puzzle1_matrix.items is required")
			@NotEmpty(message = "puzzle1_matrix.items must contain at least one item")
			@Valid
			List<MatrixItem> items
	) {

		/**
		 * Ítem individual del puzzle de matriz.
		 *
		 * @param name          texto visible para el jugador.
		 * @param categoryIndex índice (0 o 1) de la categoría a la que pertenece.
		 */
		public record MatrixItem(

				@NotBlank(message = "puzzle1_matrix.items[].name is required")
				String name,

				@NotNull(message = "puzzle1_matrix.items[].categoryIndex is required")
				@Min(value = 0, message = "puzzle1_matrix.items[].categoryIndex must be 0 or 1")
				@Max(value = 1, message = "puzzle1_matrix.items[].categoryIndex must be 0 or 1")
				Integer categoryIndex
		) {
		}
	}

	/**
	 * Puzzle 2: secuencia ordenada de exactamente 5 pasos.
	 *
	 * @param sequence lista de 5 pasos en el orden pedagógicamente correcto.
	 */
	public record Puzzle2Router(

			@NotNull(message = "puzzle2_router.sequence is required")
			@Size(min = 5, max = 5, message = "puzzle2_router.sequence must contain exactly 5 steps")
			List<@NotBlank(message = "puzzle2_router.sequence entries must not be blank") String> sequence
	) {
	}

	/**
	 * Puzzle 3: emparejar conceptos con sus definiciones.
	 *
	 * @param pairs lista no vacía de parejas concepto-definición.
	 */
	public record Puzzle3Link(

			@NotNull(message = "puzzle3_link.pairs is required")
			@NotEmpty(message = "puzzle3_link.pairs must contain at least one pair")
			@Valid
			List<Pair> pairs
	) {

		/**
		 * Pareja concepto-definición.
		 *
		 * @param concept    término, nombre o idea a identificar.
		 * @param definition explicación canónica del concepto.
		 */
		public record Pair(

				@NotBlank(message = "puzzle3_link.pairs[].concept is required")
				String concept,

				@NotBlank(message = "puzzle3_link.pairs[].definition is required")
				String definition
		) {
		}
	}

	/**
	 * Puzzle 4: consola final con PIN de 4 dígitos y pregunta de deducción.
	 *
	 * @param pin               código numérico de EXACTAMENTE 4 dígitos.
	 * @param deductionQuestion pregunta que guía al jugador a deducir el PIN.
	 */
	public record Puzzle4Console(

			@NotBlank(message = "puzzle4_console.pin is required")
			@Pattern(regexp = "\\d{4}", message = "puzzle4_console.pin must be exactly 4 digits")
			String pin,

			@NotBlank(message = "puzzle4_console.deductionQuestion is required")
			String deductionQuestion
	) {
	}
}
