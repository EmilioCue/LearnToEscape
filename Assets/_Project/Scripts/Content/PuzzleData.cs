using System;

namespace LearnToEscape.Content
{
    [Serializable]
    public class PuzzleData
    {
        public string id;
        public string name;
        public string description;
        public string type;
        public string solution;
        public string[] hints;
        public string difficulty;
        public string[] requiredItems;
    }
}
