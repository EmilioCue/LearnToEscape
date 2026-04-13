using UnityEngine;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Base de conocimiento factual para un tema educativo.
    /// El <see cref="ContentGenerator"/> inyecta <see cref="FactualContext"/>
    /// en el prompt del LLM para anclar las respuestas a datos verificados
    /// y reducir alucinaciones.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Topic Knowledge Base",
        menuName = "LearnToEscape/Topic Knowledge Base")]
    public class TopicKnowledgeBase : ScriptableObject
    {
        [Tooltip("Nombre corto del tema (se usa como etiqueta en logs y como temática de la sala).")]
        public string topicName;

        [Tooltip("Texto factual verificado que se inyecta literalmente en el prompt. " +
                 "El LLM solo puede usar esta información para generar preguntas y explicaciones.")]
        [TextArea(10, 50)]
        public string factualContext;

        /// <summary>Returns true when both fields contain usable text.</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(topicName) &&
            !string.IsNullOrWhiteSpace(factualContext);
    }
}
