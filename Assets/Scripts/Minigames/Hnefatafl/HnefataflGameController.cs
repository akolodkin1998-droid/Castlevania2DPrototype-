using System.Collections;
using Castlevania2D.Hub;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Runs one Hnefatafl match: random side for player, player moves first, medium AI replies.
    /// </summary>
    public sealed class HnefataflGameController : MonoBehaviour
    {
        [SerializeField] private HnefataflBoardView boardView;
        [SerializeField] private Text statusText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject endMatchGroup;
        [SerializeField] private float aiThinkDelay = 0.25f;
        [SerializeField] private float aiMoveDuration = 0.45f;
        [SerializeField] private int aiDepth = 3;

        private HnefataflBoardState board;
        private HnefataflAi ai;
        private HnefataflSide playerSide;
        private bool aiBusy;

        private void Awake()
        {
            ai = new HnefataflAi(aiDepth);
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(StartNewGame);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(ReturnToShop);
            }

            SetEndMatchVisible(false);
        }

        private void Start()
        {
            StartNewGame();
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(StartNewGame);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(ReturnToShop);
            }
        }

        public void StartNewGame()
        {
            StopAllCoroutines();
            aiBusy = false;

            playerSide = Random.value < 0.5f
                ? HnefataflSide.Attackers
                : HnefataflSide.Defenders;

            board = new HnefataflBoardState();
            // Player always moves first, regardless of side.
            board.SetupNewGame(playerSide);
            Canvas.ForceUpdateCanvases();

            if (boardView != null)
            {
                boardView.BindBoard(board, playerSide);
                boardView.SetInputEnabled(true);
            }

            SetEndMatchVisible(false);

            SetStatus(BuildTurnStatus());
        }

        public void HandlePlayerMove(HnefataflMove move)
        {
            if (aiBusy || board == null || board.Result != HnefataflGameResult.None)
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

            string sideName = playerSide == HnefataflSide.Attackers ? "атакующие" : "защитники";
            SetStatus(playerWon
                ? $"Победа! Вы играли за {sideName}."
                : $"Поражение. Вы играли за {sideName}.");

            SetEndMatchVisible(true);
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

        private void SetEndMatchVisible(bool visible)
        {
            if (endMatchGroup != null)
            {
                endMatchGroup.SetActive(visible);
                return;
            }

            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(visible);
            }

            if (leaveButton != null)
            {
                leaveButton.gameObject.SetActive(visible);
            }
        }

        public void Wire(
            HnefataflBoardView view,
            Text status,
            Button restart,
            Button leave,
            GameObject endMatchRoot)
        {
            boardView = view;
            statusText = status;
            restartButton = restart;
            leaveButton = leave;
            endMatchGroup = endMatchRoot;
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(StartNewGame);
                restartButton.onClick.AddListener(StartNewGame);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(ReturnToShop);
                leaveButton.onClick.AddListener(ReturnToShop);
            }

            SetEndMatchVisible(false);
        }
    }
}
