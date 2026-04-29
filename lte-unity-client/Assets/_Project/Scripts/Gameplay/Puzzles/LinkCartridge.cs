using System;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Cartucho arrastrable del puzle 3 (Link). Lleva un concepto y se
    /// inserta en la <see cref="LinkBay"/> cuya definición le corresponde.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El <see cref="Rigidbody"/> se fuerza a cinemático en <see cref="Awake"/>:
    /// solo está para habilitar los eventos de trigger contra las bahías. El
    /// arrastre se aplica directamente sobre el <c>transform</c>, sin gravedad
    /// ni inercia, igual que en el puzle 1.
    /// </para>
    /// <para>
    /// El cartucho desconoce qué bahía es la "correcta": solo guarda su
    /// <see cref="PairId"/>. Es la <see cref="LinkBay"/> quien compara su
    /// <c>ExpectedPairId</c> con el del cartucho que tenga dentro y el
    /// controlador del puzle quien decide si se ha resuelto.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class LinkCartridge : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe el concepto. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        [Header("Arrastre")]
        [Tooltip("Cámara usada para convertir la posición del ratón a mundo. " +
                 "Si se deja vacía, se usa Camera.main.")]
        [SerializeField] private Camera dragCamera;

        /// <summary>
        /// Identificador de la pareja concepto-definición a la que pertenece este
        /// cartucho. Coincide con el índice de <see cref="Content.Puzzle3Link.pairs"/>.
        /// </summary>
        public int PairId { get; private set; } = -1;

        /// <summary>Se dispara cuando el jugador suelta este cartucho.</summary>
        public event Action<LinkCartridge> OnReleased;

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
        /// Configura el cartucho con el id de su pareja y el texto del concepto.
        /// Debe llamarse una vez desde el controlador antes de activar el puzle.
        /// </summary>
        public void Setup(int pairId, string conceptText)
        {
            PairId = pairId;

            if (label != null)
            {
                label.text = conceptText ?? string.Empty;
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(LinkCartridge)}] No hay TMP_Text hijo donde escribir " +
                    $"'{conceptText}'. Asigna uno en el Inspector.", this);
            }
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
                    $"[{nameof(LinkCartridge)}] No hay cámara de arrastre " +
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
