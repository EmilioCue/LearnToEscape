using System;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Contrato común para los controladores de puzle físico de la sala.
    /// Cada implementación es responsable de gestionar su propia UI/interacción
    /// y de anunciar su resolución sin conocer al resto del flujo.
    /// </summary>
    /// <remarks>
    /// El ciclo de vida esperado es:
    /// <list type="number">
    ///   <item><see cref="InjectData"/> — el gestor entrega el bloque de datos ya parseado.</item>
    ///   <item><see cref="ActivatePuzzle"/> — el gestor habilita la interacción cuando es el turno del puzle.</item>
    ///   <item><see cref="OnPuzzleSolved"/> — el puzle notifica al gestor que ha sido resuelto.</item>
    /// </list>
    /// El tipo real del argumento de <see cref="InjectData"/> es una de las
    /// clases de <c>RoomData</c> (p. ej. <c>Puzzle1Matrix</c>); la implementación
    /// debe castear defensivamente y fallar pronto si el tipo no coincide.
    /// </remarks>
    public interface IPuzzleController
    {
        /// <summary>
        /// Entrega al puzle su bloque de datos ya deserializado.
        /// Debe llamarse una única vez, antes de <see cref="ActivatePuzzle"/>.
        /// </summary>
        /// <param name="puzzleData">
        /// Sub-objeto concreto del <c>RoomData</c> correspondiente a este puzle.
        /// </param>
        void InjectData(object puzzleData);

        /// <summary>
        /// Habilita las interacciones del puzle (inputs, raycasts, UI…).
        /// Hasta que se llame, el puzle permanece en estado latente.
        /// </summary>
        void ActivatePuzzle();

        /// <summary>
        /// Se dispara exactamente una vez cuando el jugador resuelve el puzle.
        /// El gestor de sala lo utiliza para encadenar la activación del siguiente.
        /// </summary>
        event Action OnPuzzleSolved;
    }
}
