namespace LearnToEscape.Gameplay.Interaction
{
    /// <summary>
    /// Extensión de <see cref="IInteractable"/> para objetos que el jugador
    /// puede "sostener" y llevar consigo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PlayerInteractor"/> distingue los <c>IHoldable</c> de los
    /// <c>IInteractable</c> simples (puertas, botones, consolas) para aplicar
    /// la lógica de parenting al <c>holdPoint</c> sin acoplar el interactor a
    /// ninguna clase concreta.
    /// </para>
    /// <para>
    /// El contrato de ciclo de vida añadido:
    /// <list type="number">
    ///   <item>
    ///     Primer <see cref="IInteractable.Interact"/> → el objeto se marca como
    ///     sostenido (<see cref="IsHeld"/> pasa a <c>true</c>) y gestiona su
    ///     propio estado interno (física, visual…). <see cref="PlayerInteractor"/>
    ///     lo comprueba inmediatamente después para hacer el parenting.
    ///   </item>
    ///   <item>
    ///     Segundo <see cref="IInteractable.Interact"/> → el objeto se suelta
    ///     (<see cref="IsHeld"/> pasa a <c>false</c>). <see cref="PlayerInteractor"/>
    ///     lo desvincula del <c>holdPoint</c>.
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IHoldable : IInteractable
    {
        /// <summary>
        /// <c>true</c> mientras el jugador sostiene este objeto;
        /// <c>false</c> cuando está en reposo en el mundo.
        /// </summary>
        bool IsHeld { get; }
    }
}
