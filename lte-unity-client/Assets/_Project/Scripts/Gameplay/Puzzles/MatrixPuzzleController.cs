using System;
using System.Text;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Caja gris del puzle 1 (clasificación en 2 categorías). Recibe un
    /// <see cref="Puzzle1Matrix"/> y expone un menú contextual para simular
    /// la resolución durante el desarrollo, antes de tener UI física.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatrixPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        private Puzzle1Matrix _data;
        private bool _isActive;
        private bool _isSolved;

        /// <inheritdoc />
        public void InjectData(object puzzleData)
        {
            if (puzzleData is not Puzzle1Matrix typed)
            {
                Debug.LogError(
                    $"[{nameof(MatrixPuzzleController)}] Tipo inválido en InjectData: " +
                    $"se esperaba {nameof(Puzzle1Matrix)} y llegó " +
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
                    $"[{nameof(MatrixPuzzleController)}] ActivatePuzzle llamado sin datos inyectados.",
                    this);
                return;
            }
            _isActive = true;
            Debug.Log(
                $"[{nameof(MatrixPuzzleController)}] Activo. " +
                $"{_data.items?.Length ?? 0} ítems en {_data.categories?.Length ?? 0} categorías.",
                this);
        }

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            if (_data == null)
            {
                Debug.LogError(
                    $"[{nameof(MatrixPuzzleController)}] No hay datos inyectados; " +
                    "no se puede simular la resolución.", this);
                return;
            }
            if (_isSolved)
            {
                Debug.LogWarning(
                    $"[{nameof(MatrixPuzzleController)}] Ya resuelto; se ignora la simulación.",
                    this);
                return;
            }
            if (!_isActive)
            {
                Debug.LogWarning(
                    $"[{nameof(MatrixPuzzleController)}] Simulando resolución sin activar primero " +
                    "(se permite en modo debug).", this);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[{nameof(MatrixPuzzleController)}] Resolución simulada:");
            for (int c = 0; c < _data.categories.Length; c++)
            {
                sb.AppendLine($"  Categoría [{c}] \"{_data.categories[c]}\":");
                for (int i = 0; i < _data.items.Length; i++)
                {
                    var item = _data.items[i];
                    if (item != null && item.categoryIndex == c)
                        sb.AppendLine($"    - {item.name}");
                }
            }
            Debug.Log(sb.ToString(), this);

            _isSolved = true;
            _isActive = false;
            OnPuzzleSolved?.Invoke();
        }
    }
}
