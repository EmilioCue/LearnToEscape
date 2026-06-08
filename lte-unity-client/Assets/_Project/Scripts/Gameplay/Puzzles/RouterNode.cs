using System;
using LearnToEscape.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Nodo seleccionable del puzle 2 (Router). Representa visualmente un paso de la
    /// secuencia y notifica al controlador cuando el jugador lo activa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El nodo desconoce el orden correcto global: solo guarda el índice que le
    /// asigna el controlador en <see cref="Setup"/>. El controlador es quien
    /// decide si la interacción ha sido acierto o fallo, manteniendo a este
    /// componente como una "vista" sin lógica de juego.
    /// </para>
    /// <para>
    /// Implementa <see cref="IInteractable"/> para integrarse con
    /// <see cref="LearnToEscape.Gameplay.Interaction.PlayerInteractor"/>:
    /// <list type="bullet">
    ///   <item><see cref="OnHoverEnter"/> / <see cref="OnHoverExit"/> — feedback de material.</item>
    ///   <item><see cref="Interact"/> — sustituye al antiguo <c>OnMouseDown</c>.</item>
    /// </list>
    /// El controlador colorea el label TMP (acierto/fallo/reset) vía
    /// <see cref="SetColor"/>; ese método también actualiza la referencia de
    /// color base del hover para que <see cref="OnHoverExit"/> no vuelva al
    /// blanco del <see cref="Awake"/> ignorando el estado de gameplay.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class RouterNode : MonoBehaviour, IInteractable
    {
        [Header("Visual")]
        [Tooltip("TMP hijo donde se escribe el texto del paso. Si se deja vacío, " +
                 "se busca automáticamente en los hijos al despertar.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Color de hover del material del nodo (indica que es seleccionable).")]
        [SerializeField] private Color hoverColor = Color.cyan;

        /// <summary>
        /// Posición (0..N-1) que este nodo ocupa en la secuencia correcta
        /// emitida por el backend. Se asigna en <see cref="Setup"/>.
        /// </summary>
        public int CorrectSequenceIndex { get; private set; } = -1;

        private Action<RouterNode> _onClick;

        private MeshRenderer _meshRenderer;
        private TMP_Text _tmpText;
        private Color _originalColor;
        /// <summary>
        /// Color base del nodo para este intento (p. ej. <c>defaultColor</c> del controlador).
        /// <see cref="ResetState"/> lo restaura tras un fallo en la secuencia.
        /// </summary>
        private Color _baselineColor;

        private void Awake()
        {
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(includeInactive: true);

            _tmpText = null;
            _meshRenderer = null;
            _originalColor = Color.white;

            // TMP en el mismo objeto tiene prioridad (evita el MeshRenderer interno de TMP 3D).
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

            _baselineColor = _originalColor;
        }

        /// <summary>
        /// Configura el nodo con su índice correcto, el texto a mostrar y el
        /// callback al que avisará cuando el jugador lo active.
        /// </summary>
        /// <param name="index">Posición correcta en la secuencia (0..N-1).</param>
        /// <param name="text">Texto del paso a mostrar en el TMP hijo.</param>
        /// <param name="onClickCallback">Receptor; se invoca pasando este nodo.</param>
        public void Setup(int index, string text, Action<RouterNode> onClickCallback)
        {
            CorrectSequenceIndex = index;
            _onClick = onClickCallback;

            if (label != null)
                label.text = text ?? string.Empty;
            else
                Debug.LogWarning(
                    $"[{nameof(RouterNode)}] No hay TMP_Text hijo donde escribir " +
                    $"'{text}'. Asigna uno en el Inspector.", this);
        }

        /// <summary>
        /// Fija el color “neutral” del nodo para el intento actual (coincide con
        /// el <c>defaultColor</c> del controlador). Debe llamarse al poblar el
        /// puzle antes o junto con el primer <see cref="SetColor"/> de feedback.
        /// </summary>
        public void SetBaselineColor(Color baseline)
        {
            _baselineColor = baseline;
        }

        /// <summary>
        /// Restaura el aspecto al color base guardado en <see cref="_baselineColor"/>
        /// (coincide con el estado “neutral” del puzle) y sincroniza
        /// <see cref="_originalColor"/> para el hover.
        /// </summary>
        public void ResetState()
        {
            _originalColor = _baselineColor;
            if (label != null)
                label.color = _baselineColor;
            if (_tmpText != null)
                _tmpText.color = _baselineColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = _baselineColor;
        }

        /// <summary>
        /// Cambia el color del <b>label</b> TMP. Lo usa el controlador para
        /// feedback de acierto (verde), fallo (rojo) o reset (blanco).
        /// Actualiza <see cref="_originalColor"/> al mismo valor para que
        /// <see cref="OnHoverExit"/> restaure ese estado y no el color cacheado
        /// en <see cref="Awake"/>.
        /// </summary>
        public void SetColor(Color color)
        {
            _originalColor = color;

            if (label != null)
                label.color = color;
        }

        // ------------------------------------------------------------------ //
        //  IInteractable                                                        //
        // ------------------------------------------------------------------ //

        /// <inheritdoc />
        public void OnHoverEnter()
        {
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
        /// Sustituye al antiguo <c>OnMouseDown</c>. Notifica al controlador del
        /// puzle 2 que este nodo ha sido activado por el jugador.
        /// </remarks>
        public void Interact()
        {
            _onClick?.Invoke(this);
        }
    }
}
