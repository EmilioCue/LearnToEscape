using System;

namespace LearnToEscape.Content
{
    /// <summary>
    /// Contrato estricto de sala educativa: 4 puzzles predefinidos, en el mismo
    /// orden y con los mismos nombres de campo que emite el backend Java.
    /// </summary>
    /// <remarks>
    /// Los nombres de campo usan <c>snake_case</c> para que Newtonsoft.Json los
    /// mapee 1:1 con el JSON del backend sin necesidad de <c>[JsonProperty]</c>.
    /// Todas las clases anidadas son <see cref="SerializableAttribute"/> para
    /// permitir también su uso como datos en el Inspector de Unity si hiciera falta.
    /// </remarks>
    [Serializable]
    public class RoomData
    {
        public string theme;

        public Puzzle1Matrix puzzle1_matrix;
        public Puzzle2Router puzzle2_router;
        public Puzzle3Link puzzle3_link;
        public Puzzle4Console puzzle4_console;
    }

    /// <summary>Puzzle de clasificación en exactamente 2 categorías.</summary>
    [Serializable]
    public class Puzzle1Matrix
    {
        /// <summary>Las dos etiquetas de categoría (índices 0 y 1).</summary>
        public string[] categories;

        /// <summary>Ítems a clasificar; cada uno referencia una categoría por índice.</summary>
        public MatrixItem[] items;
    }

    /// <summary>Ítem del puzzle de matriz (texto + índice de categoría 0 o 1).</summary>
    [Serializable]
    public class MatrixItem
    {
        public string name;
        public int categoryIndex;
    }

    /// <summary>Puzzle de secuencia ordenada de exactamente 5 pasos.</summary>
    [Serializable]
    public class Puzzle2Router
    {
        public string[] sequence;
    }

    /// <summary>Puzzle de emparejado concepto-definición.</summary>
    [Serializable]
    public class Puzzle3Link
    {
        public LinkPair[] pairs;
    }

    /// <summary>Pareja concepto-definición del puzzle 3.</summary>
    [Serializable]
    public class LinkPair
    {
        public string concept;
        public string definition;
    }

    /// <summary>
    /// Consola final: pregunta deductiva, razonamiento Chain-of-Thought y PIN.
    /// </summary>
    /// <remarks>
    /// El orden de los campos replica el del JSON emitido por el backend
    /// (<c>deductionQuestion</c> → <c>stepByStepReasoning</c> → <c>pin</c>),
    /// que a su vez fuerza al LLM a razonar antes de "escupir" el PIN.
    /// El campo <see cref="stepByStepReasoning"/> es la traza interna del
    /// modelo (Chain of Thought): NO debe mostrarse al jugador, pero permite
    /// al diseñador auditar que el PIN se deduce realmente de los puzles 1-3.
    /// </remarks>
    [Serializable]
    public class Puzzle4Console
    {
        /// <summary>Pregunta que guía al jugador a deducir el PIN.</summary>
        public string deductionQuestion;

        /// <summary>
        /// Razonamiento paso a paso (Chain of Thought) que justifica cada uno
        /// de los 4 dígitos del PIN a partir de los puzles anteriores.
        /// Uso interno (debug / Game Master); no se renderiza en escena.
        /// </summary>
        public string stepByStepReasoning;

        /// <summary>Código numérico de EXACTAMENTE 4 dígitos.</summary>
        public string pin;
    }
}
