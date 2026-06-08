using LearnToEscape.Content;

namespace LearnToEscape.Core
{
    /// <summary>
    /// Almacén estático que transfiere datos entre escenas de Unity.
    /// Los objetos estáticos persisten mientras el proceso está vivo,
    /// por lo que sobreviven a cualquier carga de escena aditiva o no.
    /// </summary>
    /// <remarks>
    /// Clase intencionadamente sin estado de instancia: no hereda de
    /// <c>MonoBehaviour</c> ni necesita <c>DontDestroyOnLoad</c>.
    /// El escritor (<c>MainMenuManager</c>) asigna antes del
    /// <c>SceneManager.LoadScene</c>; el lector (<c>RoomFlowManager</c>)
    /// consume en su <c>Start</c> una vez que la escena ha cargado.
    /// </remarks>
    public static class GameSession
    {
        /// <summary>
        /// Datos de sala generados en el menú principal y consumidos por
        /// <see cref="LearnToEscape.Gameplay.Rooms.RoomFlowManager"/> al
        /// arrancar la escena de la sala de escape.
        /// </summary>
        public static RoomData CurrentRoom { get; set; }
    }
}
