using UnityEngine;

namespace LearnToEscape.Gameplay.Interaction
{
    /// <summary>
    /// Implementación mínima de <see cref="IInteractable"/> para probar
    /// <see cref="PlayerInteractor"/> en escena (logs en consola + feedback de hover).
    /// </summary>
    /// <remarks>
    /// Arrastra este script a un cubo u otro objeto 3D. Unity añadirá un
    /// <see cref="BoxCollider"/> automáticamente gracias a
    /// <see cref="RequireComponentAttribute"/>. Asegúrate de que el objeto esté
    /// en una capa que el <c>PlayerInteractor</c> incluya en su
    /// <c>interactionMask</c>.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class InteractableTestCube : MonoBehaviour, IInteractable
    {
        [Header("Debug")]
        [SerializeField] private bool logHoverEvents = true;

        /// <inheritdoc />
        public void OnHoverEnter()
        {
            if (logHoverEvents)
                Debug.Log($"[{nameof(InteractableTestCube)}] Hover ENTER en '{name}'.", this);
        }

        /// <inheritdoc />
        public void OnHoverExit()
        {
            if (logHoverEvents)
                Debug.Log($"[{nameof(InteractableTestCube)}] Hover EXIT en '{name}'.", this);
        }

        /// <inheritdoc />
        public void Interact()
        {
            Debug.Log("¡Cubo interactuado!", this);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            // Al añadir el componente por primera vez, asegura un collider sólido
            // para Physics.Raycast (no trigger por defecto).
            var box = GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = false;
        }
#endif
    }
}
