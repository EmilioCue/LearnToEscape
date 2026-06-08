using System;
using System.Collections.Generic;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using LearnToEscape.UI;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Greybox del puzle 2 (secuencia ordenada de 5 pasos). Recibe un
    /// <see cref="Puzzle2Router"/>, baraja los pasos visualmente y exige al
    /// jugador clicarlos en el orden correcto. Mantiene un menú contextual
    /// para simular la resolución durante el desarrollo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La aleatorización (Fisher-Yates) se aplica a la lista de parejas
    /// (índiceCorrecto, texto), no al array de <see cref="RouterNode"/>: los
    /// nodos físicos quedan fijos en la escena y solo cambia el contenido que
    /// reciben en <see cref="RouterNode.Setup"/>.
    /// </para>
    /// <para>
    /// Cada nodo se ignora a sí mismo el orden global; este controlador es el
    /// único que conoce el progreso (<see cref="_currentExpectedIndex"/>) y
    /// arbitra acierto/fallo, manteniendo a los nodos como vistas tontas.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class RouterPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Nodos físicos en la escena")]
        [Tooltip("Slots para los 5 pasos de la secuencia. El orden de este array " +
                 "no importa: los datos se asignarán barajados.")]
        [SerializeField] private RouterNode[] nodes;

        [Header("Colores de feedback")]
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color correctColor = Color.green;
        [SerializeField] private Color wrongColor = Color.red;

        private Puzzle2Router _data;
        private string _contextInstruction = string.Empty;
        private bool _isActive;
        private bool _isSolved;
        private int _currentExpectedIndex;
        private int _expectedTotal;

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

            _contextInstruction = "Ordena los elementos en la secuencia lógica correcta.";

            PopulateSceneTexts();
            _isActive = true;
            Debug.Log(
                $"[{nameof(RouterPuzzleController)}] Activo. " +
                $"Secuencia de {_data.sequence?.Length ?? 0} pasos.", this);
        }

        private void PopulateSceneTexts()
        {
            if (_data.sequence == null) return;
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(RouterPuzzleController)}] No hay RouterNode asignados en el Inspector.",
                    this);
                return;
            }

            int count = Mathf.Min(_data.sequence.Length, nodes.Length);

            var pairs = new List<(int correctIndex, string text)>(count);
            for (int i = 0; i < count; i++)
                pairs.Add((i, _data.sequence[i]));

            // Fisher-Yates: garantiza permutación uniforme y evita sesgos del sort por random.
            for (int i = pairs.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pairs[i], pairs[j]) = (pairs[j], pairs[i]);
            }

            for (int i = 0; i < count; i++)
            {
                if (nodes[i] == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(RouterPuzzleController)}] Slot RouterNode {i} sin asignar en el Inspector.",
                        this);
                    continue;
                }
                nodes[i].Setup(pairs[i].correctIndex, pairs[i].text, OnNodeClicked);
                nodes[i].SetBaselineColor(defaultColor);
                nodes[i].SetColor(defaultColor);
            }

            _currentExpectedIndex = 0;
            _expectedTotal = count;

            if (_data.sequence.Length > nodes.Length)
            {
                Debug.LogWarning(
                    $"[{nameof(RouterPuzzleController)}] La IA generó " +
                    $"{_data.sequence.Length} pasos pero solo hay " +
                    $"{nodes.Length} nodos visuales; se descartan los sobrantes.",
                    this);
            }
        }

        /// <summary>
        /// Devuelve todos los nodos asignados al color base del intento actual.
        /// </summary>
        private void ResetAllNodes()
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                    nodes[i].ResetState();
            }
        }

        private void OnNodeClicked(RouterNode clickedNode)
        {
            if (!_isActive || _isSolved || clickedNode == null) return;

            if (clickedNode.CorrectSequenceIndex == _currentExpectedIndex)
            {
                clickedNode.SetColor(correctColor);
                _currentExpectedIndex++;

                if (_currentExpectedIndex >= _expectedTotal)
                {
                    _isSolved = true;
                    _isActive = false;
                    Debug.Log(
                        $"[{nameof(RouterPuzzleController)}] Secuencia completada → OnPuzzleSolved.",
                        this);
                    OnPuzzleSolved?.Invoke();
                }
            }
            else
            {
                ResetAllNodes();
                clickedNode.SetColor(wrongColor);
                _currentExpectedIndex = 0;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            HUDManager.Instance?.ShowContextText(_contextInstruction);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            HUDManager.Instance?.HideContextText();
        }

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            _isSolved = true;
            _isActive = false;
            Debug.Log($"[{nameof(RouterPuzzleController)}] Resolución simulada → OnPuzzleSolved.", this);
            OnPuzzleSolved?.Invoke();
        }
    }
}
