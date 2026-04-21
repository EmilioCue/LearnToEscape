using UnityEngine;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Identificador del tema educativo que se envía al backend.
    /// La base de conocimiento factual vive íntegramente en el servicio Java:
    /// Unity solo propaga el <see cref="topicName"/> para que el backend
    /// seleccione el contexto canónico correspondiente.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Topic Knowledge Base",
        menuName = "LearnToEscape/Topic Knowledge Base")]
    public class TopicKnowledgeBase : ScriptableObject
    {
        [Tooltip("Nombre corto del tema (se usa como etiqueta en logs y como temática de la sala).")]
        public string topicName;

        /// <summary>Returns true when the topic name contains usable text.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(topicName);
    }
}
