using DG.Tweening;
using LearnToEscape.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Cartucho arrastrable del puzle 3 (Link). El jugador lo recoge con el
    /// sistema de interacción en primera persona y lo deposita en la
    /// <see cref="LinkBay"/> cuya definición le corresponde.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementa <see cref="IHoldable"/> (que extiende <see cref="IInteractable"/>):
    /// <list type="bullet">
    ///   <item><see cref="OnHoverEnter"/>/<see cref="OnHoverExit"/> — feedback visual.</item>
    ///   <item><see cref="Interact"/> — alterna entre "recoger" y "soltar".</item>
    ///   <item><see cref="IsHeld"/> — estado que <see cref="PlayerInteractor"/> consulta
    ///         para vincular o desvincular el cartucho del <c>holdPoint</c>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Física</b>: el <see cref="Rigidbody"/> es cinemático en todo momento.
    /// El <see cref="Collider"/> se <b>desactiva durante el agarre</b> para evitar
    /// que el cartucho (pegado al holdPoint) choque con el <c>CharacterController</c>.
    /// Unity dispara <c>OnTriggerExit</c> al desactivarlo dentro de un trigger y
    /// <c>OnTriggerEnter</c> al reactivarlo, de modo que <see cref="LinkBay"/>
    /// gestiona el estado correctamente sin filtros adicionales.
    /// </para>
    /// <para>
    /// El cartucho desconoce qué bahía es la "correcta": solo guarda su
    /// <see cref="PairId"/>. Es la <see cref="LinkBay"/> quien compara
    /// su <c>ExpectedPairId</c> con el del cartucho dentro.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class LinkCartridge : MonoBehaviour, IHoldable
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe el concepto. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Color de resaltado cuando el jugador apunta al cartucho.")]
        [SerializeField] private Color hoverColor = Color.yellow;

        /// <summary>
        /// Identificador de la pareja concepto-definición a la que pertenece este
        /// cartucho. Coincide con el índice de <see cref="Content.Puzzle3Link.pairs"/>.
        /// </summary>
        public int PairId { get; private set; } = -1;

        /// <inheritdoc />
        public bool IsHeld { get; private set; }

        private Rigidbody _rb;
        private Collider _col;
        private MeshRenderer _meshRenderer;
        private TMP_Text _tmpText;
        private Color _originalColor;
        private bool _interactable;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;

            _col = GetComponent<Collider>();

            _tmpText = null;
            _meshRenderer = null;
            _originalColor = Color.white;

            if (TryGetComponent<TMP_Text>(out _tmpText))
            {
                _originalColor = _tmpText.color;
            }
            else if (TryGetComponent<MeshRenderer>(out _meshRenderer))
            {
                Material mat = _meshRenderer.material;
                if (mat != null && mat.HasProperty("_Color"))
                    _originalColor = mat.color;
                else
                    _meshRenderer = null;
            }

            if (label == null)
                label = GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        // ------------------------------------------------------------------ //
        //  Configuración (llamada por LinkPuzzleController)                    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Configura el cartucho con el id de su pareja y el texto del concepto.
        /// Debe llamarse una vez desde el controlador antes de activar el puzle.
        /// </summary>
        public void Setup(int pairId, string conceptText)
        {
            PairId = pairId;

            if (label != null)
                label.text = conceptText ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(LinkCartridge)}] No hay TMP_Text hijo donde escribir " +
                    $"'{conceptText}'. Asigna uno en el Inspector.", this);
        }

        /// <summary>
        /// Habilita o deshabilita la interacción. Si se desactiva mientras está
        /// siendo sostenido, se suelta inmediatamente.
        /// </summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value && IsHeld)
                ForceRelease();
        }

        // ------------------------------------------------------------------ //
        //  IInteractable                                                        //
        // ------------------------------------------------------------------ //

        /// <inheritdoc />
        public void OnHoverEnter()
        {
            if (!_interactable || IsHeld) return;
            if (_tmpText != null)
                _tmpText.color = hoverColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = hoverColor;
        }

        /// <inheritdoc />
        public void OnHoverExit()
        {
            if (_tmpText != null)
                _tmpText.color = _originalColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = _originalColor;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Alterna entre recoger y soltar. <see cref="PlayerInteractor"/> consulta
        /// <see cref="IsHeld"/> inmediatamente después para vincular/desvincular
        /// el transform al <c>holdPoint</c>.
        /// </remarks>
        public void Interact()
        {
            if (!_interactable) return;

            if (!IsHeld)
                PickUp();
            else
                Release();
        }

        // ------------------------------------------------------------------ //
        //  Pick-up / Release                                                   //
        // ------------------------------------------------------------------ //

        private void PickUp()
        {
            IsHeld = true;

            // Desactivar el collider mientras viaja pegado al holdPoint: evita que
            // empuje el CharacterController. Unity dispara OnTriggerExit en la
            // LinkBay al desactivarlo, limpiando el estado de la bahía anterior.
            _col.enabled = false;

            // Cancelar snap animation antes de que PlayerInteractor inicie el grab.
            transform.DOKill(complete: false);

            // Restaurar color: en la mano el hover no tiene sentido.
            if (_tmpText != null)
                _tmpText.color = _originalColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = _originalColor;

            Debug.Log($"[{nameof(LinkCartridge)}] '{name}' recogido.", this);
        }

        private void Release()
        {
            IsHeld = false;

            // Reactivar el collider al soltar: si el cartucho está dentro de una
            // LinkBay Unity disparará OnTriggerEnter automáticamente.
            _col.enabled = true;

            Debug.Log($"[{nameof(LinkCartridge)}] '{name}' soltado.", this);
        }

        /// <summary>
        /// Suelta el cartucho de forma forzada desde <see cref="SetInteractable"/>
        /// sin pasar por <see cref="PlayerInteractor"/>.
        /// </summary>
        private void ForceRelease()
        {
            transform.DOKill(complete: false);
            transform.SetParent(null);
            Release();
        }
    }
}
