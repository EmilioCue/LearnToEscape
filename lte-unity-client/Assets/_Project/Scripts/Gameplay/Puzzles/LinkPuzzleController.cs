using System;
using System.Collections.Generic;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using LearnToEscape.UI;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Controlador del puzle 3 (Link) en su versión jugable: orquesta
    /// <see cref="LinkCartridge"/> y <see cref="LinkBay"/> ya colocados en la
    /// escena, baraja los conceptos para que no aparezcan enfrente de su
    /// definición y dispara <see cref="OnPuzzleSolved"/> en cuanto cada
    /// bahía contiene el cartucho de su pareja.
    /// </summary>
    /// <remarks>
    /// <para>Validación event-driven: se suscribe a
    /// <see cref="LinkBay.OnContentsChanged"/> de cada bahía activa y revalida
    /// solo cuando el estado cambia — cero polling por frame.</para>
    /// <para>Las <em>bahías</em> mantienen su orden de slot en la escena
    /// (<c>bays[i].ExpectedPairId == i</c>): así las definiciones se leen
    /// "ordenadas" mientras los <em>cartuchos</em> se reparten con un
    /// Fisher-Yates sobre los <c>pairId</c>, lo que garantiza que ningún
    /// concepto nazca enfrente de su propia respuesta.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class LinkPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Elementos de escena")]
        [Tooltip("Cartuchos arrastrables (uno por concepto). Los sobrantes se ocultan.")]
        [SerializeField] private LinkCartridge[] cartridges;

        [Tooltip("Bahías receptoras (una por definición). Las sobrantes se ocultan.")]
        [SerializeField] private LinkBay[] bays;

        private Puzzle3Link _data;
        private string _contextInstruction = string.Empty;
        private bool _isActive;
        private bool _isSolved;

        private readonly List<LinkCartridge> _activeCartridges = new();
        private readonly List<LinkBay> _activeBays = new();

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

            _contextInstruction = "Empareja cada concepto con su definición correspondiente.";

            PopulateSceneTexts();
            SubscribeBayEvents();

            _isActive = true;
            Debug.Log(
                $"[{nameof(LinkPuzzleController)}] Activo. " +
                $"{_activeCartridges.Count} cartuchos en {_activeBays.Count} bahías.", this);
        }

        private void PopulateSceneTexts()
        {
            _activeCartridges.Clear();
            _activeBays.Clear();

            if (_data.pairs == null) return;

            int cartridgeSlots = cartridges?.Length ?? 0;
            int baySlots = bays?.Length ?? 0;
            int visualLimit = Mathf.Min(cartridgeSlots, baySlots);
            int count = Mathf.Min(_data.pairs.Length, visualLimit);

            // Bahías: cada slot físico bays[i] guarda la pareja i. Su orden no se
            // baraja para que las definiciones queden estables en la escena.
            for (int i = 0; i < count; i++)
            {
                var bay = bays[i];
                var pair = _data.pairs[i];
                if (bay == null || pair == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(LinkPuzzleController)}] Slot bays[{i}] o pareja nula; se omite.",
                        this);
                    continue;
                }
                bay.gameObject.SetActive(true);
                bay.Setup(i, pair.definition);
                _activeBays.Add(bay);
            }
            for (int i = count; i < baySlots; i++)
                if (bays[i] != null) bays[i].gameObject.SetActive(false);

            // Cartuchos: barajamos los pairId con Fisher-Yates y los repartimos
            // por los slots físicos en orden. Así el cartucho cartridges[i] recibe
            // el concepto de una pareja "aleatoria", nunca colocado de oficio
            // enfrente de su definición.
            var shuffledIds = new List<int>(count);
            for (int i = 0; i < count; i++) shuffledIds.Add(i);
            for (int i = shuffledIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffledIds[i], shuffledIds[j]) = (shuffledIds[j], shuffledIds[i]);
            }

            for (int i = 0; i < count; i++)
            {
                var cart = cartridges[i];
                int pairId = shuffledIds[i];
                var pair = _data.pairs[pairId];
                if (cart == null || pair == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(LinkPuzzleController)}] Slot cartridges[{i}] o pareja nula; se omite.",
                        this);
                    continue;
                }
                cart.gameObject.SetActive(true);
                cart.Setup(pairId, pair.concept);
                cart.SetInteractable(true);
                _activeCartridges.Add(cart);
            }
            for (int i = count; i < cartridgeSlots; i++)
                if (cartridges[i] != null) cartridges[i].gameObject.SetActive(false);

            if (_data.pairs.Length > visualLimit)
            {
                Debug.LogWarning(
                    $"[{nameof(LinkPuzzleController)}] La IA generó " +
                    $"{_data.pairs.Length} parejas pero solo hay {visualLimit} slots visuales " +
                    $"(cartuchos: {cartridgeSlots}, bahías: {baySlots}); " +
                    "se descartan las sobrantes.", this);
            }
        }

        private void SubscribeBayEvents()
        {
            foreach (var bay in _activeBays)
                bay.OnContentsChanged += HandleBayContentsChanged;
        }

        private void UnsubscribeBayEvents()
        {
            foreach (var bay in _activeBays)
                bay.OnContentsChanged -= HandleBayContentsChanged;
        }

        private void HandleBayContentsChanged()
        {
            if (_isSolved || !_isActive) return;
            if (!AllBaysCorrectlyLinked()) return;

            _isSolved = true;
            _isActive = false;

            foreach (var cart in _activeCartridges)
                cart.SetInteractable(false);

            UnsubscribeBayEvents();

            Debug.Log(
                $"[{nameof(LinkPuzzleController)}] Todas las parejas correctas " +
                "→ OnPuzzleSolved.", this);
            OnPuzzleSolved?.Invoke();
        }

        /// <summary>True si todas las bahías activas tienen su cartucho correcto.</summary>
        private bool AllBaysCorrectlyLinked()
        {
            foreach (var bay in _activeBays)
                if (!bay.IsCorrectlyLinked) return false;
            return true;
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

        private void OnDestroy() => UnsubscribeBayEvents();

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            _isSolved = true;
            _isActive = false;
            UnsubscribeBayEvents();
            Debug.Log($"[{nameof(LinkPuzzleController)}] Resolución simulada → OnPuzzleSolved.", this);
            OnPuzzleSolved?.Invoke();
        }
    }
}
