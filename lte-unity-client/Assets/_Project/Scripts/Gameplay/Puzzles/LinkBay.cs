using System;
using DG.Tweening;
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
    /// La relación es 1 a 1: cada bahía rastrea un único <see cref="_currentCartridge"/>.
    /// Cuando el cartucho actual sale, la bahía queda vacía.
    /// </para>
    /// <para>
    /// El controlador no necesita preguntar cada frame: la bahía emite
    /// <see cref="OnContentsChanged"/> cuando el cartucho actual cambia,
    /// permitiendo validación puramente event-driven (mismo patrón que
    /// <c>MatrixDropZone</c>).
    /// </para>
    /// <para>
    /// <b>Filtrado de IsHeld</b>: los triggers ignoran cartuchos que el jugador
    /// está transportando. <c>OnTriggerStay</c> captura el cartucho en el
    /// siguiente FixedUpdate tras soltar, cuando <c>IsHeld</c> ya es <c>false</c>.
    /// </para>
    /// <para>
    /// <b>Game Feel — Snap magnético</b>: al registrar un cartucho, la bahía lo
    /// emparenta al <see cref="snapPoint"/> y lo anima con
    /// <c>DOLocalMove / DOLocalRotate + OutBounce</c> para el efecto de "encaje".
    /// </para>
    /// <para>
    /// El <see cref="Collider"/> debe ser un <c>trigger</c>; si no lo es, el
    /// <see cref="Awake"/> lo fuerza y emite warning.
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

        [Header("Snap (DOTween)")]
        [Tooltip("Punto al que se animará magnéticamente el cartucho al soltarse " +
                 "dentro de la bahía. Crea un GameObject hijo vacío centrado en " +
                 "la ranura y asígnalo aquí.")]
        [SerializeField] private Transform snapPoint;

        [Tooltip("Duración de la animación de snap.")]
        [SerializeField] private float snapDuration = 0.3f;

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
                label.text = definitionText ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(LinkBay)}] No hay TMP_Text hijo donde escribir " +
                    $"'{definitionText}'. Asigna uno en el Inspector.", this);
        }

        // ------------------------------------------------------------------ //
        //  Trigger                                                             //
        // ------------------------------------------------------------------ //

        private void OnTriggerEnter(Collider other)
        {
            var cart = other.GetComponentInParent<LinkCartridge>();
            // Ignoramos cartuchos en tránsito: OnTriggerStay los registrará
            // en el siguiente FixedUpdate cuando IsHeld ya sea false.
            if (cart == null || cart.IsHeld) return;
            if (_currentCartridge == cart) return;

            _currentCartridge = cart;
            SnapCartridge(cart);
            OnContentsChanged?.Invoke();
        }

        /// <summary>
        /// Cubre el caso en que el jugador suelta el cartucho <em>dentro</em>
        /// de la bahía: el collider ya solapaba el trigger antes de que
        /// <c>IsHeld</c> pasase a <c>false</c>, por lo que <c>OnTriggerEnter</c>
        /// no vuelve a dispararse. <c>OnTriggerStay</c> lo captura en el
        /// siguiente FixedUpdate.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            var cart = other.GetComponentInParent<LinkCartridge>();
            if (cart == null || cart.IsHeld) return;
            if (_currentCartridge == cart) return;

            _currentCartridge = cart;
            SnapCartridge(cart);
            OnContentsChanged?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            var cart = other.GetComponentInParent<LinkCartridge>();
            if (cart == null) return;

            // Sin comprobar IsHeld: si el jugador recoge el cartucho y lo
            // aleja de la bahía, debe dejar de estar registrado aquí.
            if (_currentCartridge == cart)
            {
                _currentCartridge = null;
                OnContentsChanged?.Invoke();
            }
        }

        // ------------------------------------------------------------------ //
        //  Snap magnético (DOTween)                                            //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Emparenta el cartucho al <see cref="snapPoint"/> de la bahía y lo
        /// anima magnéticamente hasta el centro de la ranura.
        /// </summary>
        private void SnapCartridge(LinkCartridge cart)
        {
            if (snapPoint == null)
            {
                Debug.LogWarning(
                    $"[{nameof(LinkBay)}] snapPoint no asignado en '{name}'. " +
                    "El cartucho se registra pero no se anima. Crea un Transform " +
                    "hijo centrado en la ranura y asígnalo al campo snapPoint.", this);
                return;
            }

            var t = cart.transform;

            // Cancelar tween previo (p.ej. grab animation que aún no ha terminado).
            t.DOKill(complete: false);

            // Emparentar al snapPoint: el cartucho queda solidario a la bahía
            // hasta que el jugador lo recoja (OnTriggerExit detectará la salida).
            t.SetParent(snapPoint, worldPositionStays: true);

            t.DOLocalMove(Vector3.zero, snapDuration)
             .SetEase(Ease.OutBounce);

            t.DOLocalRotate(Vector3.zero, snapDuration)
             .SetEase(Ease.OutBounce);
        }
    }
}
