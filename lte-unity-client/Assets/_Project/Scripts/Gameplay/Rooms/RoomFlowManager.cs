using System;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Gameplay.Rooms
{
    /// <summary>
    /// Orquesta el flujo lineal de los 4 puzles de una sala: inyecta los datos
    /// a cada controlador, activa únicamente el puzle vigente y, al resolverse,
    /// encadena el siguiente hasta completar la sala.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity no serializa interfaces en el Inspector, por lo que los 4 slots se
    /// exponen como <see cref="MonoBehaviour"/>. <see cref="OnValidate"/> verifica
    /// en tiempo de edición que cada referencia implementa
    /// <see cref="IPuzzleController"/> para fallar pronto en el editor, no en build.
    /// </para>
    /// <para>
    /// Patrón de activación: se suscribe al evento del puzle <em>solo mientras es
    /// el activo</em> y se desuscribe en cuanto dispara. Esto impide que un
    /// <see cref="IPuzzleController.OnPuzzleSolved"/> duplicado avance el estado
    /// dos veces.
    /// </para>
    /// <para>
    /// Expuesto como Singleton <see cref="Instance"/> para que cualquier sistema
    /// (UI, audio, guardado) pueda consultarlo sin acoplamiento directo, pero
    /// igualmente puede ser referenciado como un <see cref="MonoBehaviour"/>
    /// ordinario colgado del GameManager.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class RoomFlowManager : MonoBehaviour
    {
        private const int PuzzleCount = 4;

        public static RoomFlowManager Instance { get; private set; }

        [Header("Puzzle Controllers (deben implementar IPuzzleController)")]
        [SerializeField] private MonoBehaviour puzzle1Controller;
        [SerializeField] private MonoBehaviour puzzle2Controller;
        [SerializeField] private MonoBehaviour puzzle3Controller;
        [SerializeField] private MonoBehaviour puzzle4Controller;

        /// <summary>Se dispara cuando el jugador resuelve el último puzle de la sala.</summary>
        public event Action OnRoomCompleted;

        /// <summary>Se dispara cuando se activa un puzle (índice 0..3). Útil para UI/HUD.</summary>
        public event Action<int> OnPuzzleActivated;

        private IPuzzleController[] _controllers;
        private int _currentIndex = -1;
        private bool _isInitialized;

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

        private void OnDestroy()
        {
            UnsubscribeCurrent();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Inyecta los datos de la sala en cada puzle y activa el primero.
        /// Debe llamarse una única vez por sala, tras cargar/generar el
        /// <see cref="RoomData"/>.
        /// </summary>
        /// <param name="data">Sala completa ya validada.</param>
        public void InitializeRoom(RoomData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (_isInitialized)
            {
                Debug.LogWarning("[RoomFlowManager] InitializeRoom llamado dos veces; se ignora.", this);
                return;
            }

            _controllers = ResolveControllers();
            if (_controllers == null) return;

            _controllers[0].InjectData(data.puzzle1_matrix);
            _controllers[1].InjectData(data.puzzle2_router);
            _controllers[2].InjectData(data.puzzle3_link);
            _controllers[3].InjectData(data.puzzle4_console);

            _isInitialized = true;
            ActivateStep(0);
        }

        private void ActivateStep(int index)
        {
            _currentIndex = index;
            var controller = _controllers[index];
            controller.OnPuzzleSolved += HandleCurrentPuzzleSolved;
            controller.ActivatePuzzle();
            OnPuzzleActivated?.Invoke(index);
        }

        private void HandleCurrentPuzzleSolved()
        {
            UnsubscribeCurrent();

            int next = _currentIndex + 1;
            if (next >= _controllers.Length)
            {
                _currentIndex = _controllers.Length;
                OnRoomCompleted?.Invoke();
                return;
            }

            ActivateStep(next);
        }

        private void UnsubscribeCurrent()
        {
            if (_controllers == null) return;
            if (_currentIndex < 0 || _currentIndex >= _controllers.Length) return;
            _controllers[_currentIndex].OnPuzzleSolved -= HandleCurrentPuzzleSolved;
        }

        /// <summary>
        /// Convierte los 4 slots serializados en un array de
        /// <see cref="IPuzzleController"/>. Devuelve <c>null</c> y loguea un error
        /// si algún slot está vacío o no implementa la interfaz.
        /// </summary>
        private IPuzzleController[] ResolveControllers()
        {
            var raw = new[]
            {
                puzzle1Controller,
                puzzle2Controller,
                puzzle3Controller,
                puzzle4Controller
            };

            var resolved = new IPuzzleController[PuzzleCount];
            for (int i = 0; i < PuzzleCount; i++)
            {
                if (raw[i] == null)
                {
                    Debug.LogError($"[RoomFlowManager] Slot puzzle{i + 1}Controller vacío.", this);
                    return null;
                }

                if (raw[i] is not IPuzzleController controller)
                {
                    Debug.LogError(
                        $"[RoomFlowManager] '{raw[i].GetType().Name}' asignado a " +
                        $"puzzle{i + 1}Controller no implementa IPuzzleController.", this);
                    return null;
                }

                resolved[i] = controller;
            }
            return resolved;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSlot(ref puzzle1Controller, 1);
            ValidateSlot(ref puzzle2Controller, 2);
            ValidateSlot(ref puzzle3Controller, 3);
            ValidateSlot(ref puzzle4Controller, 4);
        }

        private void ValidateSlot(ref MonoBehaviour slot, int index)
        {
            if (slot == null) return;
            if (slot is IPuzzleController) return;

            Debug.LogError(
                $"[RoomFlowManager] '{slot.GetType().Name}' no implementa IPuzzleController. " +
                $"Se limpia el slot puzzle{index}Controller.", this);
            slot = null;
        }
#endif
    }
}
