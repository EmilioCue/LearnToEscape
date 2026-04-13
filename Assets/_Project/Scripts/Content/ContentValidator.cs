using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LearnToEscape.LLM;
using UnityEngine;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Valida objetos <see cref="RoomData"/> y <see cref="PuzzleData"/> deserializados.
    /// Orquesta reintentos contra <see cref="LLMClient"/> cuando Newtonsoft lanza
    /// una excepción de parsing (máximo configurable, por defecto 3).
    /// </summary>
    public static class ContentValidator
    {
        private static readonly HashSet<string> ValidPuzzleTypes = new(StringComparer.OrdinalIgnoreCase)
            { "lock", "cipher", "search", "logic", "sequence" };

        private static readonly HashSet<string> ValidDifficulties = new(StringComparer.OrdinalIgnoreCase)
            { "easy", "medium", "hard" };

        private const int MinTimeLimit = 1;
        private const int MaxTimeLimit = 60;

        /// <summary>
        /// Envía la petición al LLM, parsea la respuesta como <see cref="RoomData"/>,
        /// valida los campos, y reintenta hasta <paramref name="maxRetries"/> veces
        /// si el JSON es inválido o la validación falla.
        /// </summary>
        public static async Task<RoomData> RequestValidatedRoom(
            LLMClient client,
            string userPrompt,
            string systemPrompt,
            int maxRetries = 3,
            CancellationToken ct = default,
            int expectedPuzzleCount = -1)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string raw = await client.SendRequest(userPrompt, systemPrompt, forceJson: true, ct);
                    var room = LLMResponseParser.ParseRoom(raw);

                    var errors = ValidateRoom(room, expectedPuzzleCount);
                    if (errors.Count == 0)
                        return room;

                    lastException = new ContentValidationException(
                        $"Room validation failed: {string.Join(" | ", errors)}");

                    Debug.LogWarning(
                        $"[ContentValidator] Intento {attempt}/{maxRetries} — " +
                        $"validación fallida: {string.Join(" | ", errors)}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (TimeoutException ex)
                {
                    Debug.LogWarning(
                        "[ContentValidator] Timeout del LLM — no se reintenta (repetir no ayuda). " +
                        ex.Message);
                    throw new ContentValidationException(
                        "El LLM no respondió a tiempo (sin datos o demasiado lentos). " +
                        "En LLMConfig: sube MaxTotalRequestSeconds, baja el modelo o calienta con " +
                        "`ollama run <modelo>`. Comprueba Ollama con curl en el puerto 11434.", ex);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.LogWarning(
                        $"[ContentValidator] Intento {attempt}/{maxRetries} — " +
                        $"error: {ex.Message}");
                }
            }

            throw new ContentValidationException(
                $"Failed to obtain valid RoomData after {maxRetries} attempts.", lastException);
        }

        /// <summary>
        /// Envía la petición al LLM, parsea la respuesta como <see cref="PuzzleData"/>,
        /// valida los campos, y reintenta hasta <paramref name="maxRetries"/> veces.
        /// </summary>
        public static async Task<PuzzleData> RequestValidatedPuzzle(
            LLMClient client,
            string userPrompt,
            string systemPrompt,
            int maxRetries = 3,
            CancellationToken ct = default)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string raw = await client.SendRequest(userPrompt, systemPrompt, forceJson: true, ct);
                    var puzzle = LLMResponseParser.ParsePuzzle(raw);

                    var errors = ValidatePuzzle(puzzle);
                    if (errors.Count == 0)
                        return puzzle;

                    lastException = new ContentValidationException(
                        $"Puzzle validation failed: {string.Join(" | ", errors)}");

                    Debug.LogWarning(
                        $"[ContentValidator] Intento {attempt}/{maxRetries} — " +
                        $"validación fallida: {string.Join(" | ", errors)}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (TimeoutException ex)
                {
                    Debug.LogWarning(
                        "[ContentValidator] Timeout del LLM — no se reintenta. " + ex.Message);
                    throw new ContentValidationException(
                        "El LLM no respondió a tiempo. Ajusta MaxTotalRequestSeconds en LLMConfig " +
                        "o usa un modelo más rápido.", ex);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.LogWarning(
                        $"[ContentValidator] Intento {attempt}/{maxRetries} — " +
                        $"error: {ex.Message}");
                }
            }

            throw new ContentValidationException(
                $"Failed to obtain valid PuzzleData after {maxRetries} attempts.", lastException);
        }

        public static List<string> ValidateRoom(RoomData room, int expectedPuzzleCount = -1)
        {
            var errors = new List<string>();

            if (room == null) { errors.Add("RoomData is null"); return errors; }

            if (string.IsNullOrWhiteSpace(room.id))
                errors.Add("id is missing");
            if (string.IsNullOrWhiteSpace(room.name))
                errors.Add("name is missing");
            if (string.IsNullOrWhiteSpace(room.theme))
                errors.Add("theme is missing");
            if (string.IsNullOrWhiteSpace(room.description))
                errors.Add("description is missing");
            if (string.IsNullOrWhiteSpace(room.narrativeIntro))
                errors.Add("narrativeIntro is missing");

            if (room.timeLimitMinutes < MinTimeLimit || room.timeLimitMinutes > MaxTimeLimit)
                errors.Add($"timeLimitMinutes ({room.timeLimitMinutes}) out of range [{MinTimeLimit}–{MaxTimeLimit}]");

            if (room.puzzles == null)
            {
                errors.Add("puzzles array is null");
            }
            else if (expectedPuzzleCount == 0 && room.puzzles.Length == 0)
            {
                // Explicitly requested 0 puzzles — empty array is valid.
            }
            else if (room.puzzles.Length == 0)
            {
                errors.Add("puzzles array is empty (expected at least 1 puzzle)");
            }
            else
            {
                for (int i = 0; i < room.puzzles.Length; i++)
                {
                    var puzzleErrors = ValidatePuzzle(room.puzzles[i]);
                    foreach (var pe in puzzleErrors)
                        errors.Add($"puzzles[{i}].{pe}");
                }
            }

            return errors;
        }

        public static List<string> ValidatePuzzle(PuzzleData puzzle)
        {
            var errors = new List<string>();

            if (puzzle == null) { errors.Add("PuzzleData is null"); return errors; }

            if (string.IsNullOrWhiteSpace(puzzle.id))
                errors.Add("id is missing");
            if (string.IsNullOrWhiteSpace(puzzle.name))
                errors.Add("name is missing");
            if (string.IsNullOrWhiteSpace(puzzle.description))
                errors.Add("description is missing");
            if (string.IsNullOrWhiteSpace(puzzle.solution))
                errors.Add("solution is missing");

            if (string.IsNullOrWhiteSpace(puzzle.type))
                errors.Add("type is missing");
            else if (!ValidPuzzleTypes.Contains(puzzle.type))
                errors.Add($"type '{puzzle.type}' is not valid (expected: lock|cipher|search|logic|sequence)");

            if (string.IsNullOrWhiteSpace(puzzle.difficulty))
                errors.Add("difficulty is missing");
            else if (!ValidDifficulties.Contains(puzzle.difficulty))
                errors.Add($"difficulty '{puzzle.difficulty}' is not valid (expected: easy|medium|hard)");

            if (puzzle.hints == null)
                errors.Add("hints array is null");

            return errors;
        }
    }

    public class ContentValidationException : Exception
    {
        public ContentValidationException(string message) : base(message) { }
        public ContentValidationException(string message, Exception inner) : base(message, inner) { }
    }
}
