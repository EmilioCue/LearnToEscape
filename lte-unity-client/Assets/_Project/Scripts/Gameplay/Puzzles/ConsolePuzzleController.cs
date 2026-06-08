using System;
using System.Collections;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using LearnToEscape.UI;
using TMPro;
using UnityEngine;
using ConsoleKey = LearnToEscape.Gameplay.Puzzles.ConsoleKey;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Controlador del puzle 4 (Console): arranca inerte y solo acepta input
    /// tras recibir <see cref="PowerUpConsole"/> desde
    /// <see cref="LearnToEscape.Gameplay.Rooms.RoomFlowManager"/> cuando los
    /// 3 subsistemas previos han sido restaurados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Barrera de sincronización:</b> mientras <c>_isPoweredOn == false</c>
    /// el display permanece vacío, la emisión del material está apagada y
    /// cualquier pulsación de tecla o entrada al Trigger se ignora
    /// silenciosamente. Al encenderse, el color de emisión HDR del
    /// <see cref="panelRenderer"/> se actualiza por código sobre una instancia
    /// de material propia (sin tocar el asset del proyecto).
    /// </para>
    /// <para>
    /// <b>Cooldown anti-fuerza bruta:</b> tras un PIN erróneo, el input queda
    /// bloqueado durante <see cref="errorCooldownSeconds"/> y el display muestra
    /// el mensaje de error. Al expirar vuelve al estado "_ _ _ _".
    /// </para>
    /// <para>
    /// La pregunta de deducción del LLM se muestra en el HUD 2D gestionado por
    /// <see cref="HUDManager"/> cuando el jugador (tag "Player") entra en el
    /// Collider Trigger de este GameObject.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class ConsolePuzzleController : MonoBehaviour, IPuzzleController
    {
        private const string ClearKey = "C";
        private const string EnterKey = "E";
        private const string ErrorMessage = "ERR - BLOQUEO";
        private const int PinLength = 4;

        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Display 3D (PIN)")]
        [Tooltip("Texto TMP en el mundo 3D que muestra el estado de la consola y el PIN introducido.")]
        [SerializeField] private TMP_Text display3DText;

        [Header("Anti-fuerza bruta")]
        [Tooltip("Segundos de bloqueo de input tras introducir un PIN erróneo.")]
        [SerializeField] private float errorCooldownSeconds = 5f;

        [Header("Visual")]
        [Tooltip("Si está activo, el display muestra asteriscos en vez de los dígitos reales.")]
        [SerializeField] private bool maskInput = false;

        [Header("Visuals — Emisión del panel")]
        [Tooltip("Renderer que contiene el material URP de la consola.")]
        [SerializeField] private Renderer panelRenderer;

        [Tooltip("Color HDR de emisión cuando la consola está encendida.")]
        [SerializeField, ColorUsage(true, true)] private Color onlineEmissionColor = Color.green;

        private Puzzle4Console _data;
        private string _currentContextText = string.Empty;
        private Material _panelMaterialInstanced;

        private bool _isPoweredOn = false;
        private bool _isActive;
        private bool _isSolved;
        private bool _isInCooldown;
        private bool _playerInTrigger;

        private string _currentInput = string.Empty;
        private Coroutine _errorRoutine;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle — inicialización del material
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Garantiza pantalla vacía independientemente del estado del renderer.
            if (display3DText != null) display3DText.text = string.Empty;

            // Auto-descubrimiento: cablea todas las ConsoleKey hijas del prefab
            // sin necesidad de asignarlas manualmente en el Inspector.
            ConsoleKey[] allKeys = GetComponentsInChildren<ConsoleKey>(true);
            if (allKeys.Length == 0)
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] No se encontraron componentes " +
                    "ConsoleKey en los hijos. Comprueba la jerarquía del prefab.", this);
            foreach (ConsoleKey key in allKeys)
                key.Setup(HandleKeyPress);

            if (panelRenderer == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] panelRenderer sin asignar; " +
                    "el efecto de emisión no funcionará.", this);
                return;
            }

            // Instanciamos el material para no modificar el asset compartido del proyecto.
            _panelMaterialInstanced = panelRenderer.material;
            _panelMaterialInstanced.EnableKeyword("_EMISSION");
            _panelMaterialInstanced.SetColor("_EmissionColor", Color.black);
        }

        private void Start()
        {
            // Segunda limpieza: cubre el caso en que otro componente escriba en el
            // TMP durante Awake (p.ej. un prefab con texto de placeholder).
            if (display3DText != null) display3DText.text = string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────
        // IPuzzleController
        // ─────────────────────────────────────────────────────────────────────

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

            _currentContextText = _data.deductionQuestion ?? string.Empty;
            _currentInput = string.Empty;
            _isPoweredOn = false;

            // La consola arranca inerte: display vacío hasta PowerUpConsole().
            SetDisplayText(string.Empty);

            _isActive = true;
            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] Activo pero OFFLINE. " +
                "Esperando señal de sincronización.", this);
        }

        // ─────────────────────────────────────────────────────────────────────
        // API pública — Barrera de sincronización
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enciende la consola tras completarse los 3 subsistemas previos.
        /// Activa la emisión HDR del panel, habilita el input, actualiza el
        /// display a "_ _ _ _" y, si el jugador ya está dentro del Trigger,
        /// muestra la pregunta en el HUD.
        /// </summary>
        public void PowerUpConsole()
        {
            if (_isPoweredOn) return;

            _isPoweredOn = true;
            // Garantiza que el input funciona aunque ActivatePuzzle() no haya sido
            // llamado (p.ej. en escenas de testing sin GameSession.CurrentRoom).
            _isActive = true;

            _panelMaterialInstanced?.SetColor("_EmissionColor", onlineEmissionColor);

            _currentInput = string.Empty;
            RefreshDisplay();

            if (_playerInTrigger)
                HUDManager.Instance?.ShowContextText(_currentContextText);

            Debug.Log($"[{nameof(ConsolePuzzleController)}] ONLINE. Esperando PIN.", this);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Trigger de proximidad (HUD 2D)
        // ─────────────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInTrigger = true;
            if (!_isPoweredOn)
            {
                HUDManager.Instance?.ShowContextText(
                    "SISTEMA SIN ENERGÍA. Restaura los 3 subsistemas de red.");
                return;
            }
            HUDManager.Instance?.ShowContextText(_currentContextText);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInTrigger = false;
            HUDManager.Instance?.HideContextText();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Teclado
        // ─────────────────────────────────────────────────────────────────────

        private void HandleKeyPress(string key)
        {
            if (!_isPoweredOn) return;
            if (!_isActive || _isSolved || _isInCooldown) return;
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

        // ─────────────────────────────────────────────────────────────────────
        // Evaluación del PIN y cooldown
        // ─────────────────────────────────────────────────────────────────────

        private void EvaluatePin()
        {
            string expected = _data?.pin ?? string.Empty;

            if (!string.IsNullOrEmpty(expected) && _currentInput == expected)
            {
                _isSolved = true;
                _isActive = false;
                StopCooldownRoutineIfRunning();

                if (_playerInTrigger)
                    HUDManager.Instance?.HideContextText();

                Debug.Log(
                    $"[{nameof(ConsolePuzzleController)}] PIN correcto → OnPuzzleSolved.", this);
                OnPuzzleSolved?.Invoke();
                return;
            }

            StopCooldownRoutineIfRunning();
            _errorRoutine = StartCoroutine(ShowErrorAndClear());
        }

        private IEnumerator ShowErrorAndClear()
        {
            _isInCooldown = true;
            SetDisplayText(ErrorMessage);

            yield return new WaitForSeconds(errorCooldownSeconds);

            _currentInput = string.Empty;
            RefreshDisplay();
            _isInCooldown = false;
            _errorRoutine = null;
        }

        private void StopCooldownRoutineIfRunning()
        {
            if (_errorRoutine != null)
            {
                StopCoroutine(_errorRoutine);
                _errorRoutine = null;
            }
            _isInCooldown = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Display 3D
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Construye la representación visual del PIN en curso:
        /// posiciones rellenas con el dígito (o asterisco si <see cref="maskInput"/>)
        /// y posiciones vacías con "_". Ejemplo: "1 2 _ _".
        /// Solo debe llamarse cuando la consola está encendida.
        /// </summary>
        private void RefreshDisplay()
        {
            if (display3DText == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] display3DText sin asignar; " +
                    "no se puede mostrar el progreso del PIN.", this);
                return;
            }

            var sb = new System.Text.StringBuilder(PinLength * 2 - 1);
            for (int i = 0; i < PinLength; i++)
            {
                if (i > 0) sb.Append(' ');
                if (i < _currentInput.Length)
                    sb.Append(maskInput ? '*' : _currentInput[i]);
                else
                    sb.Append('_');
            }
            display3DText.text = sb.ToString();
        }

        private void SetDisplayText(string text)
        {
            if (display3DText == null) return;
            display3DText.text = text;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDisable()
        {
            StopCooldownRoutineIfRunning();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Debug / QA
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Simular PowerUp")]
        private void SimulatePowerUp()
        {
            if (!_isActive)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] El puzle no está activo todavía.", this);
                return;
            }
            PowerUpConsole();
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
            if (!_isPoweredOn)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsolePuzzleController)}] La consola está OFFLINE; " +
                    "se fuerza PowerUp antes de simular la victoria.", this);
                PowerUpConsole();
            }

            DumpChainOfThought();

            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] Victoria simulada → OnPuzzleSolved " +
                $"(PIN esperado: {_data.pin}).", this);
            _isSolved = true;
            _isActive = false;
            StopCooldownRoutineIfRunning();
            OnPuzzleSolved?.Invoke();
        }

        /// <summary>
        /// Vuelca en consola el "Chain of Thought" emitido por el LLM para el
        /// puzle 4. Herramienta de Game Master / QA; no se invoca en flujo normal.
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
