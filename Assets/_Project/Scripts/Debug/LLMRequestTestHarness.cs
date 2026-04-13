using System;
using System.Threading.Tasks;
using LearnToEscape.LLM;
using UnityEngine;

namespace LearnToEscape.Debugging
{
    /// <summary>
    /// Dispara <see cref="LLMClient.SendRequest"/> para pruebas (Play Mode).
    /// Puede ejecutarse al inicio o desde el menú contextual del Inspector.
    /// </summary>
    public class LLMRequestTestHarness : MonoBehaviour
    {
        [SerializeField] private LLMClient _llmClient;
        [TextArea(2, 5)]
        [SerializeField] private string _testPrompt = "Responde en una sola frase en español: ¿qué es un escape room?";
        [SerializeField] private bool _sendOnStart;

        private void Awake()
        {
            if (_llmClient == null)
                _llmClient = GetComponent<LLMClient>();
        }

        private async void Start()
        {
            if (_sendOnStart)
                await SendTestAsync();
        }

        [ContextMenu("Probar LLM (test prompt)")]
        private void ContextMenuRunTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LLM test] Entra en Play Mode y vuelve a usar el menú contextual.", this);
                return;
            }

            _ = RunTestFireAndForget();
        }

        /// <summary>
        /// Enlaza desde un botón UI (UnityEvent sin parámetros).
        /// </summary>
        public void SendTestFromButton()
        {
            _ = RunTestFireAndForget();
        }

        private async Task RunTestFireAndForget()
        {
            try
            {
                await SendTestAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LLM test] Error: {ex.Message}\n{ex}", this);
            }
        }

        private async Task SendTestAsync()
        {
            if (_llmClient == null)
            {
                Debug.LogError("[LLM test] Falta LLMClient (mismo GameObject o arrastra en el Inspector).", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(_testPrompt))
            {
                Debug.LogError("[LLM test] El prompt de prueba está vacío.", this);
                return;
            }

            Debug.Log("[LLM test] Enviando petición…", this);
            var raw = await _llmClient.SendRequest(_testPrompt);
            Debug.Log($"[LLM test] SendRequest completado (longitud respuesta: {raw?.Length ?? 0}).", this);
            Debug.Log($"Respuesta del LLM: {raw}");
        }
    }
}
