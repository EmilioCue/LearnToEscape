namespace LearnToEscape.LLM
{
    /// <summary>
    /// Construye prompts estrictos para que el LLM devuelva JSON válido
    /// que se pueda deserializar directamente a <see cref="Content.RoomData"/>
    /// y <see cref="Content.PuzzleData"/>.
    /// </summary>
    public static class LLMRequestBuilder
    {
        public const string PuzzleJsonSchema = @"{
  ""id"":        ""string  — identificador único (snake_case, e.g. cipher_caesar_01)"",
  ""name"":      ""string  — nombre corto del puzzle"",
  ""description"":""string — descripción narrativa que el jugador lee al descubrir el puzzle"",
  ""type"":      ""string  — uno de: lock | cipher | search | logic | sequence"",
  ""solution"":  ""string  — solución exacta (texto o secuencia)"",
  ""hints"":     [""string — pista 1"", ""string — pista 2"", ""string — pista 3""],
  ""difficulty"":""string  — uno de: easy | medium | hard"",
  ""requiredItems"":[""string — objeto necesario para resolver el puzzle (puede estar vacío)""]
}";

        public const string RoomJsonSchema = @"{
  ""id"":              ""string  — identificador único (snake_case, e.g. haunted_lab_01)"",
  ""theme"":           ""string  — temática visual/narrativa (e.g. laboratorio, mazmorra, nave espacial)"",
  ""name"":            ""string  — nombre de la sala"",
  ""description"":     ""string  — descripción general de la sala"",
  ""narrativeIntro"":  ""string  — texto que se muestra al jugador al entrar en la sala"",
  ""timeLimitMinutes"":""number  — tiempo límite en minutos (entre 5 y 30)"",
  ""puzzles"":         [ <PuzzleSchema> ]
}";

        private const string JsonOnlyConstraint =
            "REGLAS ABSOLUTAS DE FORMATO:\n" +
            "1. Empieza tu respuesta con { y termínala con }.\n" +
            "2. NO escribas texto antes o después del JSON.\n" +
            "3. NO uses bloques de código markdown (```). Solo JSON puro.\n" +
            "4. Todos los campos del schema son OBLIGATORIOS.\n" +
            "5. Los valores string van entre comillas dobles.\n" +
            "6. Si un array puede estar vacío, devuelve [].\n" +
            "7. Si violas cualquiera de estas reglas, la respuesta se descarta.";

        /// <summary>
        /// System prompt para generar una sala completa con sus puzzles.
        /// </summary>
        public static string BuildRoomGenerationSystemPrompt()
        {
            return
                "Eres un diseñador de escape rooms para un videojuego educativo.\n" +
                "Tu ÚNICA función es generar contenido en formato JSON estricto.\n\n" +
                JsonOnlyConstraint + "\n\n" +
                "=== SCHEMA DE UN PUZZLE ===\n" +
                PuzzleJsonSchema + "\n\n" +
                "=== SCHEMA DE UNA SALA (contiene puzzles) ===\n" +
                RoomJsonSchema.Replace("<PuzzleSchema>", "... objetos con el schema de puzzle de arriba ...") + "\n\n" +
                "Genera contenido creativo pero respeta el schema al pie de la letra.\n" +
                "Empieza tu respuesta con { y termínala con }.";
        }

        /// <summary>
        /// System prompt para generar un único puzzle suelto.
        /// </summary>
        public static string BuildPuzzleGenerationSystemPrompt()
        {
            return
                "Eres un diseñador de puzzles para un videojuego de escape room educativo.\n" +
                "Tu ÚNICA función es generar UN puzzle en formato JSON estricto.\n\n" +
                JsonOnlyConstraint + "\n\n" +
                "=== SCHEMA DEL PUZZLE ===\n" +
                PuzzleJsonSchema + "\n\n" +
                "Genera contenido creativo pero respeta el schema al pie de la letra.\n" +
                "Empieza tu respuesta con { y termínala con }.";
        }

        /// <summary>
        /// Prompt de usuario para pedir una sala con parámetros concretos.
        /// Cuando se proporciona <paramref name="factualContext"/>, el LLM queda
        /// anclado a esos datos y no puede inventar información externa.
        /// </summary>
        public static string BuildRoomUserPrompt(
            string topicName,
            int puzzleCount,
            string difficulty,
            string factualContext = null)
        {
            var prompt =
                $"Genera una sala de escape room con temática \"{topicName}\". " +
                $"Debe contener exactamente {puzzleCount} puzzles de dificultad \"{difficulty}\". ";

            if (!string.IsNullOrWhiteSpace(factualContext))
            {
                prompt +=
                    "\n\n=== BASE DE CONOCIMIENTO (FUENTE ÚNICA DE VERDAD) ===\n" +
                    factualContext + "\n" +
                    "=== FIN DE LA BASE DE CONOCIMIENTO ===\n\n" +
                    "REGLAS SOBRE EL CONTENIDO:\n" +
                    "1. Usa EXCLUSIVAMENTE la información de la base de conocimiento anterior " +
                    "para generar las preguntas de los puzzles, las pistas y las explicaciones.\n" +
                    "2. NO inventes datos, cifras, nombres o hechos que no aparezcan en el texto proporcionado.\n" +
                    "3. Las soluciones de los puzzles DEBEN poder verificarse contra la base de conocimiento.\n" +
                    "4. Si la base de conocimiento no contiene suficiente información para un puzzle, " +
                    "reformula el puzzle para que se ajuste a los datos disponibles.\n";
            }

            prompt += "Devuelve SOLO el JSON de la sala siguiendo el schema proporcionado.";
            return prompt;
        }

        /// <summary>
        /// Prompt de usuario para pedir un puzzle individual.
        /// </summary>
        public static string BuildPuzzleUserPrompt(string type, string difficulty, string context)
        {
            return
                $"Genera un puzzle de tipo \"{type}\" con dificultad \"{difficulty}\". " +
                $"Contexto narrativo: \"{context}\". " +
                "Devuelve SOLO el JSON del puzzle siguiendo el schema proporcionado.";
        }
    }
}
