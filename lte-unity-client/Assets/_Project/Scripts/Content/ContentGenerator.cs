using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Solicita salas generadas al backend Java vía HTTP; opcionalmente puede usar JSON simulado (dry-run).
    /// </summary>
    public class ContentGenerator : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private string backendUrl = "http://localhost:8080/api/rooms/generate";

        [Header("Prueba en seco (sin servidor Java)")]
        [Tooltip("Si está activo, no se llama al backend y se deserializa un RoomData de ejemplo.")]
        [SerializeField] private bool useDryRunDummyJson = true;

        /// <summary>
        /// Adaptador legacy que acepta un <see cref="TopicKnowledgeBase"/>
        /// (utilizado por <c>PipelineTester</c>) y delega en la sobrecarga
        /// principal basada en <c>string</c>.
        /// </summary>
        public Task<RoomData> GenerateRoom(
            TopicKnowledgeBase knowledgeBase,
            int puzzleCount,
            string difficulty)
        {
            if (knowledgeBase == null || !knowledgeBase.IsValid)
                throw new System.ArgumentException(
                    "TopicKnowledgeBase is null or has an empty topicName. " +
                    "Assign a valid asset in the Inspector.", nameof(knowledgeBase));

            return GenerateRoom(knowledgeBase.topicName, puzzleCount, difficulty);
        }

        /// <summary>
        /// Solicita una sala al backend a partir de un tema textual ya
        /// resuelto (p. ej. el seleccionado en el menú principal).
        /// El backend se encarga de normalizar tildes y mayúsculas.
        /// </summary>
        public async Task<RoomData> GenerateRoom(
            string topic,
            int puzzleCount,
            string difficulty)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new System.ArgumentException(
                    "topic is null or empty.", nameof(topic));

            if (useDryRunDummyJson)
            {
                const string dummyJsonResponse =
                    "{" +
                    "\"theme\":\"Redes\"," +
                    "\"puzzle1_matrix\":{" +
                        "\"categories\":[\"TCP\",\"UDP\"]," +
                        "\"items\":[" +
                            "{\"name\":\"Orientado a conexión\",\"categoryIndex\":0}," +
                            "{\"name\":\"Sin confirmación\",\"categoryIndex\":1}" +
                        "]" +
                    "}," +
                    "\"puzzle2_router\":{\"sequence\":[\"Paso 1\",\"Paso 2\",\"Paso 3\",\"Paso 4\",\"Paso 5\"]}," +
                    "\"puzzle3_link\":{\"pairs\":[" +
                        "{\"concept\":\"IP\",\"definition\":\"Identificador de red\"}," +
                        "{\"concept\":\"MAC\",\"definition\":\"Identificador físico\"}" +
                    "]}," +
                    "\"puzzle4_console\":{" +
                        "\"deductionQuestion\":\"¿Cuál es el PIN?\"," +
                        "\"stepByStepReasoning\":\"Dummy: PIN fijo de pruebas 1234.\"," +
                        "\"pin\":\"1234\"" +
                    "}" +
                    "}";

                var roomDry = JsonConvert.DeserializeObject<RoomData>(dummyJsonResponse);
                var dryErrors = ContentValidator.ValidateRoom(roomDry);
                if (dryErrors.Count > 0)
                {
                    Debug.LogError("[ContentGenerator] Dry-run inválido: " + string.Join(", ", dryErrors));
                    return null;
                }

                Debug.Log("[ContentGenerator] Dry-run: RoomData cargado sin llamar al backend.");
                return roomDry;
            }

            var requestPayload = new
            {
                topic = topic,
                puzzleCount = puzzleCount,
                difficulty = difficulty
            };

            string jsonPayload = JsonConvert.SerializeObject(requestPayload);

            using (var request = new UnityWebRequest(backendUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                    throw new System.Exception($"Error del Backend: {request.error}");

                string responseJson = request.downloadHandler.text;
                var room = JsonConvert.DeserializeObject<RoomData>(responseJson);

                var errors = ContentValidator.ValidateRoom(room);
                if (errors.Count > 0)
                {
                    Debug.LogError("[ContentGenerator] Java envió datos inválidos: " + string.Join(", ", errors));
                    return null;
                }

                return room;
            }
        }
    }
}
