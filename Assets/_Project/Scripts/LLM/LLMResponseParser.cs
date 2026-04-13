using System;
using System.Text.RegularExpressions;
using LearnToEscape.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LearnToEscape.LLM
{
    /// <summary>
    /// Extrae el texto generado de la respuesta Ollama y lo deserializa
    /// a <see cref="RoomData"/> o <see cref="PuzzleData"/> usando Newtonsoft.Json.
    /// Lanza <see cref="JsonException"/> si el JSON es inválido.
    /// </summary>
    public static class LLMResponseParser
    {
        private static readonly Regex MarkdownFenceRegex =
            new(@"```(?:json)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.Compiled);

        /// <summary>
        /// Extrae el campo "response" del wrapper JSON de Ollama.
        /// <c>{ "model":"…", "response":"…", "done":true }</c>
        /// </summary>
        public static string ExtractResponseText(string ollamaRawBody)
        {
            if (string.IsNullOrWhiteSpace(ollamaRawBody))
                throw new ArgumentException("Ollama response body is null or empty.");

            var wrapper = JObject.Parse(ollamaRawBody);
            var responseToken = wrapper["response"];

            if (responseToken == null)
                throw new JsonException(
                    "Ollama response does not contain a 'response' field.");

            return responseToken.Value<string>() ?? string.Empty;
        }

        public static RoomData ParseRoom(string ollamaRawBody)
        {
            var llmText = ExtractResponseText(ollamaRawBody);
            var json = SanitizeJson(llmText);
            return JsonConvert.DeserializeObject<RoomData>(json);
        }

        public static PuzzleData ParsePuzzle(string ollamaRawBody)
        {
            var llmText = ExtractResponseText(ollamaRawBody);
            var json = SanitizeJson(llmText);
            return JsonConvert.DeserializeObject<PuzzleData>(json);
        }

        /// <summary>
        /// Elimina bloques de código markdown y texto espurio alrededor del JSON.
        /// </summary>
        private static string SanitizeJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new JsonException("LLM returned an empty response.");

            var match = MarkdownFenceRegex.Match(raw);
            if (match.Success)
                raw = match.Groups[1].Value;

            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');

            if (start < 0 || end < 0 || end <= start)
                throw new JsonException(
                    $"No JSON object found in LLM response. Raw text: {Truncate(raw, 200)}");

            return raw.Substring(start, end - start + 1);
        }

        private static string Truncate(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s;
            return s.Substring(0, maxLength) + "…";
        }
    }
}
