using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Zona de caída del puzle 1 (Matrix): un área con trigger que rastrea
    /// qué <see cref="DraggableMatrixItem"/> contiene en cada momento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El controlador no necesita preguntarle cada frame: la zona emite
    /// <see cref="OnContentsChanged"/> cuando entra o sale un ítem distinto
    /// a los ya rastreados, permitiendo validación puramente event-driven.
    /// </para>
    /// <para>
    /// El <see cref="Collider"/> debe ser un <c>trigger</c>. Si no lo es, el
    /// <see cref="Awake"/> lo fuerza y emite warning para que el diseñador
    /// lo detecte en el editor.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class MatrixDropZone : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe el nombre de la categoría. " +
                 "Si se deja vacío, se busca automáticamente en los hijos.")]
        [SerializeField] private TMP_Text label;

        [Header("Snap (DOTween)")]
        [Tooltip("Punto al que se animará magnéticamente el ítem al soltarse " +
                 "dentro de la zona. Crea un GameObject hijo vacío centrado en " +
                 "la zona y asígnalo aquí.")]
        [SerializeField] private Transform snapPoint;

        [Tooltip("Duración de la animación de snap.")]
        [SerializeField] private float snapDuration = 0.3f;

        /// <summary>Índice de categoría que esta zona representa (0 o 1).</summary>
        public int ZoneCategoryIndex { get; private set; } = -1;

        /// <summary>
        /// Se dispara cuando el conjunto de ítems dentro de la zona cambia
        /// (entra o sale un ítem que antes no estaba / dejó de estarlo).
        /// </summary>
        public event Action OnContentsChanged;

        private readonly HashSet<DraggableMatrixItem> _itemsInside = new();

        /// <summary>Vista de solo lectura de los ítems actualmente dentro de la zona.</summary>
        public IReadOnlyCollection<DraggableMatrixItem> ItemsInside => _itemsInside;

        /// <summary>Devuelve true si la zona contiene actualmente el ítem dado.</summary>
        public bool Contains(DraggableMatrixItem item) => item != null && _itemsInside.Contains(item);

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);

            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning(
                    $"[{nameof(MatrixDropZone)}] El collider debe ser trigger; " +
                    "se fuerza isTrigger=true en runtime.", this);
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// Configura la zona con su índice y el nombre de la categoría.
        /// Debe llamarse desde el controlador antes de activar el puzle.
        /// </summary>
        public void Setup(int categoryIndex, string categoryName)
        {
            ZoneCategoryIndex = categoryIndex;

            if (label != null)
                label.text = categoryName ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(MatrixDropZone)}] No hay TMP_Text hijo donde escribir " +
                    $"'{categoryName}'. Asigna uno en el Inspector.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<DraggableMatrixItem>();
            // Ignoramos el ítem si lo está llevando el jugador: OnTriggerStay
            // lo registrará en el siguiente FixedUpdate cuando IsHeld = false.
            if (item == null || item.IsHeld) return;

            if (_itemsInside.Add(item))
            {
                SnapItem(item);
                OnContentsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Cubre el caso en que el jugador suelta un ítem <em>dentro</em> de la
        /// zona: el ítem ya solapaba el trigger antes de que IsHeld pasase a
        /// false, por lo que <c>OnTriggerEnter</c> no vuelve a dispararse.
        /// <c>OnTriggerStay</c> lo registra en el siguiente FixedUpdate.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            var item = other.GetComponentInParent<DraggableMatrixItem>();
            if (item == null || item.IsHeld) return;

            // Add() devuelve false si ya estaba en el set → sin snap ni notificación duplicada.
            if (_itemsInside.Add(item))
            {
                SnapItem(item);
                OnContentsChanged?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var item = other.GetComponentInParent<DraggableMatrixItem>();
            if (item == null) return;

            // Eliminamos sin comprobar IsHeld: si el jugador recoge el ítem
            // de dentro de la zona, debe dejar de estar registrado aquí.
            if (_itemsInside.Remove(item))
                OnContentsChanged?.Invoke();
        }

        // ------------------------------------------------------------------ //
        //  Snap magnético (DOTween)                                            //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Anima el ítem hasta el <see cref="snapPoint"/> de la zona y lo emparenta
        /// allí para que quede fijo aunque el jugador se mueva cerca.
        /// </summary>
        private void SnapItem(DraggableMatrixItem item)
        {
            if (snapPoint == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MatrixDropZone)}] snapPoint no asignado en '{name}'. " +
                    "El ítem se registra pero no se anima. Crea un Transform hijo " +
                    "centrado en la zona y asígnalo al campo snapPoint.", this);
                return;
            }

            var t = item.transform;

            // Cancelar tween previo (p.ej. grab animation que aún no terminó).
            t.DOKill(complete: false);

            // Emparentar al snapPoint para que el ítem quede solidario a la zona
            // (si la zona se mueve en el futuro o la escena usa escala dinámica).
            t.SetParent(snapPoint, worldPositionStays: true);

            t.DOLocalMove(Vector3.zero, snapDuration)
             .SetEase(Ease.OutBounce);

            t.DOLocalRotate(Vector3.zero, snapDuration)
             .SetEase(Ease.OutBounce);
        }
    }
}
