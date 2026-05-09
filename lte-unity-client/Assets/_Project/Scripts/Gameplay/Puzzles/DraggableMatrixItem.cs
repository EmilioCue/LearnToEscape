using DG.Tweening;
using LearnToEscape.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Ítem del puzle 1 (Matrix) que el jugador puede tomar y depositar en una
    /// <see cref="MatrixDropZone"/> usando el sistema de interacción en primera persona.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementa <see cref="IHoldable"/> (que extiende <see cref="IInteractable"/>):
    /// <list type="bullet">
    ///   <item><see cref="OnHoverEnter"/>/<see cref="OnHoverExit"/> — feedback visual de selección.</item>
    ///   <item><see cref="Interact"/> — alterna entre "recoger" y "soltar".</item>
    ///   <item><see cref="IsHeld"/> — estado que <see cref="PlayerInteractor"/> consulta
    ///         para decidir si debe vincular el objeto al <c>holdPoint</c>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Física</b>: el <see cref="Rigidbody"/> es cinemático en todo momento.
    /// El <see cref="Collider"/> se <b>desactiva durante el agarre</b> para evitar
    /// que el ítem (pegado al holdPoint de la cámara) choque con el
    /// <c>CharacterController</c> del jugador y lo lance por el aire.
    /// Unity dispara <c>OnTriggerExit</c> al desactivar el collider dentro de un
    /// trigger y <c>OnTriggerEnter</c> al reactivarlo, por lo que las
    /// <see cref="MatrixDropZone"/> detectan correctamente recoger y soltar.
    /// </para>
    /// <para>
    /// <b>Hover visual</b>: en el mismo GameObject, prioriza
    /// <c>TMP_Text</c>; si no hay, usa <c>MeshRenderer</c> solo si el material
    /// expone <c>_Color</c>. Así se evita tocar el mesh interno del texto TMP 3D.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class DraggableMatrixItem : MonoBehaviour, IHoldable
    {
        [Header("Visual")]
        [Tooltip("TextMeshPro hijo donde se escribe el nombre del ítem. Si se deja " +
                 "vacío, se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Color de resaltado cuando el jugador apunta al ítem.")]
        [SerializeField] private Color hoverColor = Color.yellow;

        /// <summary>Índice de categoría correcta (0 o 1) asignado por la IA.</summary>
        public int AssignedCategoryIndex { get; private set; } = -1;

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
        //  Configuración (llamada por MatrixPuzzleController)                  //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Configura el ítem con su categoría correcta y el nombre visible.
        /// Debe llamarse una vez desde el controlador antes de activar el puzle.
        /// </summary>
        public void Setup(int categoryIndex, string itemName)
        {
            AssignedCategoryIndex = categoryIndex;

            if (label != null)
                label.text = itemName ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(DraggableMatrixItem)}] No hay TMP_Text hijo donde " +
                    $"escribir '{itemName}'. Asigna uno en el Inspector.", this);
        }

        /// <summary>
        /// Habilita o deshabilita la interacción con este ítem. Si se desactiva
        /// mientras está siendo sostenido, se suelta inmediatamente.
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
            // empuje el CharacterController y salga volando. Unity dispara
            // OnTriggerExit en las MatrixDropZone al desactivarlo, limpiando el estado.
            _col.enabled = false;

            // Cancelar snap animation antes de que PlayerInteractor inicie el grab.
            transform.DOKill(complete: false);

            // Revertir color: en la mano ya no tiene sentido el hover.
            if (_tmpText != null)
                _tmpText.color = _originalColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = _originalColor;

            Debug.Log($"[{nameof(DraggableMatrixItem)}] '{name}' recogido.", this);
        }

        private void Release()
        {
            IsHeld = false;

            // Reactivar el collider al soltar: si el ítem está dentro de una
            // MatrixDropZone Unity disparará OnTriggerEnter automáticamente.
            _col.enabled = true;

            Debug.Log($"[{nameof(DraggableMatrixItem)}] '{name}' soltado.", this);
        }

        /// <summary>
        /// Suelta el ítem de forma forzada (sin notificar a <see cref="PlayerInteractor"/>).
        /// Solo para limpieza de emergencia desde <see cref="SetInteractable"/>.
        /// </summary>
        private void ForceRelease()
        {
            transform.DOKill(complete: false);
            transform.SetParent(null);
            Release();
        }
    }
}
