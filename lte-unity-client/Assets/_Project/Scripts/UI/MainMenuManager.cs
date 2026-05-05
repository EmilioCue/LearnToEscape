using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LearnToEscape.Content;
using LearnToEscape.Gameplay.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LearnToEscape.UI
{
    /// <summary>
    /// Greybox del Menú Principal: el jugador elige una de las 2 áreas
    /// (Matemáticas / Informática) y, en cascada, una de sus 5 temáticas
    /// antes de pedir la generación de la sala al backend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Catálogo de áreas y temas espejado del GDD. Las claves se muestran al
    /// jugador con tildes y mayúsculas; el backend las normaliza internamente
    /// (ver <c>PromptFactory.normalizeTopic</c>), por lo que NO hace falta
    /// duplicar aquí la versión sin acentos.
    /// </para>
    /// <para>
    /// La generación es asíncrona pero los listeners de Unity exigen
    /// <c>UnityAction</c> síncrona. Por eso el listener real es un
    /// <c>async void</c> envoltorio (<see cref="HandleGenerateClicked"/>) que
    /// captura cualquier excepción para evitar que reviente el bucle de Unity.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MainMenuManager : MonoBehaviour
    {
        /// <summary>
        /// Mapa Área → temáticas, en el orden exacto del GDD. Usamos
        /// <see cref="Dictionary{TKey, TValue}"/> con orden de inserción
        /// estable (.NET garantiza el orden de inserción al iterar).
        /// </summary>
        private static readonly Dictionary<string, string[]> TopicsByArea = new()
        {
            ["Matemáticas"] = new[]
            {
                "Álgebra",
                "Análisis Matemático",
                "Geometría y Topología",
                "Matemática Discreta",
                "Estadística y Probabilidad",
            },
            ["Informática"] = new[]
            {
                "Arquitectura de Computadores",
                "Ingeniería de Software",
                "Algoritmia y Estructuras de Datos",
                "Sistemas Operativos y Redes",
                "Inteligencia Artificial y Ciencia de Datos",
            },
        };

        [Header("Referencias 3D")]
        [Tooltip("El jugador en 3D que se activará tras generar la sala")]
        [SerializeField] private GameObject player3D;

        [Header("UI")]
        [SerializeField] private TMP_Dropdown areaDropdown;
        [SerializeField] private TMP_Dropdown topicDropdown;
        [SerializeField] private Button generateButton;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject mainMenuCanvas;

        [Header("Dependencias de juego")]
        [SerializeField] private RoomFlowManager roomManager;
        [SerializeField] private ContentGenerator contentGenerator;

        [Header("Parámetros de generación")]
        [Tooltip("Número de puzles por sala. El contrato actual del backend es FIJO=4.")]
        [SerializeField] private int puzzleCount = 4;

        [Tooltip("Dificultad pedagógica enviada al backend (easy / medium / hard).")]
        [SerializeField] private string difficulty = "easy";

        private void Start()
        {
            if (!ValidateInspectorReferences()) return;

            PopulateAreaDropdown();
            RefreshTopicDropdown(areaDropdown.value);

            areaDropdown.onValueChanged.AddListener(RefreshTopicDropdown);
            generateButton.onClick.AddListener(HandleGenerateClicked);

            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (areaDropdown != null)
                areaDropdown.onValueChanged.RemoveListener(RefreshTopicDropdown);
            if (generateButton != null)
                generateButton.onClick.RemoveListener(HandleGenerateClicked);
        }

        private bool ValidateInspectorReferences()
        {
            bool ok = true;
            ok &= AssertAssigned(areaDropdown, nameof(areaDropdown));
            ok &= AssertAssigned(topicDropdown, nameof(topicDropdown));
            ok &= AssertAssigned(generateButton, nameof(generateButton));
            ok &= AssertAssigned(roomManager, nameof(roomManager));
            ok &= AssertAssigned(contentGenerator, nameof(contentGenerator));
            return ok;
        }

        private bool AssertAssigned(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null) return true;
            Debug.LogError(
                $"[{nameof(MainMenuManager)}] '{fieldName}' no asignado en el Inspector.", this);
            return false;
        }

        private void PopulateAreaDropdown()
        {
            areaDropdown.ClearOptions();
            areaDropdown.AddOptions(new List<string>(TopicsByArea.Keys));
            areaDropdown.value = 0;
            areaDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Repuebla el <see cref="topicDropdown"/> con los temas del área que
        /// el jugador acaba de seleccionar. Llamada manualmente en arranque y
        /// suscrita a <c>onValueChanged</c> para refrescar en cascada.
        /// </summary>
        private void RefreshTopicDropdown(int areaIndex)
        {
            if (areaIndex < 0 || areaIndex >= areaDropdown.options.Count)
            {
                Debug.LogWarning(
                    $"[{nameof(MainMenuManager)}] Índice de área fuera de rango: {areaIndex}.", this);
                return;
            }

            string areaKey = areaDropdown.options[areaIndex].text;
            if (!TopicsByArea.TryGetValue(areaKey, out var topics))
            {
                Debug.LogError(
                    $"[{nameof(MainMenuManager)}] Área '{areaKey}' no encontrada en el catálogo.",
                    this);
                return;
            }

            topicDropdown.ClearOptions();
            topicDropdown.AddOptions(new List<string>(topics));
            topicDropdown.value = 0;
            topicDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Envoltorio síncrono para el listener del botón. Captura cualquier
        /// excepción del flujo asíncrono para que un fallo de red no se
        /// propague como <c>UnobservedTaskException</c>.
        /// </summary>
        private async void HandleGenerateClicked()
        {
            try
            {
                await OnGenerateClicked();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{nameof(MainMenuManager)}] Excepción al generar sala: " +
                    $"{ex.Message}\n{ex.StackTrace}", this);
                ResetUiToIdle();
            }
        }

        private async Task OnGenerateClicked()
        {
            if (topicDropdown.options.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(MainMenuManager)}] El dropdown de temas está vacío.", this);
                return;
            }

            string selectedTopic = topicDropdown.options[topicDropdown.value].text;
            Debug.Log(
                $"[{nameof(MainMenuManager)}] Solicitando sala. " +
                $"Tema='{selectedTopic}', dificultad='{difficulty}', puzzles={puzzleCount}.",
                this);

            generateButton.interactable = false;
            if (loadingPanel != null) loadingPanel.SetActive(true);

            RoomData room;
            try
            {
                room = await contentGenerator.GenerateRoom(selectedTopic, puzzleCount, difficulty);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{nameof(MainMenuManager)}] Backend falló al generar sala: {ex.Message}",
                    this);
                ResetUiToIdle();
                return;
            }

            if (room == null)
            {
                Debug.LogError(
                    $"[{nameof(MainMenuManager)}] GenerateRoom devolvió null " +
                    "(sala inválida o error de backend).", this);
                ResetUiToIdle();
                return;
            }

            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);

            if (player3D != null)
            {
                player3D.SetActive(true);
            }
            
            roomManager.InitializeRoom(room);
        }

        /// <summary>
        /// Devuelve la UI a su estado pre-clic: oculta el panel de carga y
        /// reactiva el botón. NO modifica los dropdowns para que el jugador
        /// conserve su selección y pueda reintentar sin re-elegir.
        /// </summary>
        private void ResetUiToIdle()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (generateButton != null) generateButton.interactable = true;
        }
    }
}
