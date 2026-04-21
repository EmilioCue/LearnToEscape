using System;
using System.Text;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Caja gris del puzle 4 (consola final con PIN de 4 dígitos). Recibe un
    /// <see cref="Puzzle4Console"/> y expone un menú contextual para simular
    /// la victoria durante el desarrollo, antes de tener UI física.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConsolePuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        private Puzzle4Console _data;
        private bool _isActive;
        private bool _isSolved;

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
            _isActive = true;
            Debug.Log(
                $"[{nameof(ConsolePuzzleController)}] Activo. Esperando PIN de 4 dígitos.", this);
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

            var sb = new StringBuilder();
            sb.AppendLine($"[{nameof(ConsolePuzzleController)}] Victoria simulada:");
            sb.AppendLine($"  Pregunta de deducción: {_data.deductionQuestion}");
            sb.AppendLine($"  PIN esperado: {_data.pin}");
            Debug.Log(sb.ToString(), this);

            _isSolved = true;
            _isActive = false;
            OnPuzzleSolved?.Invoke();
        }
    }
}
