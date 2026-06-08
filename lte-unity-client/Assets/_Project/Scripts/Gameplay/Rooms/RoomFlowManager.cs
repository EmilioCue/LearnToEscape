using System;
using LearnToEscape.Content;
using LearnToEscape.Core;
using LearnToEscape.Gameplay.Puzzles;
using LearnToEscape.Puzzles;
using UnityEngine;

namespace LearnToEscape.Gameplay.Rooms
{
    /// <summary>
    /// Orquesta el flujo paralelo de los 4 puzles de una sala con una barrera de
    /// sincronización: recibe los controladores ya instanciados por el generador
    /// PCG, activa los 4 simultáneamente y, cuando los 3 subsistemas (P1, P2, P3)
    /// quedan resueltos, desbloquea la consola (P4) llamando a
    /// <see cref="ConsolePuzzleController.PowerUpConsole"/>. La sala se completa
    /// en cuanto P4 emite <see cref="IPuzzleController.OnPuzzleSolved"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El ciclo de vida esperado es:
    /// <list type="number">
    ///   <item><c>Awake</c> — el singleton queda disponible.</item>
    ///   <item><see cref="SetPuzzlesAndInitialize"/> — el generador PCG instancia
    ///   los prefabs y entrega sus controladores; este método completa la
    ///   inicialización de la sala en el mismo frame.</item>
    /// </list>
    /// No existe auto-inicialización en <c>Start</c>: la sala siempre se
    /// configura mediante <see cref="SetPuzzlesAndInitialize"/>.
    /// </para>
    /// <para>
    /// Expuesto como Singleton <see cref="Instance"/> para que cualquier sistema
    /// (UI, audio, guardado) pueda consultarlo sin acoplamiento directo.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class RoomFlowManager : MonoBehaviour
    {
        private const int PuzzleCount = 4;
        private const int FinalPuzzleIndex = PuzzleCount - 1;

        public static RoomFlowManager Instance { get; private set; }

        /// <summary>Se dispara cuando el Puzle 4 (Console) es resuelto.</summary>
        public event Action OnRoomCompleted;

        /// <summary>Se dispara cuando se activa un puzle (índice 0..3). Útil para UI/HUD.</summary>
        public event Action<int> OnPuzzleActivated;

        /// <summary>Se dispara cuando cualquier puzle es resuelto (índice 0..3). Útil para UI/audio.</summary>
        public event Action<int> OnPuzzleSolvedFeedback;

        private IPuzzleController[] _controllers;
        private Action[] _solvedHandlers;
        private bool _isInitialized;
        private int _solvedSubsystems = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"[RoomFlowManager] Ya existe una instancia en {Instance.name}; " +
                    $"destruyendo duplicado en {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                Debug.LogWarning(
                    "[RoomFlowManager] Start() alcanzado sin haber llamado a " +
                    "SetPuzzlesAndInitialize(). Asegúrate de que TGridGenerator " +
                    "llame a este método antes de que termine su propio Start().", this);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            if (Instance == this) Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // API pública
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recibe los 4 controladores ya instanciados por el generador PCG,
        /// inyecta los datos de <see cref="GameSession.CurrentRoom"/> en cada uno
        /// y los activa todos de forma simultánea.
        /// </summary>
        /// <remarks>
        /// Debe llamarse una única vez, desde el <c>Start()</c> de
        /// <c>TGridGenerator</c> (que corre después del <c>Awake</c> de este
        /// componente, garantizando que <see cref="Instance"/> ya existe).
        /// </remarks>
        /// <param name="puzzle1">Controlador del Puzle 1 (Matrix).</param>
        /// <param name="puzzle2">Controlador del Puzle 2 (Router).</param>
        /// <param name="puzzle3">Controlador del Puzle 3 (Link).</param>
        /// <param name="puzzle4">Controlador del Puzle 4 (Console) — condición de victoria.</param>
        public void SetPuzzlesAndInitialize(
            IPuzzleController puzzle1,
            IPuzzleController puzzle2,
            IPuzzleController puzzle3,
            IPuzzleController puzzle4)
        {
            if (_isInitialized)
            {
                Debug.LogWarning(
                    "[RoomFlowManager] SetPuzzlesAndInitialize llamado dos veces; se ignora.", this);
                return;
            }

            if (!ValidateController(puzzle1, 1)) return;
            if (!ValidateController(puzzle2, 2)) return;
            if (!ValidateController(puzzle3, 3)) return;
            if (!ValidateController(puzzle4, 4)) return;

            // Guardamos las referencias y suscribimos los eventos ANTES de validar
            // GameSession: así en modo testing (sin datos de red) los SimulateSolve()
            // de cada puzle siguen llegando a HandlePuzzleSolved correctamente.
            _controllers = new[] { puzzle1, puzzle2, puzzle3, puzzle4 };

            _solvedHandlers = new Action[_controllers.Length];
            for (int i = 0; i < _controllers.Length; i++)
            {
                int capturedIndex = i;
                _solvedHandlers[i] = () => HandlePuzzleSolved(capturedIndex);
                _controllers[i].OnPuzzleSolved += _solvedHandlers[i];
            }

            RoomData data = GameSession.CurrentRoom;
            if (data == null)
            {
                Debug.LogWarning(
                    "[RoomFlowManager] GameSession.CurrentRoom es null (modo testing). " +
                    "Eventos suscritos; InjectData y ActivatePuzzle omitidos.", this);
                return;
            }

            _controllers[0].InjectData(data.puzzle1_matrix);
            _controllers[1].InjectData(data.puzzle2_router);
            _controllers[2].InjectData(data.puzzle3_link);
            _controllers[3].InjectData(data.puzzle4_console);

            _isInitialized = true;

            for (int i = 0; i < _controllers.Length; i++)
            {
                _controllers[i].ActivatePuzzle();
                OnPuzzleActivated?.Invoke(i);
            }

            Debug.Log("[RoomFlowManager] Sala inicializada con 4 puzles en modo paralelo.", this);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lógica interna
        // ─────────────────────────────────────────────────────────────────────

        private void HandlePuzzleSolved(int index)
        {
            Debug.Log($"[RoomFlowManager] Puzle {index + 1} resuelto.", this);
            OnPuzzleSolvedFeedback?.Invoke(index);

            if (index == FinalPuzzleIndex)
            {
                OnRoomCompleted?.Invoke();
                return;
            }

            _solvedSubsystems++;
            Debug.Log(
                $"[RoomFlowManager] Subsistemas resueltos: {_solvedSubsystems} / {FinalPuzzleIndex}.",
                this);

            if (_solvedSubsystems == FinalPuzzleIndex)
            {
                if (_controllers[FinalPuzzleIndex] is ConsolePuzzleController console)
                {
                    Debug.Log("[RoomFlowManager] Barrera de sincronización completada → PowerUpConsole.", this);
                    console.PowerUpConsole();
                }
                else
                {
                    Debug.LogError(
                        "[RoomFlowManager] El controlador del Puzle 4 no es un " +
                        "ConsolePuzzleController; no se puede encender la consola.", this);
                }
            }
        }

        private void UnsubscribeAll()
        {
            if (_controllers == null || _solvedHandlers == null) return;
            for (int i = 0; i < _controllers.Length; i++)
            {
                if (_solvedHandlers[i] != null)
                    _controllers[i].OnPuzzleSolved -= _solvedHandlers[i];
            }
        }

        private static bool ValidateController(IPuzzleController controller, int slot)
        {
            if (controller != null) return true;

            Debug.LogError(
                $"[RoomFlowManager] El controlador del Puzle {slot} recibido en " +
                "SetPuzzlesAndInitialize es null.");
            return false;
        }
    }
}
