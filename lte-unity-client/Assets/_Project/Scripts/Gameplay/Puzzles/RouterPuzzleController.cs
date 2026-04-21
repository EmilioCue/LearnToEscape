using System;
using System.Text;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Caja gris del puzle 2 (secuencia ordenada de 5 pasos). Recibe un
    /// <see cref="Puzzle2Router"/> y expone un menú contextual para simular
    /// la resolución durante el desarrollo, antes de tener UI física.
    /// </summary>
    [DisallowMultipleComponent]
    public class RouterPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        private Puzzle2Router _data;
        private bool _isActive;
        private bool _isSolved;

        /// <inheritdoc />
        public void InjectData(object puzzleData)
        {
            if (puzzleData is not Puzzle2Router typed)
            {
                Debug.LogError(
                    $"[{nameof(RouterPuzzleController)}] Tipo inválido en InjectData: " +
                    $"se esperaba {nameof(Puzzle2Router)} y llegó " +
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
                    $"[{nameof(RouterPuzzleController)}] ActivatePuzzle llamado sin datos inyectados.",
                    this);
                return;
            }
            _isActive = true;
            Debug.Log(
                $"[{nameof(RouterPuzzleController)}] Activo. " +
                $"Secuencia de {_data.sequence?.Length ?? 0} pasos.", this);
        }

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            if (_data == null)
            {
                Debug.LogError(
                    $"[{nameof(RouterPuzzleController)}] No hay datos inyectados; " +
                    "no se puede simular la resolución.", this);
                return;
            }
            if (_isSolved)
            {
                Debug.LogWarning(
                    $"[{nameof(RouterPuzzleController)}] Ya resuelto; se ignora la simulación.",
                    this);
                return;
            }
            if (!_isActive)
            {
                Debug.LogWarning(
                    $"[{nameof(RouterPuzzleController)}] Simulando resolución sin activar primero " +
                    "(se permite en modo debug).", this);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[{nameof(RouterPuzzleController)}] Resolución simulada (secuencia correcta):");
            for (int i = 0; i < _data.sequence.Length; i++)
                sb.AppendLine($"  {i + 1}. {_data.sequence[i]}");
            Debug.Log(sb.ToString(), this);

            _isSolved = true;
            _isActive = false;
            OnPuzzleSolved?.Invoke();
        }
    }
}
