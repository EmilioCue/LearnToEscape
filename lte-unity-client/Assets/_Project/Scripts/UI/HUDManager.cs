using TMPro;
using UnityEngine;

namespace LearnToEscape.UI
{
    /// <summary>
    /// Gestiona el HUD 2D de la sala de escape: expone métodos para mostrar y
    /// ocultar el panel de texto contextual que los puzles activan por proximidad.
    /// </summary>
    /// <remarks>
    /// Singleton de escena: existe una única instancia por escena de juego y la
    /// expone a través de <see cref="Instance"/> para que cualquier controlador de
    /// puzle pueda llamarla sin referencia directa serializada.
    /// El panel se desactiva en <c>Awake</c> para garantizar que empiece oculto
    /// independientemente del estado guardado en el prefab.
    /// </remarks>
    [DisallowMultipleComponent]
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("Panel de contexto")]
        [Tooltip("GameObject raíz del panel que contiene la pregunta contextual.")]
        [SerializeField] private GameObject questionPanel;

        [Tooltip("TMP_Text donde se escribe el texto contextual enviado por los puzles.")]
        [SerializeField] private TMP_Text questionText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[HUDManager] Ya existe una instancia en '{Instance.name}'; " +
                    $"destruyendo duplicado en '{name}'.", this);
                Destroy(this);
                return;
            }

            Instance = this;

            if (questionPanel != null)
                questionPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Asigna <paramref name="text"/> al panel de contexto y lo hace visible.
        /// </summary>
        /// <param name="text">Texto a mostrar (p. ej. la pregunta de deducción del LLM).</param>
        public void ShowContextText(string text)
        {
            if (questionText != null)
                questionText.text = text ?? string.Empty;
            else
                Debug.LogWarning(
                    "[HUDManager] questionText no está asignado en el Inspector.", this);

            if (questionPanel != null)
                questionPanel.SetActive(true);
            else
                Debug.LogWarning(
                    "[HUDManager] questionPanel no está asignado en el Inspector.", this);
        }

        /// <summary>
        /// Oculta el panel de contexto.
        /// </summary>
        public void HideContextText()
        {
            if (questionPanel != null)
                questionPanel.SetActive(false);
        }
    }
}
