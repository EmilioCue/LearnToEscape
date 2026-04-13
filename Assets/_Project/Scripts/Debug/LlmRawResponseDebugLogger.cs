using UnityEngine;

namespace LearnToEscape.Debugging
{
    /// <summary>
    /// Temporal: asignar como destino del <see cref="StringUnityEvent"/> del
    /// <see cref="Core.Events.StringGameEventListener"/> para imprimir la respuesta bruta del LLM.
    /// </summary>
    public class LlmRawResponseDebugLogger : MonoBehaviour
    {
        public void LogRawResponse(string raw)
        {
            Debug.Log($"[LLM raw response]\n{raw}", this);
        }
    }
}
