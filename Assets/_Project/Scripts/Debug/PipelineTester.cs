using System;
using System.Threading;
using LearnToEscape.Content;
using UnityEngine;

public class PipelineTester : MonoBehaviour
{
    [Header("Dependencias")]
    public ContentGenerator generator;

    [Header("Parámetros de Prueba")]
    public TopicKnowledgeBase knowledgeBase;
    public int puzzleCount = 0;
    public string difficulty = "easy";

    [Header("Cancelación (opcional)")]
    [Tooltip("0 = sin límite de tiempo total. Un valor > 0 cancela tras N segundos de reloj. " +
             "Ojo: el LLMClient ya corta si no llegan tokens (idle, ver LLMConfig); " +
             "un límite global aquí puede fallar en CPU lenta con prompts largos.")]
    [Min(0)]
    public int totalTimeoutSeconds;

    [ContextMenu("🚨 EJECUTAR TEST DE PIPELINE")]
    public async void RunPipelineTest()
    {
        string topicLabel = knowledgeBase != null ? knowledgeBase.topicName : "(sin asignar)";
        Debug.Log($"<color=cyan>[Tester]</color> Iniciando petición a Ollama. Tema: {topicLabel}...");

        CancellationTokenSource cts = null;
        if (totalTimeoutSeconds > 0)
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(totalTimeoutSeconds));

        var token = cts?.Token ?? CancellationToken.None;

        try
        {
            RoomData result = await generator.GenerateRoom(knowledgeBase, puzzleCount, difficulty, token);
            
            Debug.Log($"<color=green>¡ÉXITO ABSOLUTO!</color> Sala instanciada en memoria.");
            Debug.Log($"Nombre de la sala: {result.name}");
            Debug.Log($"Puzzles recibidos: {result.puzzles.Length}");
            
            if(result.puzzles.Length > 0) 
            {
                for(int i = 0; i < result.puzzles.Length; i++)
                {
                    Debug.Log($"Puzzle {i+1}: [{result.puzzles[i].type}] - Solución esperada: {result.puzzles[i].solution}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogError(
                "<color=red>TEST FALLIDO:</color> Operación cancelada. " +
                (totalTimeoutSeconds > 0
                    ? $"Se alcanzó totalTimeoutSeconds ({totalTimeoutSeconds} s). " +
                      "Pon 0 en el Inspector para no limitar el tiempo total, o sube el valor."
                    : "Revisa si saliste de Play Mode o si otro sistema canceló el token."));
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red>TEST FALLIDO:</color> {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            cts?.Dispose();
        }
    }
}