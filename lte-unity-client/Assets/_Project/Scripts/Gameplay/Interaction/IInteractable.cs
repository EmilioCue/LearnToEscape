namespace LearnToEscape.Gameplay.Interaction
{
    /// <summary>
    /// Contrato para cualquier objeto del mundo con el que el jugador pueda
    /// interactuar mediante el sistema de <see cref="PlayerInteractor"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mantener este contrato como interfaz (y no como clase base) permite que
    /// cualquier <c>MonoBehaviour</c> existente lo implemente sin cambiar su
    /// jerarquía de herencia. Un puzle, un ítem, un NPC o una puerta pueden
    /// ser "interactuables" sin compartir ningún ancestro común.
    /// </para>
    /// <para>
    /// Ciclo de vida esperado dictado por <see cref="PlayerInteractor"/>:
    /// <list type="number">
    ///   <item><see cref="OnHoverEnter"/> — el rayo del jugador empieza a apuntar al objeto.</item>
    ///   <item><see cref="OnHoverExit"/>  — el rayo deja de apuntar al objeto (o sale del rango).</item>
    ///   <item><see cref="Interact"/>     — el jugador pulsa la acción de interactuar mientras apunta.</item>
    /// </list>
    /// <see cref="OnHoverExit"/> siempre se llama antes de cambiar a otro
    /// objeto o de limpiar el estado, así que las implementaciones pueden
    /// usarlo con seguridad para revertir cualquier efecto visual iniciado
    /// en <see cref="OnHoverEnter"/>.
    /// </para>
    /// </remarks>
    public interface IInteractable
    {
        /// <summary>
        /// Se invoca cuando el rayo del jugador empieza a apuntar a este objeto.
        /// Úsalo para mostrar un highlight, un tooltip o cualquier feedback visual.
        /// </summary>
        void OnHoverEnter();

        /// <summary>
        /// Se invoca cuando el rayo del jugador deja de apuntar a este objeto
        /// (cambia de objetivo o sale del rango de interacción).
        /// Úsalo para revertir el estado visual iniciado en <see cref="OnHoverEnter"/>.
        /// </summary>
        void OnHoverExit();

        /// <summary>
        /// Se invoca cuando el jugador pulsa la acción de interactuar
        /// mientras el rayo apunta a este objeto.
        /// </summary>
        void Interact();
    }
}
