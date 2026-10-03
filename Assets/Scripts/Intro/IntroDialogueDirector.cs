using System.Collections;
using Castlevania2D.Hub;
using Castlevania2D.Input;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Intro
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DialogueBoxPlacement2D))]
    public sealed class IntroDialogueDirector : MonoBehaviour
    {
        public const string SceneName = "Intro";
        public const string NextSceneName = "Terem";
        private const string HostPortraitResource = "UI/Dialogue/IntroHostPortrait";
        private const string WomanPortraitResource = "UI/Dialogue/IntroWomanPortrait";

        [SerializeField] private IntroSeatedNpc2D host;
        [SerializeField] private IntroWomanNpc2D woman;
        [SerializeField] [Min(0f)] private float preDialogueDelay = 1.15f;
        [SerializeField]
        private IntroDialogueLine[] lines =
        {
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Долго молчал я, как молчит дом, когда в нём гаснет огонь. Ныне же слово само просится наружу."),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Она лежит, и дыхание её тонко, словно нить, которую вот-вот перережет ночь. Смотрю — и вижу жену, которую клялся беречь и не сумел защитить."),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Всё, что имели, отдали лекарям. Золото ушло первым. Потом — зерно. Беда не стала ждать и вымела сундук до дна."),
            new IntroDialogueLine(IntroSpeaker.Woman, "Кхе-Кхе"),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Травы, настои, заговоры бабок, кровь пиявок, дым от смолы… Пробовали всё, что знает людская жалость. И всё оказалось слабее её недуга."),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Лекарства больше не берут. Платить уже нечем. Осталась одна дорога — не к знахарю, а на поклон."),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "В город. К царю. Пасть в пыль у его порога и молить о милости. Да смягчится сердце его хоть ради одного её вздоха."),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Только милость царя недаром даётся. Он помнит. Я ушёл из дружины самовольно. Не пал в бою, не был отпущен — сам бросил строй и бежал домой, ибо сердце звало сильнее знамени."),
            new IntroDialogueLine(IntroSpeaker.Woman, "Кхе-Кхе"),
            new IntroDialogueLine(
                IntroSpeaker.Host,
                "Ныне иду назад не воином, а просителем. Если простит — она, быть может, ещё увидит весну. Если нет — пусть кара падёт на меня одного. Ей и так довольно этой муки.")
        };

        private int lineIndex;
        private bool finishing;
        private bool lineOpen;
        private Sprite hostPortrait;
        private Sprite womanPortrait;

        private void Awake()
        {
            if (host == null)
            {
                host = FindFirstObjectByType<IntroSeatedNpc2D>();
            }

            if (woman == null)
            {
                woman = FindFirstObjectByType<IntroWomanNpc2D>();
            }

            hostPortrait = Resources.Load<Sprite>(HostPortraitResource);
            womanPortrait = Resources.Load<Sprite>(WomanPortraitResource);
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
            if (host != null)
            {
                host.PlayIdle();
            }

            if (woman != null)
            {
                woman.PlayIdle();
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
            if (preDialogueDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(preDialogueDelay);
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

            IntroDialogueLine line = lines[lineIndex];
            bool last = lineIndex == lines.Length - 1;
            bool womanLine = line.Speaker == IntroSpeaker.Woman;
            lineOpen = true;

            if (womanLine && woman != null)
            {
                woman.PlayCough();
            }

            if (!womanLine && woman != null && !woman.IsCoughing)
            {
                woman.PlayIdle();
            }

            DialogueBoxUI.Ensure().Open(
                string.Empty,
                string.IsNullOrEmpty(line.Text) ? string.Empty : line.Text,
                new[] { last ? "В путь" : "Далее" },
                OnChosen,
                womanLine ? womanPortrait : hostPortrait,
                typewriter: true);
        }

        private void OnTypingStarted()
        {
            if (lines == null || lineIndex < 0 || lineIndex >= lines.Length || lines[lineIndex] == null)
            {
                return;
            }

            if (lines[lineIndex].Speaker == IntroSpeaker.Woman)
            {
                if (host != null)
                {
                    host.PlayIdle();
                }

                if (woman != null)
                {
                    woman.PlayCough();
                }

                return;
            }

            if (host != null)
            {
                host.PlaySpeak();
            }
        }

        private void OnTypingFinished()
        {
            if (host != null)
            {
                host.PlayIdle();
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

#if UNITY_EDITOR
        public void EditorAssignHost(IntroSeatedNpc2D npc)
        {
            host = npc;
        }

        public void EditorAssignWoman(IntroWomanNpc2D npc)
        {
            woman = npc;
        }
#endif
    }
}
