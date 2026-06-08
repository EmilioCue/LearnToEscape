using System;
using System.Collections.Generic;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Puzzles;
using LearnToEscape.UI;
using UnityEngine;

namespace LearnToEscape.Puzzles
{
    /// <summary>
    /// Controlador del puzle 1 (Matrix): instancia dinámicamente los
    /// <see cref="DraggableMatrixItem"/> a partir de los datos inyectados por el
    /// backend (no hay límite fijo de ítems) y dispara <see cref="OnPuzzleSolved"/>
    /// en cuanto todos están dentro de la zona correcta.
    /// </summary>
    /// <remarks>
    /// <para>Validación event-driven: se suscribe a
    /// <see cref="MatrixDropZone.OnContentsChanged"/> de cada zona activa y
    /// revalida solo cuando el estado cambia — cero polling por frame.</para>
    /// <para>Los ítems se instancian en <see cref="ActivatePuzzle"/> alrededor del
    /// <see cref="spawnPoint"/> con un offset aleatorio para evitar explosiones de
    /// física cuando las cajas nacen solapadas.</para>
    /// <para>Al resolverse, desactiva el arrastre, destruye los ítems instanciados
    /// y se desuscribe para evitar reentradas.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatrixPuzzleController : MonoBehaviour, IPuzzleController
    {
        /// <inheritdoc />
        public event Action OnPuzzleSolved;

        [Header("Fábrica de ítems")]
        [Tooltip("Prefab de DraggableMatrixItem que se instanciará una vez por cada ítem del JSON.")]
        [SerializeField] private DraggableMatrixItem itemPrefab;

        [Tooltip("Transform desde cuya posición se esparcen los ítems al instanciarse.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Radio máximo de dispersión aleatoria alrededor del spawnPoint. " +
                 "Evita que las cajas nazcan solapadas y las físicas exploten.")]
        [SerializeField] private float spawnScatterRadius = 0.5f;

        [Header("Zonas de caída")]
        [Tooltip("Zonas de caída de la escena (exactamente 2 según contrato del backend).")]
        [SerializeField] private MatrixDropZone[] dropZones;

        private Puzzle1Matrix _data;
        private string _contextInstruction = string.Empty;
        private bool _isActive;
        private bool _isSolved;

        private readonly List<DraggableMatrixItem> _activeItems = new();
        private readonly Dictionary<int, MatrixDropZone> _zonesByIndex = new();

        // ─────────────────────────────────────────────────────────────────────
        // IPuzzleController
        // ─────────────────────────────────────────────────────────────────────

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

            string categoryList = _data.categories != null
                ? string.Join(", ", _data.categories)
                : string.Empty;
            _contextInstruction = string.IsNullOrEmpty(categoryList)
                ? "Clasifica los elementos en las categorías correctas."
                : $"Clasifica los elementos en las categorías correctas: {categoryList}.";

            ConfigureDropZones();
            SpawnItems();
            SubscribeZoneEvents();

            _isActive = true;
            Debug.Log(
                $"[{nameof(MatrixPuzzleController)}] Activo. " +
                $"{_activeItems.Count} ítems instanciados en {_zonesByIndex.Count} zonas.", this);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Configuración
        // ─────────────────────────────────────────────────────────────────────

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

        /// <summary>
        /// Instancia un <see cref="DraggableMatrixItem"/> por cada ítem del JSON,
        /// distribuyéndolos alrededor de <see cref="spawnPoint"/> con un offset
        /// aleatorio para prevenir colisiones al nacer solapados.
        /// </summary>
        private void SpawnItems()
        {
            _activeItems.Clear();

            if (itemPrefab == null)
            {
                Debug.LogError(
                    $"[{nameof(MatrixPuzzleController)}] itemPrefab no asignado en el Inspector.",
                    this);
                return;
            }

            if (spawnPoint == null)
            {
                Debug.LogError(
                    $"[{nameof(MatrixPuzzleController)}] spawnPoint no asignado en el Inspector.",
                    this);
                return;
            }

            int count = _data.items?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                var src = _data.items[i];
                if (src == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(MatrixPuzzleController)}] _data.items[{i}] es null; se omite.",
                        this);
                    continue;
                }

                // Offset aleatorio en el plano horizontal para evitar solapamiento.
                Vector3 scatter = UnityEngine.Random.insideUnitSphere * spawnScatterRadius;
                scatter.y = 0f;
                Vector3 spawnPos = spawnPoint.position + scatter;

                DraggableMatrixItem newItem = Instantiate(itemPrefab, spawnPos, Quaternion.identity);
                newItem.Setup(src.categoryIndex, src.name);
                newItem.InitializeItem(src.name);
                newItem.SetInteractable(true);

                _activeItems.Add(newItem);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Suscripciones a eventos de zonas
        // ─────────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────
        // Lógica de victoria
        // ─────────────────────────────────────────────────────────────────────

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
                if (item == null) continue;
                if (!_zonesByIndex.TryGetValue(item.AssignedCategoryIndex, out var correctZone))
                    return false;
                if (!correctZone.Contains(item))
                    return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Proximidad — HUD de instrucción
        // ─────────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            UnsubscribeZoneEvents();
            DestroySpawnedItems();
        }

        private void DestroySpawnedItems()
        {
            foreach (var item in _activeItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _activeItems.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Debug / QA
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Simular Resolución")]
        private void SimulateSolve()
        {
            _isSolved = true;
            _isActive = false;
            foreach (var item in _activeItems)
                if (item != null) item.SetInteractable(false);
            UnsubscribeZoneEvents();
            Debug.Log($"[{nameof(MatrixPuzzleController)}] Resolución simulada → OnPuzzleSolved.", this);
            OnPuzzleSolved?.Invoke();
        }
    }
}
