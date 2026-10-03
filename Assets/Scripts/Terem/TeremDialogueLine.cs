using System;
using UnityEngine;

namespace Castlevania2D.Terem
{
    public enum TeremSpeaker
    {
        Visitor,
        Knyaz
    }

    [Serializable]
    public sealed class TeremDialogueLine
    {
        [SerializeField] private TeremSpeaker speaker;
        [SerializeField] [TextArea(2, 4)] private string text;

        public TeremSpeaker Speaker => speaker;
        public string Text => text;

        public TeremDialogueLine()
        {
        }

        public TeremDialogueLine(TeremSpeaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }
}
