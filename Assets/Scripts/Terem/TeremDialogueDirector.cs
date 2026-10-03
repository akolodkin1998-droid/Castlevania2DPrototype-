using System.Collections;
using Castlevania2D.Hub;
using Castlevania2D.Input;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Terem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DialogueBoxPlacement2D))]
    public sealed class TeremDialogueDirector : MonoBehaviour
    {
        public const string SceneName = "Terem";
        public const string NextSceneName = "Hub";
        private const string VisitorPortraitResource = "UI/Dialogue/IntroHostPortrait";
        private const string KnyazPortraitResource = "UI/Dialogue/TeremKnyazPortrait";

        [SerializeField] private TeremVisitorNpc2D visitor;
        [SerializeField] private TeremKnyazNpc2D knyaz;
        [SerializeField] [Min(0f)] private float readyTimeout = 25f;
        [SerializeField]
        private TeremDialogueLine[] lines =
        {
            new TeremDialogueLine(
                TeremSpeaker.Visitor,
                "Княже, припал я к твоему порогу не воином — просителем. Жена лежит, и ночь уже считает её вздохи. Всё добро ушло на лекарства. Недуг сильнее."),
            new TeremDialogueLine(
                TeremSpeaker.Visitor,
                "Я бросил дружину самовольно. Знаю вину свою. Молю не о себе — о ней одной. Карай меня, только не оставь её умирать."),
            new TeremDialogueLine(
                TeremSpeaker.Knyaz,
                "Довольно. Обиды у меня нет. Ушёл — значит, сердце было крепче приказа. Ныне ты здесь. Этим и закрыт тот счёт."),
            new TeremDialogueLine(
                TeremSpeaker.Knyaz,
                "Жену возьмём на лечение. Мои лекари сделают всё, что умеет людская наука. Но снадобью нужны травы, каких на торгу не сыщешь."),
            new TeremDialogueLine(
                TeremSpeaker.Knyaz,
                "Первое — листок аленького цветка. Без него зелье мертво. Достать его пойдёшь ты сам. Это твоя дорога, не чужая."),
            new TeremDialogueLine(
                TeremSpeaker.Knyaz,
                "Доспехи твои целы и сохранны. Ждут тебя, как ждал твой щит. Оденься и ступай. Пока она дышит — у тебя есть время.")
        };

        private int lineIndex;
        private bool finishing;
        private bool lineOpen;
        private Sprite visitorPortrait;
        private Sprite knyazPortrait;

        private void Awake()
        {
            if (visitor == null)
            {
                visitor = FindFirstObjectByType<TeremVisitorNpc2D>();
            }

            if (knyaz == null)
            {
                knyaz = FindFirstObjectByType<TeremKnyazNpc2D>();
            }

            if (lines == null || lines.Length == 0)
            {
                lines = CreateDefaultLines();
            }

            visitorPortrait = Resources.Load<Sprite>(VisitorPortraitResource);
            knyazPortrait = Resources.Load<Sprite>(KnyazPortraitResource);
            GameplayInputLock.IsLocked = true;
        }

        private void OnEnable()
        {
            DialogueBoxUI.TypingStarted += OnTypingStarted;
            DialogueBoxUI.TypingFinished += OnTypingFinished;
        }

        private void OnDisable()
        {
            DialogueBoxUI.TypingStarted -= OnTypingStarted;
            DialogueBoxUI.TypingFinished -= OnTypingFinished;
        }

        private void Start()
        {
            if (knyaz != null)
            {
                knyaz.PlayListen();
            }

            StartCoroutine(BeginRoutine());
        }

        private void Update()
        {
            if (finishing)
            {
                return;
            }

            if (lineOpen && !DialogueBoxUI.IsOpen)
            {
                Finish();
            }
        }

        private IEnumerator BeginRoutine()
        {
            float waited = 0f;
            while (visitor != null && !visitor.IsReadyForDialogue && waited < readyTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (lines == null || lineIndex >= lines.Length || lines[lineIndex] == null)
            {
                Finish();
                return;
            }

            TeremDialogueLine line = lines[lineIndex];
            bool last = lineIndex == lines.Length - 1;
            bool knyazLine = line.Speaker == TeremSpeaker.Knyaz;
            lineOpen = true;

            DialogueBoxUI.Ensure().Open(
                knyazLine ? "Князь" : string.Empty,
                string.IsNullOrEmpty(line.Text) ? string.Empty : line.Text,
                new[] { last ? "В путь" : "Далее" },
                OnChosen,
                knyazLine ? knyazPortrait : visitorPortrait,
                typewriter: true);
        }

        private void OnTypingStarted()
        {
            if (lines == null || lineIndex < 0 || lineIndex >= lines.Length || lines[lineIndex] == null)
            {
                return;
            }

            if (lines[lineIndex].Speaker == TeremSpeaker.Knyaz)
            {
                if (visitor != null)
                {
                    visitor.PlayIdle();
                }

                if (knyaz != null)
                {
                    knyaz.PlaySpeak();
                }

                return;
            }

            if (knyaz != null)
            {
                knyaz.PlayListen();
            }

            if (visitor != null)
            {
                visitor.PlaySpeak();
            }
        }

        private void OnTypingFinished()
        {
            if (lines == null || lineIndex < 0 || lineIndex >= lines.Length || lines[lineIndex] == null)
            {
                return;
            }

            if (lines[lineIndex].Speaker == TeremSpeaker.Visitor)
            {
                if (visitor != null)
                {
                    visitor.PlayIdle();
                }

                return;
            }

            if (knyaz != null)
            {
                knyaz.PlayIdle();
            }
        }

        private void OnChosen(int _)
        {
            lineOpen = false;
            lineIndex++;
            ShowCurrent();
        }

        private void Finish()
        {
            if (finishing)
            {
                return;
            }

            finishing = true;
            lineOpen = false;
            DialogueBoxUI.CloseIfOpen();
            GameplayInputLock.IsLocked = true;
            HubSceneFadeLoad.Load(NextSceneName);
        }

        private static TeremDialogueLine[] CreateDefaultLines()
        {
            return new[]
            {
                new TeremDialogueLine(
                    TeremSpeaker.Visitor,
                    "Княже, припал я к твоему порогу не воином — просителем. Жена лежит, и ночь уже считает её вздохи. Всё добро ушло на лекарства. Недуг сильнее."),
                new TeremDialogueLine(
                    TeremSpeaker.Visitor,
                    "Я бросил дружину самовольно. Знаю вину свою. Молю не о себе — о ней одной. Карай меня, только не оставь её умирать."),
                new TeremDialogueLine(
                    TeremSpeaker.Knyaz,
                    "Довольно. Обиды у меня нет. Ушёл — значит, сердце было крепче приказа. Ныне ты здесь. Этим и закрыт тот счёт."),
                new TeremDialogueLine(
                    TeremSpeaker.Knyaz,
                    "Жену возьмём на лечение. Мои лекари сделают всё, что умеет людская наука. Но снадобью нужны травы, каких на торгу не сыщешь."),
                new TeremDialogueLine(
                    TeremSpeaker.Knyaz,
                    "Первое — листок аленького цветка. Без него зелье мертво. Достать его пойдёшь ты сам. Это твоя дорога, не чужая."),
                new TeremDialogueLine(
                    TeremSpeaker.Knyaz,
                    "Доспехи твои целы и сохранны. Ждут тебя, как ждал твой щит. Оденься и ступай. Пока она дышит — у тебя есть время.")
            };
        }
    }
}
