using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToEscape.Gameplay.Interaction
{
    /// <summary>
    /// Lanza un raycast desde el centro de la cámara, gestiona el hover sobre
    /// cualquier <see cref="IInteractable"/> y permite al jugador "sostener"
    /// objetos que implementen <see cref="IHoldable"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Responsabilidad única</b>: detectar qué objeto está mirando el jugador,
    /// cuándo pulsa la acción de interactuar y manejar el parenting al
    /// <see cref="holdPoint"/>. La lógica de "qué ocurre al interactuar" o "cómo
    /// cambiar el aspecto visual" es exclusiva de cada <see cref="IInteractable"/>.
    /// </para>
    /// <para>
    /// <b>Dos modos de operación</b>:
    /// <list type="bullet">
    ///   <item>
    ///     <b>Libre</b> — no hay objeto sostenido. Se escanea cada frame con
    ///     raycast y se gestiona el hover. Al pulsar interact sobre un
    ///     <see cref="IHoldable"/> se pasa a modo Sosteniendo.
    ///     Un <see cref="IInteractable"/> simple llama a <c>Interact()</c>
    ///     y permanece en modo Libre.
    ///   </item>
    ///   <item>
    ///     <b>Sosteniendo</b> — se suprime el raycast para evitar hover
    ///     involuntario. Al pulsar interact se llama a <c>Interact()</c> sobre
    ///     el objeto sostenido (que se suelta) y se vuelve a modo Libre.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Montaje</b>: añade el componente a la <c>MainCamera</c>. Crea un
    /// <c>GameObject</c> hijo vacío frente a la cámara (p.ej. a 0.6 m) y
    /// asígnalo como <see cref="holdPoint"/>. Conecta la
    /// <see cref="interactAction"/> al asset de Input Actions del proyecto.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Distancia máxima (en metros) desde la que el jugador puede " +
                 "interactuar con un objeto.")]
        [SerializeField] private float interactionRange = 3f;

        [Tooltip("Capas que el raycast puede golpear. Restringir a la capa " +
                 "de objetos interactuables mejora el rendimiento.")]
        [SerializeField] private LayerMask interactionMask = Physics.AllLayers;

        [Header("Holding")]
        [Tooltip("Punto hijo de la cámara donde flotará el objeto sostenido. " +
                 "Crea un GameObject vacío frente a la cámara y asígnalo aquí.")]
        [SerializeField] private Transform holdPoint;

        [Header("Game Feel — Agarre suave (DOTween)")]
        [Tooltip("Tiempo que tarda el objeto en volar desde su posición hasta la mano.")]
        [SerializeField] private float grabMoveDuration = 0.25f;

        [Tooltip("Tiempo que tarda el objeto en orientarse recto en la mano.")]
        [SerializeField] private float grabRotateDuration = 0.25f;

        [Header("Input")]
        [Tooltip("Referencia a la acción de interactuar del InputActionAsset " +
                 "(p.ej. tecla E o clic izquierdo).")]
        [SerializeField] private InputActionReference interactAction;

        // Objeto bajo el rayo en modo Libre (null si no hay ninguno).
        private IInteractable _currentTarget;

        // Objeto que el jugador está sosteniendo (null si manos vacías).
        private IHoldable _currentHeldObject;

        private Camera _camera;

        private bool IsHolding => _currentHeldObject != null;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (interactAction != null)
                interactAction.action.Enable();
        }

        private void OnDisable()
        {
            // Limpieza defensiva: soltamos lo que llevemos y borramos hover.
            ForceDropHeldObject();
            ClearCurrentTarget();

            if (interactAction != null)
                interactAction.action.Disable();
        }

        private void Update()
        {
            // En modo Sosteniendo suprimimos el raycast para evitar que el
            // jugador active el hover de objetos mientras lleva algo en la mano.
            if (!IsHolding)
                ScanForInteractable();

            if (!WantsToInteract()) return;

            if (IsHolding)
                HandleDropInteraction();
            else
                HandlePickOrInteract();
        }

        // ------------------------------------------------------------------ //
        //  Interacción — Libre                                                 //
        // ------------------------------------------------------------------ //

        private void HandlePickOrInteract()
        {
            if (_currentTarget == null) return;

            _currentTarget.Interact();

            // Si el target es IHoldable y acaba de pasar a sostenido, lo
            // vinculamos al holdPoint. El ítem ya gestionó sus físicas.
            if (_currentTarget is IHoldable holdable && holdable.IsHeld)
                AttachToHoldPoint(holdable);
        }

        // ------------------------------------------------------------------ //
        //  Interacción — Sosteniendo                                           //
        // ------------------------------------------------------------------ //

        private void HandleDropInteraction()
        {
            _currentHeldObject.Interact(); // El ítem gestiona sus propias físicas.
            DetachFromHoldPoint();
        }

        // ------------------------------------------------------------------ //
        //  Parenting                                                           //
        // ------------------------------------------------------------------ //

        private void AttachToHoldPoint(IHoldable holdable)
        {
            if (holdPoint == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerInteractor)}] holdPoint no asignado. " +
                    "El objeto se marcó como sostenido pero no se puede vincular " +
                    "a la cámara. Asigna un Transform hijo como holdPoint.", this);
                return;
            }

            var mono = holdable as MonoBehaviour;
            if (mono == null) return;

            // worldPositionStays: true → el objeto mantiene su posición de mundo
            // al ser reparentado. DOTween anima desde ahí hasta el origen del holdPoint.
            mono.transform.DOKill(complete: false);
            mono.transform.SetParent(holdPoint, worldPositionStays: true);
            mono.transform.DOLocalMove(Vector3.zero, grabMoveDuration)
                          .SetEase(Ease.OutBack);
            mono.transform.DOLocalRotate(Vector3.zero, grabRotateDuration)
                          .SetEase(Ease.OutBack);

            _currentHeldObject = holdable;
            // Borramos el hover: el objeto ya no está "apuntado", está en la mano.
            ClearCurrentTarget();
        }

        private void DetachFromHoldPoint()
        {
            var mono = _currentHeldObject as MonoBehaviour;
            mono?.transform.SetParent(null);
            _currentHeldObject = null;
        }

        /// <summary>
        /// Fuerza soltar el objeto sin notificar al ítem (solo para limpieza de
        /// emergencia en <see cref="OnDisable"/>).
        /// </summary>
        private void ForceDropHeldObject()
        {
            if (!IsHolding) return;
            var mono = _currentHeldObject as MonoBehaviour;
            if (mono != null)
            {
                mono.transform.DOKill(complete: false);
                mono.transform.SetParent(null);
            }
            _currentHeldObject = null;
        }

        // ------------------------------------------------------------------ //
        //  Raycast y hover                                                     //
        // ------------------------------------------------------------------ //

        private void ScanForInteractable()
        {
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            IInteractable hit = null;
            if (Physics.Raycast(ray, out RaycastHit hitInfo, interactionRange, interactionMask))
                hitInfo.collider.TryGetComponent(out hit);

            if (hit == _currentTarget) return;

            ClearCurrentTarget();

            if (hit != null)
            {
                _currentTarget = hit;
                _currentTarget.OnHoverEnter();
            }
        }

        private void ClearCurrentTarget()
        {
            if (_currentTarget == null) return;
            _currentTarget.OnHoverExit();
            _currentTarget = null;
        }

        // ------------------------------------------------------------------ //
        //  Input                                                               //
        // ------------------------------------------------------------------ //

        private bool WantsToInteract()
        {
            if (interactAction == null || interactAction.action == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerInteractor)}] interactAction no asignada en el Inspector. " +
                    "La interacción no funcionará hasta que se configure.", this);
                return false;
            }
            return interactAction.action.WasPressedThisFrame();
        }

        // ------------------------------------------------------------------ //
        //  Editor                                                              //
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;

            // Rayo de interacción.
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactionMask))
            {
                bool isInteractable = hit.collider.TryGetComponent<IInteractable>(out _);
                Gizmos.color = isInteractable ? Color.green : Color.yellow;
                Gizmos.DrawLine(ray.origin, hit.point);
                Gizmos.DrawWireSphere(hit.point, 0.05f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * interactionRange);
            }

            // Hold point.
            if (holdPoint != null)
            {
                Gizmos.color = IsHolding ? Color.cyan : new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawWireSphere(holdPoint.position, 0.08f);
            }
        }
#endif
    }
}
