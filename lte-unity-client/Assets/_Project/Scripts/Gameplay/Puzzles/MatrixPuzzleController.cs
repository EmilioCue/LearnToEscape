using System;
using System.Collections.Generic;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Controlador del puzle 1 (Matrix) en su versión jugable: orquesta
    /// <see cref="DraggableMatrixItem"/> e <see cref="MatrixDropZone"/>
    /// previamente colocados en la escena, y dispara
    /// <see cref="OnPuzzleSolved"/> en cuanto todos los ítems están dentro de
    /// la zona cuyo índice coincide con su categoría asignada por la IA.
    /// </summary>
    /// <remarks>
    /// <para>Validación event-driven: se suscribe a
    /// <see cref="MatrixDropZone.OnContentsChanged"/> de cada zona activa y
    /// revalida solo cuando el estado cambia — cero polling por frame.</para>
    /// <para>Al resolverse, desactiva el arrastre de todos los ítems activos
    /// y se desuscribe para evitar reentradas.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatrixPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Elementos de escena")]
        [Tooltip("Ítems arrastrables (entre 4 y 6 según diseño). Los sobrantes se ocultan.")]
        [SerializeField] private DraggableMatrixItem[] items;

        [Tooltip("Zonas de caída (exactamente 2 según contrato del backend).")]
        [SerializeField] private MatrixDropZone[] dropZones;

        private Puzzle1Matrix _data;
        private bool _isActive;
        private bool _isSolved;

        private readonly List<DraggableMatrixItem> _activeItems = new();
        private readonly Dictionary<int, MatrixDropZone> _zonesByIndex = new();

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

            ConfigureDropZones();
            ConfigureItems();
            SubscribeZoneEvents();

            _isActive = true;
            Debug.Log(
                $"[{nameof(MatrixPuzzleController)}] Activo. " +
                $"{_activeItems.Count} ítems arrastrables en {_zonesByIndex.Count} zonas.", this);
        }

        private void ConfigureDropZones()
        {
            _zonesByIndex.Clear();

            int zoneCount = Mathf.Min(_data.categories?.Length ?? 0, dropZones?.Length ?? 0);
            for (int i = 0; i < zoneCount; i++)
            {
                var zone = dropZones[i];
                if (zone == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(MatrixPuzzleController)}] Slot dropZones[{i}] sin asignar.", this);
                    continue;
                }
                zone.gameObject.SetActive(true);
                zone.Setup(i, _data.categories[i]);
                _zonesByIndex[i] = zone;
            }

            for (int i = zoneCount; i < (dropZones?.Length ?? 0); i++)
                if (dropZones[i] != null) dropZones[i].gameObject.SetActive(false);
        }

        private void ConfigureItems()
        {
            _activeItems.Clear();

            int available = Mathf.Min(_data.items?.Length ?? 0, items?.Length ?? 0);
            for (int i = 0; i < available; i++)
            {
                var slot = items[i];
                var src = _data.items[i];
                if (slot == null || src == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(MatrixPuzzleController)}] Slot items[{i}] o dato nulo; se omite.",
                        this);
                    continue;
                }
                slot.gameObject.SetActive(true);
                slot.Setup(src.categoryIndex, src.name);
                slot.SetInteractable(true);
                _activeItems.Add(slot);
            }

            for (int i = available; i < (items?.Length ?? 0); i++)
                if (items[i] != null) items[i].gameObject.SetActive(false);

            if ((_data.items?.Length ?? 0) > (items?.Length ?? 0))
            {
                Debug.LogWarning(
                    $"[{nameof(MatrixPuzzleController)}] La IA generó " +
                    $"{_data.items.Length} ítems pero solo hay " +
                    $"{items?.Length ?? 0} slots físicos; se descartan los sobrantes.", this);
            }
        }

        private void SubscribeZoneEvents()
        {
            foreach (var zone in _zonesByIndex.Values)
                zone.OnContentsChanged += HandleZoneContentsChanged;
        }

        private void UnsubscribeZoneEvents()
        {
            foreach (var zone in _zonesByIndex.Values)
                zone.OnContentsChanged -= HandleZoneContentsChanged;
        }

        private void HandleZoneContentsChanged()
        {
            if (_isSolved || !_isActive) return;
            if (!AllItemsInCorrectZones()) return;

            _isSolved = true;
            _isActive = false;

            foreach (var item in _activeItems)
                item.SetInteractable(false);

            UnsubscribeZoneEvents();

            Debug.Log(
                $"[{nameof(MatrixPuzzleController)}] Todos los ítems correctamente clasificados " +
                "→ OnPuzzleSolved.", this);
            OnPuzzleSolved?.Invoke();
        }

        /// <summary>
        /// True si cada ítem activo está dentro de la zona cuyo índice coincide
        /// con <see cref="DraggableMatrixItem.AssignedCategoryIndex"/>.
        /// </summary>
        private bool AllItemsInCorrectZones()
        {
            foreach (var item in _activeItems)
            {
                if (!_zonesByIndex.TryGetValue(item.AssignedCategoryIndex, out var correctZone))
                    return false;
                if (!correctZone.Contains(item))
                    return false;
            }
            return true;
        }

        private void OnDestroy() => UnsubscribeZoneEvents();
    }
}
