using System;
using DG.Tweening;
using LearnToEscape.Gameplay.Interaction;
using TMPro;
using UnityEngine;

namespace LearnToEscape.Gameplay.Puzzles
{
    /// <summary>
    /// Tecla 3D del teclado numérico del puzle 4 (Console). Notifica al
    /// controlador su <see cref="keyValue"/> al ser activada y simula el
    /// hundimiento mecánico de una tecla física usando DOTween.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La tecla no contiene lógica de juego: solo conoce su <see cref="keyValue"/>
    /// y dispara el callback inyectado en <see cref="Setup"/>. Implementa
    /// <see cref="IInteractable"/> para integrarse con
    /// <see cref="LearnToEscape.Gameplay.Interaction.PlayerInteractor"/>.
    /// </para>
    /// <para>
    /// <b>Game Feel — hundimiento mecánico</b>: <c>DOPunchLocalPosition</c>
    /// genera el impulso en el eje Z local y la respuesta elástica de vuelta en
    /// una sola llamada. <c>DOKill(true)</c> antes de cada pulsación garantiza
    /// que no haya tweens acumulados si el jugador clica muy rápido.
    /// </para>
    /// <para>
    /// <b>Orientación del botón</b>: el punch usa el eje Z local. Si la tecla
    /// "hunde" hacia la dirección equivocada, invierte el signo del campo
    /// <see cref="punchAxis"/> en el Inspector o rota el GameObject 180° en Y.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ConsoleKey : MonoBehaviour, IInteractable
    {
        [Header("Identidad de la tecla")]
        [Tooltip("Valor literal que enviará al controlador al pulsarse: " +
                 "\"0\"-\"9\", \"C\" (Clear) o \"E\" (Enter).")]
        [SerializeField] private string keyValue;

        [Header("Visual — Hover")]
        [Tooltip("Color del material al apuntar con el rayo.")]
        [SerializeField] private Color hoverColor = Color.yellow;

        [Header("Game Feel — Hundimiento mecánico (DOTween)")]
        [Tooltip("Magnitud y dirección del hundimiento en espacio local. " +
                 "Z positivo = se aleja de la cámara (hacia la pared). " +
                 "Ajusta el signo según la orientación del GameObject.")]
        [SerializeField] private Vector3 punchAxis = new Vector3(0f, 0f, 0.05f);

        [Tooltip("Duración total del efecto de pulsación (ida + vuelta elástica).")]
        [SerializeField] private float punchDuration = 0.2f;

        [Tooltip("Número de oscilaciones tras el impacto. 1 = rebote simple y seco.")]
        [SerializeField] private int punchVibrato = 1;

        [Tooltip("Elasticidad del rebote (0 = lineal, 1 = muy elástico).")]
        [SerializeField] private float punchElasticity = 0f;

        /// <summary>Valor que esta tecla envía al controlador al pulsarse.</summary>
        public string KeyValue => keyValue;

        private Action<string> _onKeyPress;

        private MeshRenderer _meshRenderer;
        private TMP_Text _tmpText;
        private Color _originalColor;

        private void Awake()
        {
            _tmpText = null;
            _meshRenderer = null;
            _originalColor = Color.white;

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
        }

        private void OnDisable()
        {
            // Cancelamos el tween activo y restauramos la posición para no dejar
            // la tecla hundida si el puzle se desactiva en mitad de una animación.
            transform.DOKill(complete: false);
            transform.localPosition = transform.localPosition; // fuerza estado limpio

            if (_tmpText != null)
                _tmpText.color = _originalColor;
            if (_meshRenderer != null)
                _meshRenderer.material.color = _originalColor;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }

        /// <summary>
        /// Inyecta el callback al que la tecla notificará su <see cref="keyValue"/>
        /// cuando el jugador la active.
        /// </summary>
        public void Setup(Action<string> onKeyPress)
        {
            _onKeyPress = onKeyPress;
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
        /// Notifica el valor de la tecla al controlador y dispara el efecto
        /// mecánico de hundimiento vía DOTween.
        /// </remarks>
        public void Interact()
        {
            if (_onKeyPress == null)
            {
                Debug.LogWarning(
                    $"[{nameof(ConsoleKey)}] Tecla '{keyValue}' activada sin callback " +
                    "(¿olvidaste llamar a Setup desde el controlador?).", this);
                return;
            }

            _onKeyPress.Invoke(keyValue);

            // DOKill(true) finaliza cualquier tween previo antes de iniciar el nuevo:
            // evita que pulsaciones rápidas acumulen tweens que se pisen entre sí.
            transform.DOKill(complete: true);
            transform.DOPunchPosition(punchAxis, punchDuration, punchVibrato, punchElasticity);
        }

        // ------------------------------------------------------------------ //
        //  Editor                                                              //
        // ------------------------------------------------------------------ //

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(keyValue))
                Debug.LogWarning(
                    $"[{nameof(ConsoleKey)}] keyValue vacío en '{name}'. " +
                    "Asigna \"0\"-\"9\", \"C\" o \"E\".", this);
        }
    }
}
