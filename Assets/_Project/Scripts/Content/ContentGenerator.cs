using System.Threading;
using System.Threading.Tasks;
using LearnToEscape.LLM;
using UnityEngine;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Punto de entrada para generar contenido procedural vía LLM.
    /// Construye los prompts, delega en <see cref="ContentValidator"/> para
    /// parsing + validación + reintentos, y devuelve instancias de
    /// <see cref="RoomData"/> / <see cref="PuzzleData"/> listas para usar.
    /// </summary>
    public class ContentGenerator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private LLMClient _llmClient;

        [Header("Retry Policy")]
        [SerializeField, Range(1, 5)] private int _maxRetries = 3;

        /// <summary>
        /// Genera una sala completa con puzzles validados.
        /// El <paramref name="knowledgeBase"/> ancla al LLM a datos verificados,
        /// evitando que alucine contenido fuera del contexto factual proporcionado.
        /// </summary>
        public async Task<RoomData> GenerateRoom(
            TopicKnowledgeBase knowledgeBase,
            int puzzleCount,
            string difficulty,
            CancellationToken ct = default)
        {
            if (knowledgeBase == null || !knowledgeBase.IsValid)
                throw new System.ArgumentException(
                    "TopicKnowledgeBase is null or has empty fields. " +
                    "Assign a valid asset in the Inspector.", nameof(knowledgeBase));

            string systemPrompt = LLMRequestBuilder.BuildRoomGenerationSystemPrompt();
            string userPrompt = LLMRequestBuilder.BuildRoomUserPrompt(
                knowledgeBase.topicName,
                puzzleCount,
                difficulty,
                knowledgeBase.factualContext);

            Debug.Log($"[ContentGenerator] Solicitando sala — tema: {knowledgeBase.topicName}, " +
                      $"puzzles: {puzzleCount}, dificultad: {difficulty}, " +
                      $"contexto factual: {knowledgeBase.factualContext.Length} chars");

            var room = await ContentValidator.RequestValidatedRoom(
                _llmClient, userPrompt, systemPrompt, _maxRetries, ct,
                expectedPuzzleCount: puzzleCount);

            Debug.Log($"[ContentGenerator] Sala generada: \"{room.name}\" " +
                      $"con {room.puzzles.Length} puzzles.");

            return room;
        }

        /// <summary>
        /// Genera un puzzle individual validado.
        /// </summary>
        public async Task<PuzzleData> GeneratePuzzle(
            string type,
            string difficulty,
            string context,
            CancellationToken ct = default)
        {
            string systemPrompt = LLMRequestBuilder.BuildPuzzleGenerationSystemPrompt();
            string userPrompt = LLMRequestBuilder.BuildPuzzleUserPrompt(type, difficulty, context);

            Debug.Log($"[ContentGenerator] Solicitando puzzle — tipo: {type}, " +
                      $"dificultad: {difficulty}");

            var puzzle = await ContentValidator.RequestValidatedPuzzle(
                _llmClient, userPrompt, systemPrompt, _maxRetries, ct);

            Debug.Log($"[ContentGenerator] Puzzle generado: \"{puzzle.name}\" " +
                      $"(tipo: {puzzle.type}).");

            return puzzle;
        }
    }
}
