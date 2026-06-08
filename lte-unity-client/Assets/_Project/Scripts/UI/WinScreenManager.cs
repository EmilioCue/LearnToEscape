using LearnToEscape.Gameplay.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LearnToEscape.UI
{
    /// <summary>
    /// Pantalla de victoria: escucha <see cref="RoomFlowManager.OnRoomCompleted"/>
    /// y muestra el panel ganador. Su botón "Reiniciar" carga explícitamente
    /// la escena del menú principal, limpiando el estado de la sala en curso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El botón de reinicio carga <c>SCN_MainMenu</c> por nombre, garantizando
    /// que el jugador vuelve al menú aunque la escena de juego no sea la de
    /// inicio del build.
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

        [Tooltip("Botón que carga SCN_MainMenu para volver al menú.")]
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
        /// Carga la escena del menú principal, destruyendo la sala actual.
        /// </summary>
        private void HandleRestartClicked()
        {
            Debug.Log(
                $"[{nameof(WinScreenManager)}] Volviendo al menú principal (SCN_MainMenu).",
                this);
            SceneManager.LoadScene("SCN_MainMenu");
        }
    }
}
