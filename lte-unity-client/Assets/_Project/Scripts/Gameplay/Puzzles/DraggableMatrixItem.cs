using System;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Ítem arrastrable con ratón del puzle 1 (Matrix). Representa un concepto
    /// que el jugador debe clasificar en una <see cref="MatrixDropZone"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El <see cref="Rigidbody"/> se fuerza a cinemático en <see cref="Awake"/>:
    /// su única función es habilitar los eventos de trigger con las zonas de
    /// caída. El movimiento se aplica directamente sobre el <c>transform</c>
    /// durante el arrastre, y los ítems se quedan donde el jugador los suelta
    /// (sin gravedad ni inercia).
    /// </para>
    /// <para>
    /// El controlador gobierna la interactividad: el ítem nace en estado
    /// <em>no interactuable</em> y solo responde al ratón cuando
    /// <see cref="SetInteractable"/> recibe <c>true</c> (típicamente al
    /// activarse el puzle). Al resolverse, se vuelve a desactivar para impedir
    /// que el jugador siga moviéndolos.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class DraggableMatrixItem : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe el nombre del ítem. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        [Header("Arrastre")]
        [Tooltip("Cámara usada para convertir la posición del ratón a mundo. " +
                 "Si se deja vacía, se usa Camera.main.")]
        [SerializeField] private Camera dragCamera;

        /// <summary>Índice de categoría correcta (0 o 1) asignado por la IA.</summary>
        public int AssignedCategoryIndex { get; private set; } = -1;

        /// <summary>Se dispara cuando el jugador suelta este ítem.</summary>
        public event Action<DraggableMatrixItem> OnReleased;

        private Rigidbody _rb;
        private Vector3 _grabWorldOffset;
        private float _grabScreenDepth;
        private bool _isDragging;
        private bool _interactable;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;

            if (dragCamera == null) dragCamera = Camera.main;
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>
        /// Configura el ítem con su categoría correcta y el nombre visible.
        /// Debe llamarse una vez desde el controlador del puzle antes de activarlo.
        /// </summary>
        public void Setup(int categoryIndex, string itemName)
        {
            AssignedCategoryIndex = categoryIndex;

            if (label != null)
                label.text = itemName ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(DraggableMatrixItem)}] No hay TMP_Text hijo donde escribir " +
                    $"'{itemName}'. Asigna uno en el Inspector.", this);
        }

        /// <summary>
        /// Habilita o deshabilita el arrastre. Si se desactiva durante un
        /// arrastre en curso, lo cancela limpiamente.
        /// </summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value && _isDragging) _isDragging = false;
        }

        private void OnMouseDown()
        {
            if (!_interactable) return;
            if (dragCamera == null)
            {
                Debug.LogError(
                    $"[{nameof(DraggableMatrixItem)}] No hay cámara de arrastre " +
                    "(dragCamera vacío y Camera.main no existe).", this);
                return;
            }

            _grabScreenDepth = dragCamera.WorldToScreenPoint(transform.position).z;
            Vector3 worldUnderCursor = ScreenToWorldAtDepth(Input.mousePosition, _grabScreenDepth);
            _grabWorldOffset = transform.position - worldUnderCursor;
            _isDragging = true;
        }

        private void OnMouseDrag()
        {
            if (!_isDragging || dragCamera == null) return;

            Vector3 worldUnderCursor = ScreenToWorldAtDepth(Input.mousePosition, _grabScreenDepth);
            transform.position = worldUnderCursor + _grabWorldOffset;
        }

        private void OnMouseUp()
        {
            if (!_isDragging) return;
            _isDragging = false;
            OnReleased?.Invoke(this);
        }

        private Vector3 ScreenToWorldAtDepth(Vector3 screenPos, float depth)
        {
            return dragCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        }
    }
}
