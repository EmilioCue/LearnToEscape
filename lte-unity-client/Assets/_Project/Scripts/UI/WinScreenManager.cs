using LearnToEscape.Gameplay.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LearnToEscape.UI
{
    /// <summary>
    /// Pantalla de victoria: escucha <see cref="RoomFlowManager.OnRoomCompleted"/>
    /// y muestra el panel ganador. Su botón "Reiniciar" recarga la escena
    /// activa, lo que limpia TODO el estado en memoria (sala generada,
    /// progreso de puzzles, posición de cámara…) y devuelve al jugador al
    /// Menú Principal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La estrategia de "reset por recarga de escena" es deliberada: en este
    /// proyecto cada sala es contenido procedural servido por el LLM, así que
    /// no tiene sentido mantener objetos antiguos vivos. Recargar el
    /// <c>buildIndex</c> actual es la forma más simple y robusta de garantizar
    /// estado limpio sin tener que escribir un sistema de "rewind" manual.
    /// </para>
    /// <para>
    /// La suscripción a <c>OnRoomCompleted</c> se hace en <see cref="Start"/>
    /// (no en <c>Awake</c>) para asegurar que <see cref="RoomFlowManager"/>
    /// ya pasó por su propio <c>Awake</c> y existe la <c>Instance</c>. La
    /// desuscripción en <see cref="OnDestroy"/> evita listeners colgados
    /// hacia un componente que está a punto de desaparecer durante la
    /// recarga de escena.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class WinScreenManager : MonoBehaviour
    {
        [Header("Dependencias")]
        [Tooltip("Manager del flujo de puzles. Si se deja vacío, se intenta " +
                 "resolver vía RoomFlowManager.Instance en Start().")]
        [SerializeField] private RoomFlowManager roomManager;

        [Header("UI")]
        [Tooltip("Panel raíz de la pantalla de victoria. Se activa al ganar.")]
        [SerializeField] private GameObject winPanel;

        [Tooltip("Botón que recarga la escena activa para volver al menú.")]
        [SerializeField] private Button restartButton;

        [Tooltip("Texto opcional dentro del winPanel; si se asigna, se " +
                 "rellena con el mensaje de victoria al ganar.")]
        [SerializeField] private TMP_Text victoryMessage;

        [Tooltip("Mensaje que se muestra en victoryMessage al ganar.")]
        [SerializeField] private string victoryMessageText = "¡Sala completada!";

        private bool _isSubscribed;

        private void Start()
        {
            if (winPanel != null) winPanel.SetActive(false);

            if (roomManager == null) roomManager = RoomFlowManager.Instance;
            if (roomManager == null)
            {
                Debug.LogError(
                    $"[{nameof(WinScreenManager)}] No se encontró RoomFlowManager " +
                    "ni en el Inspector ni como Singleton. La pantalla de victoria " +
                    "no se activará nunca.", this);
                return;
            }

            roomManager.OnRoomCompleted += HandleRoomCompleted;
            _isSubscribed = true;

            if (restartButton != null)
                restartButton.onClick.AddListener(HandleRestartClicked);
            else
                Debug.LogWarning(
                    $"[{nameof(WinScreenManager)}] restartButton no asignado; " +
                    "el jugador no podrá reiniciar tras ganar.", this);
        }

        private void OnDestroy()
        {
            if (_isSubscribed && roomManager != null)
                roomManager.OnRoomCompleted -= HandleRoomCompleted;

            if (restartButton != null)
                restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        private void HandleRoomCompleted()
        {
            Debug.Log(
                $"[{nameof(WinScreenManager)}] Sala completada → mostrando pantalla de victoria.",
                this);

            if (victoryMessage != null)
                victoryMessage.text = victoryMessageText;

            if (winPanel != null)
            {
                winPanel.SetActive(true);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(WinScreenManager)}] winPanel no asignado; " +
                    "no se puede mostrar la pantalla de victoria.", this);
            }
        }

        /// <summary>
        /// Recarga la escena activa. Esto destruye toda la jerarquía actual
        /// (sala generada, puzles, UI in-game) y vuelve al estado inicial,
        /// que en esta build es el Menú Principal.
        /// </summary>
        private void HandleRestartClicked()
        {
            int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
            Debug.Log(
                $"[{nameof(WinScreenManager)}] Reiniciando escena (buildIndex={activeBuildIndex}).",
                this);
            SceneManager.LoadScene(activeBuildIndex);
        }
    }
}
