using System.Collections.Generic;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Validación defensiva en cliente del contrato estricto de 4 puzzles.
    /// Se ejecuta tras deserializar la respuesta del backend para detectar,
    /// ANTES de instanciar contenido en escena, cualquier desviación respecto
    /// al esquema (campos nulos, tamaños incorrectos, PIN mal formado…).
    /// </summary>
    /// <remarks>
    /// Duplica intencionadamente las reglas de Bean Validation del backend:
    /// el cliente no puede asumir que la respuesta sea válida sólo porque
    /// proviene de un endpoint que valida en servidor — podría haber un
    /// servicio mockeado, un proxy, o una regresión en el backend.
    /// </remarks>
    public static class ContentValidator
    {
        /// <summary>
        /// Valida una <see cref="RoomData"/> completa contra el esquema cerrado.
        /// </summary>
        /// <returns>
        /// Lista de errores encontrados. Vacía si la sala es estructuralmente válida.
        /// </returns>
        public static List<string> ValidateRoom(RoomData room)
        {
            var errors = new List<string>();

            if (room == null)
            {
                errors.Add("RoomData is null");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(room.theme))
                errors.Add("theme is missing");

            ValidatePuzzle1Matrix(room.puzzle1_matrix, errors);
            ValidatePuzzle2Router(room.puzzle2_router, errors);
            ValidatePuzzle3Link(room.puzzle3_link, errors);
            ValidatePuzzle4Console(room.puzzle4_console, errors);

            return errors;
        }

        private static void ValidatePuzzle1Matrix(Puzzle1Matrix puzzle, List<string> errors)
        {
            if (puzzle == null)
            {
                errors.Add("puzzle1_matrix is null");
                return;
            }

            if (puzzle.categories == null || puzzle.categories.Length != 2)
            {
                errors.Add("puzzle1_matrix.categories must contain exactly 2 entries");
            }
            else
            {
                for (int i = 0; i < puzzle.categories.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(puzzle.categories[i]))
                        errors.Add($"puzzle1_matrix.categories[{i}] is empty");
                }
            }

            if (puzzle.items == null || puzzle.items.Length < 2)
            {
                errors.Add("puzzle1_matrix.items must contain at least 2 entries");
            }
            else
            {
                for (int i = 0; i < puzzle.items.Length; i++)
                {
                    var item = puzzle.items[i];
                    if (item == null)
                    {
                        errors.Add($"puzzle1_matrix.items[{i}] is null");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(item.name))
                        errors.Add($"puzzle1_matrix.items[{i}].name is empty");
                    if (item.categoryIndex < 0 || item.categoryIndex > 1)
                        errors.Add($"puzzle1_matrix.items[{i}].categoryIndex must be 0 or 1");
                }
            }
        }

        private static void ValidatePuzzle2Router(Puzzle2Router puzzle, List<string> errors)
        {
            if (puzzle == null)
            {
                errors.Add("puzzle2_router is null");
                return;
            }

            if (puzzle.sequence == null || puzzle.sequence.Length != 5)
            {
                errors.Add("puzzle2_router.sequence must contain exactly 5 entries");
                return;
            }

            for (int i = 0; i < puzzle.sequence.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(puzzle.sequence[i]))
                    errors.Add($"puzzle2_router.sequence[{i}] is empty");
            }
        }

        private static void ValidatePuzzle3Link(Puzzle3Link puzzle, List<string> errors)
        {
            if (puzzle == null)
            {
                errors.Add("puzzle3_link is null");
                return;
            }

            if (puzzle.pairs == null || puzzle.pairs.Length < 2)
            {
                errors.Add("puzzle3_link.pairs must contain at least 2 entries");
                return;
            }

            for (int i = 0; i < puzzle.pairs.Length; i++)
            {
                var pair = puzzle.pairs[i];
                if (pair == null)
                {
                    errors.Add($"puzzle3_link.pairs[{i}] is null");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(pair.concept))
                    errors.Add($"puzzle3_link.pairs[{i}].concept is empty");
                if (string.IsNullOrWhiteSpace(pair.definition))
                    errors.Add($"puzzle3_link.pairs[{i}].definition is empty");
            }
        }

        private static void ValidatePuzzle4Console(Puzzle4Console puzzle, List<string> errors)
        {
            if (puzzle == null)
            {
                errors.Add("puzzle4_console is null");
                return;
            }

            if (string.IsNullOrEmpty(puzzle.pin) || puzzle.pin.Length != 4 || !IsAllDigits(puzzle.pin))
                errors.Add("puzzle4_console.pin must be exactly 4 numeric digits");

            if (string.IsNullOrWhiteSpace(puzzle.deductionQuestion))
                errors.Add("puzzle4_console.deductionQuestion is missing");

            // Chain of Thought: si no hay razonamiento, el PIN no es auditable
            // y, por contrato, el backend ha incumplido el esquema CoT.
            if (string.IsNullOrWhiteSpace(puzzle.stepByStepReasoning))
                errors.Add("puzzle4_console.stepByStepReasoning is missing");
        }

        private static bool IsAllDigits(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                    return false;
            }
            return true;
        }
    }
}
