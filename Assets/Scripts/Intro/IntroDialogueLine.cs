using System;
using UnityEngine;

namespace Castlevania2D.Intro
{
    public enum IntroSpeaker
    {
        Host,
        Woman
    }

    [Serializable]
    public sealed class IntroDialogueLine
    {
        [SerializeField] private IntroSpeaker speaker;
        [SerializeField] [TextArea(2, 4)] private string text;

        public IntroSpeaker Speaker => speaker;
        public string Text => text;

        public IntroDialogueLine()
        {
        }

        public IntroDialogueLine(IntroSpeaker speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }
}
