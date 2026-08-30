using System.Collections;
using Castlevania2D.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Runs one Hnefatafl match after both sides have placed the same stake.
    /// Attackers always move first; the player's side is random.
    /// </summary>
    public sealed class HnefataflGameController : MonoBehaviour
    {
        [SerializeField] private HnefataflBoardView boardView;
        [SerializeField] private Text statusText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject endMatchGroup;
        [SerializeField] private HnefataflBetPanel betPanel;
        [SerializeField] private float aiThinkDelay = 0.25f;
        [SerializeField] private float aiMoveDuration = 0.45f;
        [SerializeField] private int aiDepth = 3;

        private HnefataflBoardState board;
        private HnefataflAi ai;
        private HnefataflSide playerSide;
        private bool aiBusy;
        private bool matchStarted;
        private bool potSettled;
        private int liveStake;

        private void Awake()
        {
            ai = new HnefataflAi(aiDepth);
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(ReturnToShop);
            }

            if (betPanel != null)
            {
                betPanel.BetConfirmed += OnBetConfirmed;
            }
        }

        private void Start()
        {
            EnterBettingPhase();
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(ReturnToShop);
            }

            if (betPanel != null)
            {
                betPanel.BetConfirmed -= OnBetConfirmed;
            }
        }

        public void StartNewGame()
        {
            if (!matchStarted)
            {
                EnterBettingPhase();
                return;
            }

            StopAllCoroutines();
            aiBusy = false;

            playerSide = Random.value < 0.5f
                ? HnefataflSide.Attackers
                : HnefataflSide.Defenders;

            board = new HnefataflBoardState();
            board.SetupNewGame();
            Canvas.ForceUpdateCanvases();

            bool playerOpens = playerSide == HnefataflSide.Attackers;
            if (boardView != null)
            {
                boardView.BindBoard(board, playerSide);
                boardView.SetInputEnabled(playerOpens);
            }

            SetStatus(BuildTurnStatus());

            if (!playerOpens)
            {
                StartCoroutine(AiTurnRoutine());
            }
        }

        public void HandlePlayerMove(HnefataflMove move)
        {
            if (!matchStarted || aiBusy || board == null || board.Result != HnefataflGameResult.None)
            {
                return;
            }

            if (board.SideToMove != playerSide)
            {
                return;
            }

            if (!board.TryApplyMove(move))
            {
                boardView?.Refresh();
                return;
            }

            boardView?.CompletePlayerMove(move);
            if (board.Result != HnefataflGameResult.None)
            {
                OnGameOver();
                return;
            }

            SetStatus(BuildTurnStatus());
            StartCoroutine(AiTurnRoutine());
        }

        public void Wire(
            HnefataflBoardView view,
            Text status,
            Button restart,
            Button leave,
            GameObject endMatchRoot,
            HnefataflBetPanel stakes)
        {
            boardView = view;
            statusText = status;
            restartButton = restart;
            leaveButton = leave;
            endMatchGroup = endMatchRoot;
            betPanel = stakes;
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(ReturnToShop);
                leaveButton.onClick.AddListener(ReturnToShop);
            }

            if (betPanel != null)
            {
                betPanel.BetConfirmed -= OnBetConfirmed;
                betPanel.BetConfirmed += OnBetConfirmed;
            }

            if (endMatchGroup != null)
            {
                endMatchGroup.SetActive(true);
            }
        }

        private void OnBetConfirmed(int stake)
        {
            if (matchStarted || !HubTaflSession.TrySpend(stake))
            {
                return;
            }

            liveStake = stake;
            potSettled = false;
            matchStarted = true;
            betPanel?.LockPiles(stake);
            StartNewGame();
        }

        private void OnRestartClicked()
        {
            if (matchStarted && board != null && board.Result == HnefataflGameResult.None)
            {
                StartNewGame();
                return;
            }

            EnterBettingPhase();
        }

        private void EnterBettingPhase()
        {
            StopAllCoroutines();
            aiBusy = false;
            matchStarted = false;
            potSettled = false;
            liveStake = 0;
            board = new HnefataflBoardState();
            if (boardView != null)
            {
                boardView.BindBoard(board, HnefataflSide.Attackers);
                boardView.SetInputEnabled(false);
            }

            betPanel?.EnterBetting(HubTaflSession.Coins);
            SetStatus("Сделайте ставку, чтобы начать партию.");
        }

        private IEnumerator AiTurnRoutine()
        {
            aiBusy = true;
            boardView?.SetInputEnabled(false);
            SetStatus(playerSide == HnefataflSide.Attackers
                ? "Ход ИИ (защитники)..."
                : "Ход ИИ (атакующие)...");

            yield return new WaitForSecondsRealtime(aiThinkDelay);

            if (ai.TryChooseMove(board, out HnefataflMove move))
            {
                if (boardView != null)
                {
                    yield return boardView.PlayMoveAnimation(move, aiMoveDuration);
                }

                board.TryApplyMove(move);
                boardView?.Refresh();
            }

            aiBusy = false;

            if (board.Result != HnefataflGameResult.None)
            {
                OnGameOver();
                yield break;
            }

            boardView?.SetInputEnabled(true);
            SetStatus(BuildTurnStatus());
        }

        private void OnGameOver()
        {
            boardView?.SetInputEnabled(false);
            bool playerWon =
                (board.Result == HnefataflGameResult.AttackersWin
                 && playerSide == HnefataflSide.Attackers)
                || (board.Result == HnefataflGameResult.DefendersWin
                    && playerSide == HnefataflSide.Defenders);

            if (!potSettled && liveStake > 0)
            {
                potSettled = true;
                if (playerWon)
                {
                    HubTaflSession.AddCoins(liveStake * 2);
                }
            }

            string sideName = playerSide == HnefataflSide.Attackers ? "атакующие" : "защитники";
            if (playerWon)
            {
                SetStatus(liveStake > 0
                    ? $"Победа! Вы играли за {sideName}. +{liveStake} монет."
                    : $"Победа! Вы играли за {sideName}.");
            }
            else
            {
                SetStatus($"Поражение. Вы играли за {sideName}.");
            }
        }

        private string BuildTurnStatus()
        {
            string you = playerSide == HnefataflSide.Attackers ? "атакующие" : "защитники";
            return $"Вы: {you}. Ваш ход — зажмите и перетащите фигуру.";
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        private void ReturnToShop()
        {
            HubTaflSession.MarkReturnToShop();
            HubSceneFadeLoad.Load(HubTaflSession.HubSceneName);
        }
    }
}
