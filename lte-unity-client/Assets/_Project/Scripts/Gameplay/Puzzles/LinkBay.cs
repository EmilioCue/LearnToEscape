using System;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Bahía del puzle 3 (Link): zona con trigger que muestra una definición
    /// y comprueba si el <see cref="LinkCartridge"/> insertado dentro tiene
    /// el <see cref="LinkCartridge.PairId"/> que esta bahía espera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La relación es 1 a 1: cada bahía rastrea un único cartucho actual. Si
    /// un segundo cartucho entra antes de que el primero salga, sustituye al
    /// primero como "actual". Cuando el cartucho actual sale, la bahía queda
    /// vacía aunque haya otros que estuvieran solapando — basta para greybox
    /// y evita la complejidad de gestionar conjuntos de cartuchos en una
    /// mecánica que conceptualmente es exclusiva.
    /// </para>
    /// <para>
    /// El controlador no necesita preguntar cada frame: la bahía emite
    /// <see cref="OnContentsChanged"/> cuando el cartucho actual cambia,
    /// permitiendo validación event-driven (mismo patrón que
    /// <c>MatrixDropZone</c>).
    /// </para>
    /// <para>
    /// El <see cref="Collider"/> debe ser un <c>trigger</c>; si no lo es, el
    /// <see cref="Awake"/> lo fuerza y emite warning para que el diseñador
    /// lo detecte en el editor.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class LinkBay : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe la definición. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        /// <summary>
        /// Identificador de la pareja concepto-definición que esta bahía espera.
        /// Coincide con el índice de <see cref="Content.Puzzle3Link.pairs"/>.
        /// </summary>
        public int ExpectedPairId { get; private set; } = -1;

        /// <summary>
        /// Se dispara cuando el cartucho contenido cambia (entra uno nuevo o
        /// sale el actual). El controlador la usa para revalidar el puzle.
        /// </summary>
        public event Action OnContentsChanged;

        private LinkCartridge _currentCartridge;

        /// <summary>Cartucho actualmente insertado en la bahía, o <c>null</c>.</summary>
        public LinkCartridge CurrentCartridge => _currentCartridge;

        /// <summary>
        /// True si la bahía contiene un cartucho cuyo <see cref="LinkCartridge.PairId"/>
        /// coincide con <see cref="ExpectedPairId"/>.
        /// </summary>
        public bool IsCorrectlyLinked =>
            _currentCartridge != null && _currentCartridge.PairId == ExpectedPairId;

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);

            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning(
                    $"[{nameof(LinkBay)}] El collider debe ser trigger; " +
                    "se fuerza isTrigger=true en runtime.", this);
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// Configura la bahía con el id de la pareja que espera y el texto de
        /// la definición a mostrar. Debe llamarse desde el controlador antes
        /// de activar el puzle.
        /// </summary>
        public void Setup(int pairId, string definitionText)
        {
            ExpectedPairId = pairId;

            if (label != null)
            {
                label.text = definitionText ?? string.Empty;
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(LinkBay)}] No hay TMP_Text hijo donde escribir " +
                    $"'{definitionText}'. Asigna uno en el Inspector.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var cart = other.GetComponentInParent<LinkCartridge>();
            if (cart == null) return;
            if (_currentCartridge == cart) return;

            _currentCartridge = cart;
            OnContentsChanged?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            var cart = other.GetComponentInParent<LinkCartridge>();
            if (cart == null) return;

            if (_currentCartridge == cart)
            {
                _currentCartridge = null;
                OnContentsChanged?.Invoke();
            }
        }
    }
}
