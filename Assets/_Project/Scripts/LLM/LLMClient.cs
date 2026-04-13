using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LearnToEscape.Core.Events;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LearnToEscape.LLM
{
    /// <summary>
    /// HTTP client for a local LLM (Ollama-compatible POST /api/generate).
    /// Uses <see cref="UnityWebRequest"/> with a streaming <see cref="DownloadHandlerScript"/>
    /// to avoid Mono's HttpClient buffering issues on slow hardware.
    /// Assign an <see cref="LLMConfig"/> in the Inspector.
    /// </summary>
    public class LLMClient : MonoBehaviour
    {
        private const int MinTimeoutSeconds = 30;
        private const int MaxTimeoutSeconds = 600;

        [SerializeField] private LLMConfig _config;
        [SerializeField] private StringGameEvent _onLlmRawResponse;

        [Header("Debug")]
        [Tooltip("Si está activo, escribe en la consola el texto completo generado por el modelo (JSON de la sala/puzzle).")]
        [SerializeField] private bool _logModelOutputToConsole;

        /// <summary>
        /// Sends <paramref name="prompt"/> to the configured Ollama endpoint using streaming.
        /// The idle timeout (no new tokens) is taken from <see cref="LLMConfig.RequestTimeoutSeconds"/>.
        /// During initial prompt evaluation (before the first token) the limit is doubled
        /// to accommodate slow CPU-only inference.
        /// </summary>
        public async Task<string> SendRequest(
            string prompt,
            string systemPrompt = null,
            bool forceJson = false,
            CancellationToken ct = default)
        {
            if (_config == null)
                throw new InvalidOperationException(
                    "LLMClient requires an LLMConfig assigned in the Inspector.");

            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            float idleTimeout = Mathf.Clamp(
                _config.RequestTimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds);

            var requestJson = BuildOllamaGenerateJson(prompt, systemPrompt, forceJson);
            var bodyBytes = Encoding.UTF8.GetBytes(requestJson);

            using var request = new UnityWebRequest(_config.Endpoint, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.uploadHandler.contentType = "application/json";
            request.timeout = 0;

            var streamHandler = new OllamaStreamingHandler();
            request.downloadHandler = streamHandler;

            float totalTimeout = _config.MaxTotalRequestSeconds;

            Debug.Log($"[LLMClient] Sending request to {_config.Endpoint} " +
                      $"(model: {_config.ModelName}, idle: {idleTimeout}s, " +
                      $"total: {(totalTimeout > 0 ? $"{totalTimeout}s" : "unlimited")})…");

            var operation = request.SendWebRequest();

            // Wall-clock (Stopwatch): Time.realtimeSinceStartup freezes when the Editor is paused,
            // which would stall idle/total timeouts and skew heartbeat seconds.
            var wallClock = System.Diagnostics.Stopwatch.StartNew();
            double lastProgressWallSeconds = 0;
            double lastHeartbeatWallSeconds = 0;
            int lastAssembledLength = 0;
            int lastRawBytesSnapshot = 0;
            const double HeartbeatIntervalSeconds = 30d;

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }

#if UNITY_EDITOR
                // Async continuations can resume during script compile / editor ticks after Stop Play;
                // abort so heartbeats and network work do not continue in the background.
                if (!Application.isPlaying)
                {
                    request.Abort();
                    throw new OperationCanceledException(
                        "Play Mode ended — Ollama request was aborted (async must not outlive Play Mode).");
                }
#endif
                if (!this)
                {
                    request.Abort();
                    throw new OperationCanceledException(
                        "LLMClient was destroyed — Ollama request was aborted.");
                }

                double totalElapsed = wallClock.Elapsed.TotalSeconds;

                if (totalTimeout > 0 && totalElapsed > totalTimeout)
                {
                    request.Abort();
                    throw new TimeoutException(
                        $"LLM request exceeded total time limit ({totalTimeout}s). " +
                        $"{streamHandler.ChunkCount} chunks / {streamHandler.AssembledLength} chars " +
                        $"received. Increase MaxTotalRequestSeconds or use a faster model.");
                }

                int rawBytes = streamHandler.RawBytesReceived;
                if (rawBytes != lastRawBytesSnapshot)
                {
                    lastRawBytesSnapshot = rawBytes;
                    lastProgressWallSeconds = totalElapsed;
                }

                int currentLength = streamHandler.AssembledLength;
                if (currentLength != lastAssembledLength)
                {
                    lastAssembledLength = currentLength;
                    lastProgressWallSeconds = totalElapsed;
                }
                else
                {
                    bool hasParsedTokens = lastAssembledLength > 0;
                    float effectiveTimeout = hasParsedTokens ? idleTimeout : idleTimeout * 2f;
                    float idleElapsed = (float)(totalElapsed - lastProgressWallSeconds);

                    if (idleElapsed > effectiveTimeout)
                    {
                        request.Abort();
                        throw new TimeoutException(hasParsedTokens
                            ? $"LLM stopped producing tokens for {effectiveTimeout}s " +
                              $"({lastAssembledLength} chars received before stalling)."
                            : rawBytes == 0
                                ? $"No response bytes from Ollama within {effectiveTimeout}s " +
                                  "(model loading / prompt eval is very slow, or Ollama is not running). " +
                                  "Try: smaller model, `ollama run <model>` once to warm cache, " +
                                  "or raise RequestTimeoutSeconds in LLMConfig."
                                : $"Received {rawBytes} raw bytes but no complete JSON line/token in " +
                                  $"{effectiveTimeout}s. Check endpoint and Ollama version.");
                    }
                }

                if (totalElapsed - lastHeartbeatWallSeconds >= HeartbeatIntervalSeconds)
                {
                    lastHeartbeatWallSeconds = totalElapsed;
                    string phase = rawBytes == 0
                        ? "waiting for first byte (prompt eval / load model — normal on slow CPU)"
                        : lastAssembledLength == 0
                            ? "bytes arriving, no full line yet"
                            : "streaming tokens";
                    Debug.Log($"[LLMClient] ⏳ {totalElapsed:F0}s (wall) — {phase} | " +
                              $"{streamHandler.ChunkCount} chunks, {streamHandler.AssembledLength} chars, " +
                              $"{rawBytes} raw bytes");
                }

                await Task.Yield();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                throw new OperationCanceledException(
                    "Play Mode ended before the Ollama stream finished.");
#endif
            if (!this)
                throw new OperationCanceledException(
                    "LLMClient was destroyed before the Ollama stream finished.");

            if (request.result != UnityWebRequest.Result.Success)
            {
                string detail = request.result switch
                {
                    UnityWebRequest.Result.ConnectionError =>
                        $"Cannot connect to Ollama at {_config.Endpoint}. Is it running?",
                    UnityWebRequest.Result.ProtocolError =>
                        $"HTTP {request.responseCode}: {request.error}",
                    _ => request.error
                };
                throw new InvalidOperationException($"LLM request failed — {detail}");
            }

            var assembledText = streamHandler.GetAssembledText();
            var body = BuildSyntheticOllamaBody(assembledText);

            Debug.Log($"[LLMClient] Streaming complete — {streamHandler.ChunkCount} chunks, " +
                      $"{streamHandler.AssembledLength} chars.");

            if (_logModelOutputToConsole)
                Debug.Log($"[LLMClient] Salida del modelo (texto ensamblado):\n{assembledText}");

            _onLlmRawResponse?.Raise(body);
            return body;
        }

        /// <summary>
        /// Wraps the assembled token text into the same JSON envelope Ollama
        /// returns with <c>stream:false</c>, so <see cref="LLMResponseParser"/>
        /// works without changes.
        /// </summary>
        private string BuildSyntheticOllamaBody(string assembledText)
        {
            var escaped = JsonEscape(assembledText);
            return $"{{\"response\":\"{escaped}\",\"done\":true}}";
        }

        private string BuildOllamaGenerateJson(string prompt, string systemPrompt, bool forceJson)
        {
            var model = JsonEscape(_config.ModelName);
            var escapedPrompt = JsonEscape(prompt);
            var temperature = _config.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var topP = _config.TopP.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var numPredict = _config.MaxTokens.ToString();

            var sb = new StringBuilder(512);
            sb.Append("{\"model\":\"").Append(model)
              .Append("\",\"prompt\":\"").Append(escapedPrompt).Append('"');

            if (!string.IsNullOrEmpty(systemPrompt))
                sb.Append(",\"system\":\"").Append(JsonEscape(systemPrompt)).Append('"');

            if (forceJson)
                sb.Append(",\"format\":\"json\"");

            sb.Append(",\"stream\":true,\"options\":{\"temperature\":")
              .Append(temperature)
              .Append(",\"top_p\":").Append(topP)
              .Append(",\"num_predict\":").Append(numPredict)
              .Append("}}");

            return sb.ToString();
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Processes Ollama's newline-delimited streaming JSON in real-time.
        /// <see cref="DownloadHandlerScript.ReceiveData"/> is called by Unity's
        /// native network layer each time a chunk of bytes arrives, bypassing
        /// Mono's HttpClient buffering entirely.
        /// </summary>
        private class OllamaStreamingHandler : DownloadHandlerScript
        {
            private readonly StringBuilder _assembled = new(4096);
            private readonly StringBuilder _lineBuffer = new(512);

            private int _rawBytesReceived;

            public int AssembledLength => _assembled.Length;
            public int ChunkCount { get; private set; }
            public bool StreamDone { get; private set; }

            /// <summary>Bytes received on the wire (before line/token parsing). Thread-safe.</summary>
            public int RawBytesReceived => _rawBytesReceived;

            public OllamaStreamingHandler() : base(new byte[4096]) { }

            public string GetAssembledText() => _assembled.ToString();

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength == 0) return false;

                Interlocked.Add(ref _rawBytesReceived, dataLength);

                _lineBuffer.Append(Encoding.UTF8.GetString(data, 0, dataLength));

                var accumulated = _lineBuffer.ToString();
                int lastNewline = accumulated.LastIndexOf('\n');
                if (lastNewline < 0) return true;

                var completeText = accumulated.Substring(0, lastNewline);
                _lineBuffer.Clear();
                if (lastNewline + 1 < accumulated.Length)
                    _lineBuffer.Append(accumulated.Substring(lastNewline + 1));

                foreach (var rawLine in completeText.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    try
                    {
                        var chunk = JObject.Parse(line);
                        var token = chunk["response"]?.Value<string>();
                        if (token != null)
                        {
                            _assembled.Append(token);
                            ChunkCount++;
                        }

                        if (chunk["done"]?.Value<bool>() == true)
                            StreamDone = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LLMClient] Malformed streaming chunk: {ex.Message}");
                    }
                }

                return true;
            }

            protected override float GetProgress() => StreamDone ? 1f : 0f;
        }
    }
}
