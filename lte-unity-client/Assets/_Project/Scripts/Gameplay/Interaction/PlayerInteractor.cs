using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToEscape.Gameplay.Interaction
{
    /// <summary>
    /// Lanza un raycast desde la cámara hacia adelante en cada frame y gestiona
    /// el hover y la interacción con cualquier objeto que implemente
    /// <see cref="IInteractable"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Responsabilidad única</b>: este componente solo detecta qué objeto
    /// interactuable está mirando el jugador y cuándo pulsa la acción de
    /// interactuar. La lógica de "qué ocurre al interactuar" es exclusiva
    /// de cada implementación de <see cref="IInteractable"/>.
    /// </para>
    /// <para>
    /// <b>Montaje en escena</b>: añade este componente a la <c>MainCamera</c>
    /// del First-Person Controller. Asigna en el Inspector la
    /// <see cref="interactAction"/> (un <c>InputActionReference</c> que apunte
    /// a la acción de interactuar del <c>InputActionAsset</c> del proyecto,
    /// p.ej. la tecla E o el botón izquierdo del ratón).
    /// </para>
    /// <para>
    /// <b>Máscara de capas</b>: aunque el campo <see cref="interactionMask"/>
    /// se puede dejar en "Everything", restringirlo a las capas donde viven
    /// los objetos interactuables (p.ej. "Interactable") mejora el rendimiento
    /// del raycast y evita falsos positivos con colisionadores de UI o triggers.
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

        [Header("Input")]
        [Tooltip("Referencia a la acción de interactuar del InputActionAsset " +
                 "(p.ej. tecla E o clic izquierdo).")]
        [SerializeField] private InputActionReference interactAction;

        // El IInteractable actualmente bajo el rayo; null si no hay ninguno.
        private IInteractable _currentTarget;

        private Camera _camera;

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
            // Si la cámara se desactiva, limpiamos el hover para no dejar
            // objetos en estado "resaltado" sin que nadie los limpie.
            ClearCurrentTarget();

            if (interactAction != null)
                interactAction.action.Disable();
        }

        private void Update()
        {
            ScanForInteractable();

            if (_currentTarget != null && WantsToInteract())
                _currentTarget.Interact();
        }

        /// <summary>
        /// Lanza el raycast y actualiza <see cref="_currentTarget"/>, llamando
        /// a <see cref="IInteractable.OnHoverEnter"/> y
        /// <see cref="IInteractable.OnHoverExit"/> según corresponda.
        /// </summary>
        private void ScanForInteractable()
        {
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            IInteractable hit = null;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, interactionRange, interactionMask))
                hitInfo.collider.TryGetComponent(out hit);

            if (hit == _currentTarget) return;

            // Cambia de objetivo: notifica al anterior (si existía) y al nuevo.
            ClearCurrentTarget();

            if (hit != null)
            {
                _currentTarget = hit;
                _currentTarget.OnHoverEnter();
            }
        }

        /// <summary>
        /// Limpia el objetivo actual llamando a <see cref="IInteractable.OnHoverExit"/>
        /// y poniendo la referencia a <c>null</c>.
        /// </summary>
        private void ClearCurrentTarget()
        {
            if (_currentTarget == null) return;
            _currentTarget.OnHoverExit();
            _currentTarget = null;
        }

        /// <summary>
        /// Devuelve <c>true</c> si la acción de interactuar fue pulsada este
        /// frame. Maneja defensivamente el caso en que <see cref="interactAction"/>
        /// no esté asignada.
        /// </summary>
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

#if UNITY_EDITOR
        /// <summary>
        /// Dibuja el rayo de interacción en el editor cuando el objeto está
        /// seleccionado. Verde si golpea un <see cref="IInteractable"/>,
        /// amarillo si golpea otra cosa, rojo si no golpea nada.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;

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
        }
#endif
    }
}
