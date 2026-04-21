package com.learntoescape.util;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Random;

/**
 * Factoría centralizada de prompts para la generación de salas educativas.
 *
 * <p>Mantiene los textos de sistema y de usuario en un único punto para:
 * <ul>
 *   <li>Versionar y auditar el "contrato semántico" con el LLM en un solo lugar.</li>
 *   <li>Forzar un contrato JSON estricto y <b>cerrado</b> (4 puzzles predefinidos)
 *       que minimice alucinaciones estructurales.</li>
 *   <li>Habilitar "Rejugabilidad Determinista": el contenido varía entre
 *       ejecuciones incluso con la misma temática, sin tocar la temperatura
 *       del modelo, inyectando subtemas seleccionados al azar desde una
 *       tabla hardcodeada.</li>
 *   <li>Permitir pruebas unitarias deterministas sobre el ensamblado del
 *       prompt sin tocar el cliente HTTP.</li>
 * </ul>
 *
 * <p>Esta clase es un <i>utility class</i> sin estado observable: todos sus
 * métodos son {@code static} y el constructor privado previene instanciación.</p>
 */
public final class PromptFactory {

	/**
	 * Tabla de subtemas por temática principal. Fuerza variedad de contenido
	 * entre ejecuciones sin necesidad de recalentar el modelo.
	 *
	 * <p>Las claves deben consultarse mediante {@link #findSubtopics(String)},
	 * que normaliza mayúsculas/minúsculas y espacios del topic entrante.</p>
	 */
	private static final Map<String, List<String>> SUBTOPICS_BY_TOPIC = Map.ofEntries(
			Map.entry("ingeniería de software", List.of(
					"Patrones de diseño",
					"Metodologías Ágiles",
					"Testing automatizado",
					"UML y modelado",
					"Integración y despliegue continuos (CI/CD)")),
			Map.entry("programación", List.of(
					"Algoritmos y complejidad",
					"Estructuras de datos",
					"Programación orientada a objetos",
					"Programación funcional",
					"Concurrencia y paralelismo")),
			Map.entry("matemáticas", List.of(
					"Álgebra lineal",
					"Cálculo diferencial",
					"Geometría analítica",
					"Estadística descriptiva",
					"Probabilidad")),
			Map.entry("historia", List.of(
					"Edad Antigua",
					"Edad Media",
					"Renacimiento",
					"Revolución Industrial",
					"Siglo XX")),
			Map.entry("física", List.of(
					"Mecánica clásica",
					"Termodinámica",
					"Electromagnetismo",
					"Óptica",
					"Física moderna"))
	);

	/**
	 * Fuente de aleatoriedad para la selección de subtemas. Se instancia una
	 * sola vez a nivel de clase; {@link Random} es thread-safe para usos
	 * simples como {@link Random#nextInt(int)}.
	 */
	private static final Random RANDOM = new Random();

	/** Número de subtemas a inyectar en el prompt en cada invocación. */
	private static final int SUBTOPICS_PER_PROMPT = 3;

	private PromptFactory() {
	}

	/**
	 * Prompt de sistema con el esquema JSON <b>cerrado</b> que debe producir el LLM.
	 *
	 * <p>La sala consta siempre de 4 puzzles predefinidos: matriz de clasificación,
	 * secuencia "router" de 5 pasos, emparejado concepto-definición y consola
	 * final con PIN de 4 dígitos. No se permiten tipos adicionales ni claves extra.</p>
	 *
	 * @return texto del system prompt listo para inyectar en {@code OllamaRequest}.
	 */
	public static String buildSystemPrompt() {
		return """
				You are an expert educational escape-room designer.
				Your ONLY job is to output a single JSON object that STRICTLY matches the schema below.

				HARD RULES:
				- Output ONLY the JSON object. No prose, no markdown, no code fences, no comments.
				- The JSON MUST contain EXACTLY these top-level keys, in this order:
				  "theme", "puzzle1_matrix", "puzzle2_router", "puzzle3_link", "puzzle4_console".
				- Do NOT add, remove or rename any key. Do NOT invent extra puzzles or fields.
				- Use plain UTF-8 text. Do not escape characters unnecessarily.
				- All string fields are REQUIRED and MUST be non-empty.

				PER-PUZZLE RULES:
				- puzzle1_matrix.categories: array of EXACTLY 2 non-empty strings.
				- puzzle1_matrix.items: non-empty array; each item has "name" (non-empty string)
				  and "categoryIndex" (integer, 0 or 1 only, referencing categories by position).
				- puzzle2_router.sequence: array of EXACTLY 5 non-empty strings, ordered from
				  first to last step of the procedure.
				- puzzle3_link.pairs: non-empty array; each pair has non-empty "concept" and
				  non-empty "definition".
				- puzzle4_console.pin: string of EXACTLY 4 digits (regex: ^\\d{4}$). No letters, no spaces.
				- puzzle4_console.deductionQuestion: non-empty string that lets the player deduce the pin.

				JSON SCHEMA (TypeScript-like notation, all keys required):
				{
				  "theme": string,
				  "puzzle1_matrix": {
				    "categories": [string, string],
				    "items": [ { "name": string, "categoryIndex": 0 | 1 }, ... ]
				  },
				  "puzzle2_router": {
				    "sequence": [string, string, string, string, string]
				  },
				  "puzzle3_link": {
				    "pairs": [ { "concept": string, "definition": string }, ... ]
				  },
				  "puzzle4_console": {
				    "pin": string,
				    "deductionQuestion": string
				  }
				}

				QUALITY RULES:
				- Content MUST be pedagogically meaningful and coherent with the requested topic and subtopics.
				- Keep texts concise, unambiguous and factually correct.
				- The 4 puzzles MUST collectively cover different facets of the topic.

				Respond with the JSON object ONLY.
				""";
	}

	/**
	 * Ensambla el prompt de usuario con los parámetros de la petición e inyecta
	 * <b>3 subtemas seleccionados al azar</b> del catálogo hardcodeado para la
	 * temática dada. Esto implementa "Rejugabilidad Determinista": el contenido
	 * varía entre llamadas sin alterar la temperatura del modelo.
	 *
	 * <p>Si la temática no está registrada en el catálogo, el prompt se emite sin
	 * sección de subtemas y se pide al modelo variar por sí mismo. Esto mantiene
	 * el sistema operativo para cualquier input del cliente.</p>
	 *
	 * @param topic      temática educativa principal (no nulo/vacío; validado aguas arriba).
	 * @param puzzles    número de puzzles solicitado (parámetro legacy; el esquema
	 *                   actual fija <b>exactamente 4</b> puzzles predefinidos, pero
	 *                   se conserva en la firma para compatibilidad con la capa de
	 *                   servicio).
	 * @param difficulty dificultad pedagógica solicitada.
	 * @param context    contexto o restricciones adicionales, opcional.
	 * @return texto del user prompt listo para inyectar en {@code OllamaRequest}.
	 */
	public static String buildUserPrompt(String topic, int puzzles, String difficulty, String context) {
		String safeContext = (context == null || context.isBlank())
				? "(none)"
				: context.trim();

		List<String> selectedSubtopics = selectRandomSubtopics(topic);
		String subtopicsBlock = selectedSubtopics.isEmpty()
				? "- Focus subtopics: (none available; vary freely across subtopics of the main topic)"
				: "- Focus subtopics (use these to vary content, at least one per puzzle when possible): "
						+ String.join(", ", selectedSubtopics);

		return """
				Generate an educational escape room with the following parameters:
				- Topic: %s
				- Overall difficulty: %s
				- Additional context: %s
				%s

				The room MUST contain exactly 4 puzzles following the predefined schema
				(puzzle1_matrix, puzzle2_router, puzzle3_link, puzzle4_console).
				The legacy "number of puzzles" hint from the client was: %d (ignore if it conflicts with the schema).

				Return ONLY the JSON object defined in the system schema. Do NOT wrap it in code fences.
				""".formatted(topic, difficulty, safeContext, subtopicsBlock, puzzles);
	}

	/**
	 * Selecciona {@value #SUBTOPICS_PER_PROMPT} subtemas distintos al azar del
	 * catálogo para la temática dada. Devuelve una lista vacía si no hay match.
	 */
	private static List<String> selectRandomSubtopics(String topic) {
		List<String> pool = findSubtopics(topic);
		if (pool.isEmpty()) {
			return List.of();
		}

		List<String> shuffleable = new ArrayList<>(pool);
		Collections.shuffle(shuffleable, RANDOM);
		int take = Math.min(SUBTOPICS_PER_PROMPT, shuffleable.size());
		return List.copyOf(shuffleable.subList(0, take));
	}

	/**
	 * Busca los subtemas asociados a un topic normalizando mayúsculas/minúsculas
	 * y espacios para tolerar variantes ortográficas del cliente.
	 */
	private static List<String> findSubtopics(String topic) {
		if (topic == null) {
			return List.of();
		}
		String normalized = topic.trim().toLowerCase();
		return SUBTOPICS_BY_TOPIC.getOrDefault(normalized, List.of());
	}
}
