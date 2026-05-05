using System;
using System.Collections;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using TMPro;
using UnityEngine;
using ConsoleKey = LearnToEscape.Gameplay.Puzzles.ConsoleKey;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Controlador del puzle 4 (Console) en su versión jugable: muestra la
    /// pregunta de deducción, registra los clics de un teclado 3D de
    /// <see cref="ConsoleKey"/> y compara el PIN tecleado con el emitido por
    /// el backend, disparando <see cref="OnPuzzleSolved"/> al acertar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El controlador es la única fuente de verdad del input en curso
    /// (<see cref="_currentInput"/>); las teclas son meras vistas que envían
    /// su <c>keyValue</c> y delegan la interpretación aquí.
    /// </para>
    /// <para>
    /// El feedback de error se gestiona con una corrutina (<see cref="ShowErrorAndClear"/>)
    /// que bloquea nuevas pulsaciones mientras se muestra "ERROR", para evitar
    /// que el jugador acumule dígitos sobre el mensaje y para que el reset
    /// sea atómico desde su punto de vista.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class ConsolePuzzleController : MonoBehaviour, IPuzzleController
    {
        private const string ClearKey = "C";
        private const string EnterKey = "E";
        private const string ErrorMessage = "ERROR";
        private const int PinLength = 4;
        private const float ErrorDisplaySeconds = 1f;

        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Textos de escena (TextMeshPro)")]
        [Tooltip("Slot para mostrar la pregunta de deducción al jugador.")]
        [SerializeField] private TMP_Text questionText;

        [Tooltip("Display donde se muestra el PIN que va tecleando el jugador.")]
        [SerializeField] private TMP_Text inputDisplay;

        [Header("Teclado")]
        [Tooltip("Las 12 teclas del teclado 3D: \"0\"-\"9\", \"C\" (Clear) y \"E\" (Enter).")]
        [SerializeField] private ConsoleKey[] keys;

        [Header("Visual")]
        [Tooltip("Si está activo, el display muestra asteriscos en vez de los dígitos. " +
                 "Útil para producción; conviene dejarlo desactivado en greybox.")]
        [SerializeField] private bool maskInput = false;

        private Puzzle4Console _data;
        private bool _isActive;
        private bool _isSolved;
        private bool _isShowingError;

        private string _currentInput = string.Empty;
        private Coroutine _errorRoutine;

        /// <inheritdoc />
        public void InjectData(object puzzleData)
        {
            if (puzzleData is not Puzzle4Console typed)
            {
                Debug.LogError(
                    $"[{nameof(ConsolePuzzleController)}] Tipo inválido en InjectData: " +
                    $"se esperaba {nameof(Puzzle4Console)} y llegó " +
                    $"'{puzzleData?.GetType().Name ?? "null"}'.", this);
                return;
            }
            _data = typed;
        }

        /// <inheritdoc />
        public void ActivatePuzzle()
        {
            if (_data == null)
            {
                Debug.LogError(
                    $"[{nameof(ConsolePuzzleController)}] ActivatePuzzle llamado sin datos inyectados.",
                    this);
                return;
            }

            PopulateSceneTexts();
            WireKeys();

            _currentInput = string.Empty;
            RefreshDisplay();

            _isActive = true;
            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] Activo. Esperando PIN de {PinLength} dígitos.", this);
        }

        private void PopulateSceneTexts()
        {
            AssignText(questionText, _data.deductionQuestion);
        }

        private void WireKeys()
        {
            if (keys == null || keys.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(ConsolePuzzleController)}] No hay ConsoleKey asignados en el Inspector.",
                    this);
                return;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(ConsolePuzzleController)}] Slot keys[{i}] sin asignar.", this);
                    continue;
                }
                keys[i].Setup(HandleKeyPress);
            }
        }

        private void HandleKeyPress(string key)
        {
            if (!_isActive || _isSolved || _isShowingError) return;
            if (string.IsNullOrEmpty(key)) return;

            if (key == ClearKey)
            {
                _currentInput = string.Empty;
                RefreshDisplay();
                return;
            }

            if (key == EnterKey)
            {
                EvaluatePin();
                return;
            }

            // Resto: solo aceptamos un dígito por tecla.
            if (key.Length != 1 || !char.IsDigit(key[0]))
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] Valor de tecla no soportado: '{key}'. " +
                    "Esperaba un dígito, \"C\" o \"E\".", this);
                return;
            }

            if (_currentInput.Length >= PinLength) return;

            _currentInput += key;
            RefreshDisplay();
        }

        private void EvaluatePin()
        {
            string expected = _data?.pin ?? string.Empty;

            if (!string.IsNullOrEmpty(expected) && _currentInput == expected)
            {
                _isSolved = true;
                _isActive = false;
                StopErrorRoutineIfRunning();

                Debug.Log(
                    $"[{nameof(ConsolePuzzleController)}] PIN correcto → OnPuzzleSolved.", this);
                OnPuzzleSolved?.Invoke();
                return;
            }

            // Fallo: mostramos ERROR durante un segundo y reseteamos el input.
            StopErrorRoutineIfRunning();
            _errorRoutine = StartCoroutine(ShowErrorAndClear());
        }

        private IEnumerator ShowErrorAndClear()
        {
            _isShowingError = true;

            if (inputDisplay != null) inputDisplay.text = ErrorMessage;

            yield return new WaitForSeconds(ErrorDisplaySeconds);

            _currentInput = string.Empty;
            RefreshDisplay();
            _isShowingError = false;
            _errorRoutine = null;
        }

        private void StopErrorRoutineIfRunning()
        {
            if (_errorRoutine != null)
            {
                StopCoroutine(_errorRoutine);
                _errorRoutine = null;
            }
            _isShowingError = false;
        }

        private void RefreshDisplay()
        {
            if (inputDisplay == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] inputDisplay sin asignar; " +
                    "no se puede mostrar el progreso del PIN.", this);
                return;
            }

            if (string.IsNullOrEmpty(_currentInput))
            {
                inputDisplay.text = string.Empty;
                return;
            }

            inputDisplay.text = maskInput
                ? new string('*', _currentInput.Length)
                : _currentInput;
        }

        private void AssignText(TMP_Text target, string value)
        {
            if (target == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] Slot TMP_Text sin asignar en el Inspector.",
                    this);
                return;
            }
            target.text = value ?? string.Empty;
        }

        private void OnDisable()
        {
            // Si la escena se desactiva en mitad de un mensaje de error,
            // detenemos la corrutina para no dejar estado colgado.
            StopErrorRoutineIfRunning();
        }

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            if (_data == null)
            {
                Debug.LogError(
                    $"[{nameof(ConsolePuzzleController)}] No hay datos inyectados; " +
                    "no se puede simular la victoria.", this);
                return;
            }
            if (_isSolved)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] Ya resuelto; se ignora la simulación.",
                    this);
                return;
            }
            if (!_isActive)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] Simulando victoria sin activar primero " +
                    "(se permite en modo debug).", this);
            }

            DumpChainOfThought();

            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] Victoria simulada → OnPuzzleSolved " +
                $"(PIN esperado: {_data.pin}).", this);
            _isSolved = true;
            _isActive = false;
            StopErrorRoutineIfRunning();
            OnPuzzleSolved?.Invoke();
        }

        /// <summary>
        /// Vuelca en consola el "Chain of Thought" emitido por el LLM para el
        /// puzle 4: pregunta deductiva, razonamiento paso a paso y PIN final.
        /// Pensado como herramienta de Game Master / QA para auditar que el
        /// PIN se deduce realmente de los puzles 1-3 y no es un número
        /// aleatorio. NO se invoca en el flujo normal de juego.
        /// </summary>
        private void DumpChainOfThought()
        {
            string question = string.IsNullOrWhiteSpace(_data.deductionQuestion)
                ? "(sin pregunta)" : _data.deductionQuestion;
            string reasoning = string.IsNullOrWhiteSpace(_data.stepByStepReasoning)
                ? "(sin razonamiento — el backend no envió stepByStepReasoning)"
                : _data.stepByStepReasoning;
            string pin = string.IsNullOrEmpty(_data.pin) ? "????" : _data.pin;

            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] === Chain of Thought (Game Master) ===\n" +
                $"  • Pregunta deductiva : {question}\n" +
                $"  • Razonamiento (CoT) : {reasoning}\n" +
                $"  • PIN esperado       : {pin}",
                this);
        }
    }
}
