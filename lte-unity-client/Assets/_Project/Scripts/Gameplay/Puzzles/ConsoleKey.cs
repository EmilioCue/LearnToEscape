using System;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Tecla 3D del teclado numérico del puzle 4 (Console). Cada tecla
    /// guarda un valor literal (<c>"0"</c>-<c>"9"</c>, <c>"C"</c> para Clear,
    /// <c>"E"</c> para Enter) y notifica al controlador cuando el jugador la
    /// pulsa con el ratón.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La tecla no contiene lógica de juego: solo conoce su <see cref="keyValue"/>
    /// y dispara el callback que el controlador le inyecta en <see cref="Setup"/>.
    /// Esto permite que el mismo script se reutilice para dígitos y modificadores
    /// (Clear/Enter) cambiando únicamente el campo serializado en el Inspector.
    /// </para>
    /// <para>
    /// Requiere un <see cref="Collider"/> para que <see cref="OnMouseDown"/>
    /// reciba clics. La detección depende de que la cámara renderice una capa
    /// alcanzable por raycast.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ConsoleKey : MonoBehaviour
    {
        [Header("Identidad de la tecla")]
        [Tooltip("Valor literal que enviará al controlador al pulsarse: " +
                 "\"0\"-\"9\", \"C\" (Clear) o \"E\" (Enter).")]
        [SerializeField] private string keyValue;

        /// <summary>Valor que esta tecla envía al controlador al pulsarse.</summary>
        public string KeyValue => keyValue;

        private Action<string> _onKeyPress;

        /// <summary>
        /// Inyecta el callback al que la tecla notificará su <see cref="keyValue"/>
        /// cuando el jugador la pulse. El controlador suele llamarlo al activar
        /// el puzle.
        /// </summary>
        public void Setup(Action<string> onKeyPress)
        {
            _onKeyPress = onKeyPress;
        }

        private void OnMouseDown()
        {
            if (_onKeyPress == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsoleKey)}] Tecla '{keyValue}' pulsada sin callback inyectado " +
                    $"(¿olvidaste llamar a Setup desde el controlador?).", this);
                return;
            }
            _onKeyPress.Invoke(keyValue);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(keyValue))
                Debug.LogWarning(
                    $"[{nameof(ConsoleKey)}] keyValue vacío en '{name}'. " +
                    "Asigna \"0\"-\"9\", \"C\" o \"E\".", this);
        }
    }
}
