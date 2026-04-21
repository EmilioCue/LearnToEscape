using System;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Rooms;
using UnityEngine;

public class PipelineTester : MonoBehaviour
{
    [Header("Dependencias")]
    public ContentGenerator generator;

    [Header("Parámetros de Prueba")]
    public TopicKnowledgeBase knowledgeBase;
    public int puzzleCount = 4;
    public string difficulty = "easy";

    [ContextMenu("EJECUTAR TEST DE PIPELINE")]
    public async void RunPipelineTest()
    {
        string topicLabel = knowledgeBase != null ? knowledgeBase.topicName : "(sin asignar)";
        Debug.Log($"<color=cyan>[Tester]</color> Generando sala (backend / dry-run). Tema: {topicLabel}...");

        try
        {
            RoomData result = await generator.GenerateRoom(knowledgeBase, puzzleCount, difficulty);

            if (result == null)
            {
                Debug.LogError("<color=red>TEST FALLIDO:</color> GenerateRoom devolvió null (validación o error).");
                return;
            }

            Debug.Log("<color=green>Éxito.</color> Sala en memoria.");
            Debug.Log($"Tema: {result.theme}");

            int categoryCount = result.puzzle1_matrix?.categories?.Length ?? 0;
            int itemCount = result.puzzle1_matrix?.items?.Length ?? 0;
            Debug.Log($"Puzzle 1 (matrix): {categoryCount} categorías / {itemCount} ítems");

            int sequenceCount = result.puzzle2_router?.sequence?.Length ?? 0;
            Debug.Log($"Puzzle 2 (router): {sequenceCount} pasos");

            int pairCount = result.puzzle3_link?.pairs?.Length ?? 0;
            Debug.Log($"Puzzle 3 (link): {pairCount} parejas");

            string pin = result.puzzle4_console?.pin ?? "?";
            Debug.Log($"Puzzle 4 (console): PIN={pin}");

            FindObjectOfType<RoomFlowManager>()?.InitializeRoom(result);
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>TEST FALLIDO:</color> {e.Message}\n{e.StackTrace}");
        }
    }
}
