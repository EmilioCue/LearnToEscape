using System;

namespace LearnToEscape.Content
{
    [Serializable]
    public class RoomData
    {
        public string id;
        public string theme;
        public string name;
        public string description;
        public string narrativeIntro;
        public int timeLimitMinutes;
        public PuzzleData[] puzzles;
    }
}
