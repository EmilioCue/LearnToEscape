using System;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Nodo clicable del puzle 2 (Router). Representa visualmente un paso de la
    /// secuencia y notifica al controlador cuando el jugador lo selecciona.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El nodo desconoce el orden correcto global: solo guarda el índice que le
    /// asigna el controlador en <see cref="Setup"/>. El controlador es quien
    /// decide si el clic ha sido acierto o fallo, manteniendo a este componente
    /// como una "vista" sin lógica de juego.
    /// </para>
    /// <para>
    /// Requiere un <see cref="Collider"/> para que <see cref="OnMouseDown"/>
    /// reciba clics. La detección depende de que la cámara renderice una capa
    /// alcanzable por raycast (sin <c>Ignore Raycast</c>).
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class RouterNode : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("TMP hijo donde se escribe el texto del paso. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        /// <summary>
        /// Posición (0..N-1) que este nodo ocupa en la secuencia correcta
        /// emitida por el backend. Se asigna en <see cref="Setup"/>.
        /// </summary>
        public int CorrectSequenceIndex { get; private set; } = -1;

        private Action<RouterNode> _onClick;

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>
        /// Configura el nodo con su índice correcto en la secuencia, el texto a
        /// mostrar y el callback al que avisará cuando el jugador haga clic.
        /// </summary>
        /// <param name="index">Posición correcta en la secuencia (0..N-1).</param>
        /// <param name="text">Texto del paso a mostrar en el TMP hijo.</param>
        /// <param name="onClickCallback">Receptor del clic; se invoca pasando este nodo.</param>
        public void Setup(int index, string text, Action<RouterNode> onClickCallback)
        {
            CorrectSequenceIndex = index;
            _onClick = onClickCallback;

            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(RouterNode)}] No hay TMP_Text hijo donde escribir " +
                    $"'{text}'. Asigna uno en el Inspector.", this);
            }
        }

        /// <summary>
        /// Cambia el color del texto del nodo. Lo usa el controlador para dar
        /// feedback visual (acierto, fallo o reset).
        /// </summary>
        public void SetColor(Color color)
        {
            if (label != null) label.color = color;
        }

        private void OnMouseDown()
        {
            _onClick?.Invoke(this);
        }
    }
}
