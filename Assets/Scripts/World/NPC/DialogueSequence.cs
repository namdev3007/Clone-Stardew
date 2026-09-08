using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.NPC
{
    [CreateAssetMenu(fileName = "Dialogue Sequence", menuName = "NPC/Dialogue Sequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        public enum Speaker
        {
            Player,
            Npc
        }

        [Serializable]
        public sealed class Line
        {
            public Speaker speaker;
            public string localizationKey;
            [TextArea(2, 6)] public string english;
            [TextArea(2, 6)] public string vietnamese;

            public string GetText(bool useVietnamese)
            {
                if (useVietnamese && !string.IsNullOrWhiteSpace(vietnamese))
                    return vietnamese;
                return english ?? string.Empty;
            }
        }

        [SerializeField] private string sequenceId;
        [SerializeField] private List<Line> lines = new List<Line>();

        public string SequenceId => sequenceId;
        public IReadOnlyList<Line> Lines => lines;

        public void Configure(string id, IEnumerable<Line> dialogueLines)
        {
            sequenceId = id;
            lines = new List<Line>(dialogueLines ?? Array.Empty<Line>());
        }
    }
}
