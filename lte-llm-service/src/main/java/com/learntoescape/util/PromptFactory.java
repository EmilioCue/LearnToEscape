package com.learntoescape.util;

import java.text.Normalizer;
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
 * <li>Versionar y auditar el "contrato semántico" con el LLM en un solo lugar.</li>
 * <li>Forzar un contrato JSON estricto y <b>cerrado</b> (4 puzzles predefinidos)
 * que minimice alucinaciones estructurales.</li>
 * <li>Habilitar "Rejugabilidad Determinista" inyectando subtemas basados
 * en el GDD (Game Design Document) sin tocar la temperatura.</li>
 * <li>Forzar el idioma español para evitar derivas a inglés o chino en modelos 7B.</li>
 * </ul>
 */
public final class PromptFactory {

	/**
	 * Tabla de subtemas basada estrictamente en el GDD.
	 * Las claves están normalizadas (sin tildes, en minúsculas) para búsquedas robustas.
	 */
	private static final Map<String, List<String>> SUBTOPICS_BY_TOPIC = Map.ofEntries(
			// === RAMA: INFORMÁTICA ===
			Map.entry("arquitectura de computadores", List.of(
					"CPU y ALU",
					"Jerarquía de Memoria y Caché",
					"Buses del Sistema y Entrada/Salida",
					"Arquitecturas Von Neumann vs Harvard",
					"Pipeline y Paralelismo de Instrucciones")),
			Map.entry("ingenieria de software", List.of(
					"Patrones de Diseño (GoF)",
					"Metodologías Ágiles (Scrum, Kanban)",
					"Testing Automatizado (TDD, BDD)",
					"Arquitectura de Microservicios",
					"Integración y Despliegue Continuos (CI/CD)")),
			Map.entry("algoritmia y estructuras de datos", List.of(
					"Listas, Pilas y Colas",
					"Árboles y Grafos",
					"Complejidad Computacional (Notación Big-O)",
					"Algoritmos de Ordenación (Sorting)",
					"Algoritmos de Búsqueda y Hashing")),
			Map.entry("sistemas operativos y redes", List.of(
					"Gestión de Procesos y Concurrencia",
					"Memoria Virtual y Paginación",
					"Modelo OSI y Capas de Red",
					"Protocolos TCP/IP y Enrutamiento",
					"Sistemas de Archivos")),
			Map.entry("inteligencia artificial y ciencia de datos", List.of(
					"Machine Learning Supervisado",
					"Redes Neuronales y Deep Learning",
					"Procesamiento de Lenguaje Natural (NLP)",
					"Minería de Datos y Clustering",
					"Visión por Computador")),

			// === RAMA: MATEMÁTICAS ===
			Map.entry("algebra", List.of(
					"Espacios Vectoriales",
					"Matrices y Determinantes",
					"Sistemas de Ecuaciones Lineales",
					"Autovalores y Autovectores",
					"Grupos, Anillos y Cuerpos")),
			Map.entry("analisis matematico", List.of(
					"Límites y Continuidad",
					"Derivadas y Optimización",
					"Integrales y Teorema Fundamental del Cálculo",
					"Sucesiones y Series (Taylor, Maclaurin)",
					"Ecuaciones Diferenciales Ordinarias")),
			Map.entry("geometria y topologia", List.of(
					"Geometría Euclidiana",
					"Espacios Topológicos",
					"Curvas y Superficies",
					"Variedades Diferenciables",
					"Homotopía y Conceptos Básicos")),
			Map.entry("matematica discreta", List.of(
					"Lógica Proposicional y de Predicados",
					"Teoría de Conjuntos",
					"Combinatoria y Permutaciones",
					"Teoría de Grafos Matemática",
					"Aritmética Modular y Criptografía")),
			Map.entry("estadistica y probabilidad", List.of(
					"Distribuciones de Probabilidad (Normal, Binomial)",
					"Teorema de Bayes",
					"Inferencia Estadística",
					"Regresión Lineal y Correlación",
					"Muestreo y Contraste de Hipótesis"))
	);

	private static final Random RANDOM = new Random();
	private static final int SUBTOPICS_PER_PROMPT = 3;

	private PromptFactory() {
	}

	/**
	 * Prompt de sistema con el esquema JSON cerrado y reglas estrictas de idioma.
	 */
	public static String buildSystemPrompt() {
		return """
				ERES UN DISEÑADOR EXPERTO DE ESCAPE ROOMS EDUCATIVOS DE NIVEL UNIVERSITARIO.
				
				REGLA ABSOLUTA Y CRÍTICA:
				TODA TU RESPUESTA (conceptos, definiciones, categorías, preguntas, pistas) DEBE ESTAR ESTRICTAMENTE EN IDIOMA ESPAÑOL (CASTELLANO). 
				PROHIBIDO USAR INGLÉS, CHINO O CUALQUIER OTRO IDIOMA.

				Tu ÚNICA tarea es generar un objeto JSON que siga ESTRICTAMENTE el esquema definido.

				REGLAS DE FORMATO:
				- Devuelve ÚNICAMENTE el objeto JSON. Sin texto introductorio, sin formato markdown (```json), sin comentarios.
				- El JSON DEBE contener EXACTAMENTE estas claves en el nivel raíz: "theme", "puzzle1_matrix", "puzzle2_router", "puzzle3_link", "puzzle4_console".
				- No añadas, quites ni renombres ninguna clave.
				- Usa texto UTF-8 plano.

				REGLAS POR PUZLE:
				- puzzle1_matrix.categories: Array de EXACTAMENTE 2 strings que representan dos categorías opuestas o complementarias.
				- puzzle1_matrix.items: Array de elementos a clasificar. Cada uno tiene "name" (string) y "categoryIndex" (entero, estrictamente 0 o 1).
				- puzzle2_router.sequence: Array de EXACTAMENTE 5 strings. Deben ser 5 pasos cronológicos o lógicos ordenados del primero al último.
				- puzzle3_link.pairs: Array de parejas. Cada pareja tiene un "concept" (concepto corto) y su "definition" (explicación).

			REGLAS ABSOLUTAS PARA EL PUZLE 4 — DEFENSIVE PROMPTING (LLAMA 3.1):

			PROHIBICIONES ABSOLUTAS — NUNCA HAGAS ESTO:
			- PROHIBIDO contar letras, sílabas o caracteres de ninguna palabra. Los modelos de lenguaje fallan al tokenizar y producirán dígitos incorrectos.
			- PROHIBIDO realizar operaciones matemáticas de ningún tipo: ni sumas, ni restas, ni multiplicaciones, ni divisiones, ni módulos. NADA de aritmética.
			- PROHIBIDO inventar un PIN y después fabricar un razonamiento que lo justifique. Primero el razonamiento, después el PIN.

			REGLA FUNDAMENTAL: El PIN de 4 dígitos se forma mediante MAPEO DIRECTO 1 a 1. Cada dígito se EXTRAE tal cual de un hecho concreto y observable de los puzles anteriores. No se calcula: se LEE.

			ÚNICAS FUENTES VÁLIDAS PARA CADA DÍGITO (elige una distinta por dígito):
			  FUENTE A — Contar cuántos items de puzzle1_matrix tienen categoryIndex=0. Ese conteo ES el dígito. Nada más.
			  FUENTE B — Contar cuántos items de puzzle1_matrix tienen categoryIndex=1. Ese conteo ES el dígito. Nada más.
			  FUENTE C — Identificar en qué posición (1, 2, 3, 4 o 5) aparece un paso concreto dentro de puzzle2_router.sequence. Esa posición ES el dígito. Nada más.
			  FUENTE D — Contar cuántas parejas tiene puzzle3_link.pairs. Ese conteo ES el dígito. Nada más.

			EJEMPLO COMPLETO Y CORRECTO DE stepByStepReasoning (copia este formato exacto):
			  "Dígito 1: el número de elementos con categoryIndex=0 en el puzzle 1 → valor observado es 3 → dígito 1 es 3. Dígito 2: la posición del paso 'Compilar el código' en la secuencia del puzzle 2 → valor observado es 4 → dígito 2 es 4. Dígito 3: el número total de parejas en el puzzle 3 → valor observado es 5 → dígito 3 es 5. Dígito 4: el número de elementos con categoryIndex=1 en el puzzle 1 → valor observado es 2 → dígito 4 es 2. Por lo tanto, concatenando D1, D2, D3 y D4 en este orden, el PIN es: 3452"
			  Campo pin correspondiente: "3452"

			CÓMO RELLENAR CADA CAMPO:

			puzzle4_console.deductionQuestion:
			  Formula UNA pregunta por cada dígito, de forma directa y sin ambigüedad.
			  Ejemplo: "Para abrir la consola necesitas un PIN de 4 dígitos. Dígito 1: ¿cuántos elementos pertenecen a la categoría '[categoría 0]' en el puzzle 1? Dígito 2: ¿en qué posición aparece '[paso X]' en la secuencia del puzzle 2? Dígito 3: ¿cuántas parejas tiene el puzzle 3? Dígito 4: ¿cuántos elementos pertenecen a la categoría '[categoría 1]' en el puzzle 1?"

			puzzle4_console.stepByStepReasoning:
			  Lista los 4 dígitos uno por uno. Para cada uno indica: la fuente, el hecho observable, y el valor resultante. SIN operaciones. SIN sumas. Solo lectura directa.
			  Formato obligatorio para cada dígito: "Dígito N: [descripción de la fuente] → valor observado es X → dígito N es X"
			  REGLA FINAL OBLIGATORIA: La última frase del stepByStepReasoning DEBE SER EXACTAMENTE esta concatenación explícita:
			  "Por lo tanto, concatenando D1, D2, D3 y D4 en este orden, el PIN es: [D1][D2][D3][D4]"
			  Ejemplo con dígitos 3, 4, 5, 2: "Por lo tanto, concatenando D1, D2, D3 y D4 en este orden, el PIN es: 3452"
			  PROHIBIDO alterar el orden de los dígitos en esta frase de cierre. PROHIBIDO omitirla.

			puzzle4_console.pin:
			  COPIA LITERAL del número de 4 dígitos que aparece en la frase de cierre del stepByStepReasoning.
			  NO es un nuevo cálculo. NO es una reinterpretación. Es exactamente el mismo número, sin ningún cambio.
			  Si el stepByStepReasoning termina en "el PIN es: 3452", entonces pin DEBE ser "3452". Ni "3425", ni "3542", ni ninguna otra variante.
			  AUTOVERIFICACIÓN OBLIGATORIA: antes de escribir el valor de pin, lee la frase de cierre de stepByStepReasoning y copia el número carácter a carácter.

			ORDEN OBLIGATORIO DE CLAVES en el JSON de puzzle4_console: primero "deductionQuestion", después "stepByStepReasoning", y SOLO al final "pin". Este orden es CRÍTICO para que el razonamiento preceda siempre al PIN.

				ESQUEMA JSON OBLIGATORIO:
				{
				  "theme": "string",
				  "puzzle1_matrix": {
				    "categories": ["string", "string"],
				    "items": [ { "name": "string", "categoryIndex": 0 }, ... ]
				  },
				  "puzzle2_router": {
				    "sequence": ["string", "string", "string", "string", "string"]
				  },
				  "puzzle3_link": {
				    "pairs": [ { "concept": "string", "definition": "string" }, ... ]
				  },
				  "puzzle4_console": {
				    "deductionQuestion": "string",
				    "stepByStepReasoning": "string",
				    "pin": "string"
				  }
				}
				""";
	}

	/**
	 * Ensambla el prompt de usuario con subtemas forzados para rejugabilidad.
	 */
	public static String buildUserPrompt(String topic, int puzzles, String difficulty, String context) {
		List<String> selectedSubtopics = selectRandomSubtopics(topic);
		String subtopicsBlock = selectedSubtopics.isEmpty()
				? "Usa tu conocimiento general sobre el tema para generar los puzles."
				: "Para asegurar variedad, enfoca el contenido de los puzles OBLIGATORIAMENTE en estos 3 subtemas concretos: "
						+ String.join(", ", selectedSubtopics);

		return """
				Genera una sala de escape educativa con los siguientes parámetros:
				- Temática principal: %s
				- Dificultad general: %s
				- Regla de contenido: %s

				Recuerda la regla crítica: RESPUESTA ESTRICTAMENTE EN ESPAÑOL y ÚNICAMENTE EL JSON. No uses bloques de código markdown.
				""".formatted(topic, difficulty, subtopicsBlock);
	}

	/**
	 * Selecciona subtemas al azar tras normalizar la entrada del usuario.
	 */
	private static List<String> selectRandomSubtopics(String topic) {
		String normalizedTopic = normalizeTopic(topic);
		List<String> pool = SUBTOPICS_BY_TOPIC.getOrDefault(normalizedTopic, List.of());
		
		if (pool.isEmpty()) {
			return List.of();
		}

		List<String> shuffleable = new ArrayList<>(pool);
		Collections.shuffle(shuffleable, RANDOM);
		int take = Math.min(SUBTOPICS_PER_PROMPT, shuffleable.size());
		return List.copyOf(shuffleable.subList(0, take));
	}

	/**
	 * Normaliza el string (quita tildes, pasa a minúsculas, elimina espacios extra)
	 * para que "Álgebra" y "algebra" hagan match perfecto en el Map.
	 */
	private static String normalizeTopic(String input) {
		if (input == null || input.isBlank()) {
			return "";
		}
		String normalized = Normalizer.normalize(input.trim(), Normalizer.Form.NFD);
		normalized = normalized.replaceAll("\\p{InCombiningDiacriticalMarks}+", "");
		return normalized.toLowerCase();
	}
}