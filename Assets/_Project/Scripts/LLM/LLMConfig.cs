using UnityEngine;

namespace LearnToEscape.LLM
{
    [CreateAssetMenu(menuName = "LearnToEscape/LLM/Config")]
    public class LLMConfig : ScriptableObject
    {
        [Header("Connection")]
        [SerializeField] private string _endpoint = "http://localhost:11434/api/generate";
        [SerializeField] private string _modelName = "llama3.1:8b-instruct-q4_K_M";

        [Header("Generation Parameters")]
        [SerializeField, Range(0f, 2f)] private float _temperature = 0.1f;
        [SerializeField, Range(1, 4096)] private int _maxTokens = 2048;
        [SerializeField, Range(0f, 1f)] private float _topP = 0.9f;

        [Header("Timeouts")]
        [Tooltip("Idle: si no llegan tokens nuevos en este intervalo, la petición se aborta (30–600 s).")]
        [SerializeField, Range(30, 600)] private int _requestTimeoutSeconds = 120;

        [Tooltip("Tiempo TOTAL máximo por petición en segundos de reloj (prompt eval + generación). " +
                 "0 = sin límite total (solo idle). En CPU lenta, 300 puede ser poco — sube a 600–900 o 0.")]
        [SerializeField, Min(0)] private int _maxTotalRequestSeconds = 600;

        public string Endpoint => _endpoint;
        public string ModelName => _modelName;
        public float Temperature => _temperature;
        public int MaxTokens => _maxTokens;
        public float TopP => _topP;
        public int RequestTimeoutSeconds => _requestTimeoutSeconds;
        public int MaxTotalRequestSeconds => _maxTotalRequestSeconds;
    }
}
