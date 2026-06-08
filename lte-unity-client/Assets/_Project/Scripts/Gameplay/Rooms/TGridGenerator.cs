using LearnToEscape.Gameplay.Puzzles;
using UnityEngine;

namespace LearnToEscape.Gameplay.Rooms
{
    /// <summary>
    /// Generador PCG de la sala en planta de "T".
    /// Instancia los 4 prefabs de puzle en los Sockets predefinidos y entrega
    /// los controladores al <see cref="RoomFlowManager"/> para que arranque la
    /// sala en el mismo frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los Sockets son <see cref="Transform"/> vacíos colocados manualmente en
    /// la escena que actúan como anclas de posición/rotación. El generador no
    /// conoce la geometría: solo sabe dónde colocar cada puzle.
    /// </para>
    /// <para>
    /// Los Puzles 1, 2 y 3 se asignan a los tres nichos variables
    /// (<c>leftNiche</c>, <c>rightNiche</c>, <c>bottomNiche</c>) en orden
    /// aleatorio usando Fisher-Yates, garantizando una distribución uniforme
    /// en cada partida. El Puzle 4 (Console/salida) siempre se instancia en
    /// <c>topNiche</c>.
    /// </para>
    /// <para>
    /// Orden de ejecución garantizado:
    /// <c>RoomFlowManager.Awake</c> → <c>TGridGenerator.Start</c>.
    /// De este modo, <see cref="RoomFlowManager.Instance"/> siempre está
    /// disponible cuando se llama a
    /// <see cref="RoomFlowManager.SetPuzzlesAndInitialize"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class TGridGenerator : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Prefabs de puzle
        // ─────────────────────────────────────────────────────────────────────

        [Header("Prefabs de Puzle")]
        [Tooltip("Prefab del Puzle 1 (Matrix). Debe contener un componente IPuzzleController.")]
        [SerializeField] private GameObject puzzle1Prefab;

        [Tooltip("Prefab del Puzle 2 (Router). Debe contener un componente IPuzzleController.")]
        [SerializeField] private GameObject puzzle2Prefab;

        [Tooltip("Prefab del Puzle 3 (Link). Debe contener un componente IPuzzleController.")]
        [SerializeField] private GameObject puzzle3Prefab;

        [Tooltip("Prefab del Puzle 4 (Console / salida). Siempre se instancia en topNiche.")]
        [SerializeField] private GameObject puzzle4Prefab;

        // ─────────────────────────────────────────────────────────────────────
        // Placeholders de entorno (Kenney assets — para uso futuro)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Entorno (placeholders — uso futuro)")]
        [Tooltip("Prefab de loseta de suelo (Kenney).")]
        [SerializeField] private GameObject floorTilePrefab;

        [Tooltip("Prefab de panel de pared (Kenney).")]
        [SerializeField] private GameObject wallPanelPrefab;

        // ─────────────────────────────────────────────────────────────────────
        // Sockets de posicionamiento
        // ─────────────────────────────────────────────────────────────────────

        [Header("Sockets de la planta en T")]
        [Tooltip("Nicho izquierdo del travesaño horizontal — puzles aleatorios 1-3.")]
        [SerializeField] private Transform leftNiche;

        [Tooltip("Nicho derecho del travesaño horizontal — puzles aleatorios 1-3.")]
        [SerializeField] private Transform rightNiche;

        [Tooltip("Nicho inferior del travesaño horizontal — puzles aleatorios 1-3.")]
        [SerializeField] private Transform bottomNiche;

        [Tooltip("Nicho superior del palo vertical — siempre el Puzle 4 (Console/salida).")]
        [SerializeField] private Transform topNiche;

        // ─────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (!ValidateDependencies()) return;

            // Barajar los 3 nichos variables con Fisher-Yates.
            Transform[] variableNiches = { leftNiche, rightNiche, bottomNiche };
            Shuffle(variableNiches);

            // Instanciar P1, P2, P3 en los nichos barajados; P4 siempre en topNiche.
            GameObject p1Instance = InstantiateAtSocket(puzzle1Prefab, variableNiches[0]);
            GameObject p2Instance = InstantiateAtSocket(puzzle2Prefab, variableNiches[1]);
            GameObject p3Instance = InstantiateAtSocket(puzzle3Prefab, variableNiches[2]);
            GameObject p4Instance = InstantiateAtSocket(puzzle4Prefab, topNiche);

            if (p1Instance == null || p2Instance == null ||
                p3Instance == null || p4Instance == null) return;

            // Extraer los IPuzzleController de cada instancia.
            IPuzzleController c1 = GetControllerFrom(p1Instance, 1);
            IPuzzleController c2 = GetControllerFrom(p2Instance, 2);
            IPuzzleController c3 = GetControllerFrom(p3Instance, 3);
            IPuzzleController c4 = GetControllerFrom(p4Instance, 4);

            if (c1 == null || c2 == null || c3 == null || c4 == null) return;

            // Delegar la inicialización al RoomFlowManager.
            RoomFlowManager.Instance.SetPuzzlesAndInitialize(c1, c2, c3, c4);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia <paramref name="prefab"/> en la posición y rotación del
        /// <paramref name="socket"/>, convirtiéndolo en hijo del socket para
        /// mantener la jerarquía limpia.
        /// </summary>
        private static GameObject InstantiateAtSocket(GameObject prefab, Transform socket)
        {
            return Instantiate(prefab, socket.position, socket.rotation, socket);
        }

        /// <summary>
        /// Obtiene el primer componente <see cref="IPuzzleController"/> del
        /// <paramref name="instance"/>. Loguea un error con el número de slot si
        /// no existe y devuelve <c>null</c>.
        /// </summary>
        private IPuzzleController GetControllerFrom(GameObject instance, int slot)
        {
            var controller = instance.GetComponent<IPuzzleController>();
            if (controller == null)
            {
                Debug.LogError(
                    $"[TGridGenerator] La instancia del Puzle {slot} " +
                    $"('{instance.name}') no tiene ningún componente IPuzzleController.",
                    instance);
            }
            return controller;
        }

        /// <summary>
        /// Algoritmo Fisher-Yates in-place sobre un array de cualquier tipo.
        /// Usa <see cref="Random.Range"/> de Unity para integración correcta
        /// con la semilla del motor.
        /// </summary>
        private static void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        /// <summary>
        /// Verifica en caliente que todos los campos obligatorios están asignados.
        /// Devuelve <c>false</c> y loguea errores acumulados si falta alguno.
        /// </summary>
        private bool ValidateDependencies()
        {
            bool valid = true;

            valid &= RequireField(puzzle1Prefab,  nameof(puzzle1Prefab));
            valid &= RequireField(puzzle2Prefab,  nameof(puzzle2Prefab));
            valid &= RequireField(puzzle3Prefab,  nameof(puzzle3Prefab));
            valid &= RequireField(puzzle4Prefab,  nameof(puzzle4Prefab));
            valid &= RequireField(leftNiche,      nameof(leftNiche));
            valid &= RequireField(rightNiche,     nameof(rightNiche));
            valid &= RequireField(bottomNiche,    nameof(bottomNiche));
            valid &= RequireField(topNiche,       nameof(topNiche));

            if (RoomFlowManager.Instance == null)
            {
                Debug.LogError(
                    "[TGridGenerator] RoomFlowManager.Instance es null. " +
                    "Asegúrate de que RoomFlowManager está en la escena y su Awake() " +
                    "se ejecuta antes que el Start() de TGridGenerator.", this);
                valid = false;
            }

            return valid;
        }

        private bool RequireField(Object field, string fieldName)
        {
            if (field != null) return true;

            Debug.LogError(
                $"[TGridGenerator] El campo '{fieldName}' no está asignado en el Inspector.",
                this);
            return false;
        }
    }
}
