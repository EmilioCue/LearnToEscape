using System;
using System.Text;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Caja gris del puzle 3 (emparejado concepto-definición). Recibe un
    /// <see cref="Puzzle3Link"/> y expone un menú contextual para simular
    /// la resolución durante el desarrollo, antes de tener UI física.
    /// </summary>
    [DisallowMultipleComponent]
    public class LinkPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        private Puzzle3Link _data;
        private bool _isActive;
        private bool _isSolved;

        /// <inheritdoc />
        public void InjectData(object puzzleData)
        {
            if (puzzleData is not Puzzle3Link typed)
            {
                Debug.LogError(
                    $"[{nameof(LinkPuzzleController)}] Tipo inválido en InjectData: " +
                    $"se esperaba {nameof(Puzzle3Link)} y llegó " +
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
                    $"[{nameof(LinkPuzzleController)}] ActivatePuzzle llamado sin datos inyectados.",
                    this);
                return;
            }
            _isActive = true;
            Debug.Log(
                $"[{nameof(LinkPuzzleController)}] Activo. " +
                $"{_data.pairs?.Length ?? 0} parejas concepto-definición.", this);
        }

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            if (_data == null)
            {
                Debug.LogError(
                    $"[{nameof(LinkPuzzleController)}] No hay datos inyectados; " +
                    "no se puede simular la resolución.", this);
                return;
            }
            if (_isSolved)
            {
                Debug.LogWarning(
                    $"[{nameof(LinkPuzzleController)}] Ya resuelto; se ignora la simulación.",
                    this);
                return;
            }
            if (!_isActive)
            {
                Debug.LogWarning(
                    $"[{nameof(LinkPuzzleController)}] Simulando resolución sin activar primero " +
                    "(se permite en modo debug).", this);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[{nameof(LinkPuzzleController)}] Resolución simulada (emparejamientos):");
            for (int i = 0; i < _data.pairs.Length; i++)
            {
                var pair = _data.pairs[i];
                if (pair == null)
                {
                    sb.AppendLine($"  [{i}] (pareja nula)");
                    continue;
                }
                sb.AppendLine($"  [{i}] \"{pair.concept}\"  <->  \"{pair.definition}\"");
            }
            Debug.Log(sb.ToString(), this);

            _isSolved = true;
            _isActive = false;
            OnPuzzleSolved?.Invoke();
        }
    }
}
