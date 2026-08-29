using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Renders the 9x9 board and pieces. Drag a piece onto a legal cell to move.
    /// </summary>
    public sealed class HnefataflBoardView : MonoBehaviour
    {
        private const float TextureWidth = 512f;
        private const float TextureHeight = 512f;
        private const float GridLeft = 71f;
        private const float GridTop = 71f;
        private const float GridRight = 438f;
        private const float GridBottom = 440f;

        [SerializeField] private RectTransform boardRect;
        [SerializeField] private Image boardImage;
        [SerializeField] private RectTransform piecesRoot;
        [SerializeField] private float pieceScale = 0.81f;

        private readonly Dictionary<HnefataflCoord, HnefataflPieceView> pieces =
            new Dictionary<HnefataflCoord, HnefataflPieceView>(32);

        private Sprite attackerSpriteA;
        private Sprite attackerSpriteB;
        private Sprite defenderSpriteA;
        private Sprite defenderSpriteB;
        private Sprite kingSprite;
        private HnefataflBoardState board;
        private HnefataflSide playerSide;
        private bool inputEnabled = true;
        private HnefataflPieceView dragging;
        private HnefataflCoord dragFrom;
        private Vector2 dragOffset;
        private Action<HnefataflMove> onPlayerMove;

        public void Configure(
            Sprite boardSprite,
            Sprite attackerA,
            Sprite attackerB,
            Sprite defenderA,
            Sprite defenderB,
            Sprite king,
            Action<HnefataflMove> playerMoveHandler)
        {
            attackerSpriteA = attackerA;
            attackerSpriteB = attackerB != null ? attackerB : attackerA;
            defenderSpriteA = defenderA;
            defenderSpriteB = defenderB != null ? defenderB : defenderA;
            kingSprite = king;
            onPlayerMove = playerMoveHandler;

            if (boardImage != null && boardSprite != null)
            {
                boardImage.sprite = boardSprite;
                boardImage.preserveAspect = true;
                boardImage.type = Image.Type.Simple;
                boardImage.useSpriteMesh = false;
            }

            LayoutPlayableGrid();
        }

        public void BindBoard(HnefataflBoardState state, HnefataflSide humanSide)
        {
            board = state;
            playerSide = humanSide;
            RebuildPieces();
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled && dragging != null)
            {
                CancelDrag();
            }
        }

        public void Refresh()
        {
            SyncWithBoard();
        }

        public void CompletePlayerMove(HnefataflMove move)
        {
            if (pieces.TryGetValue(move.From, out HnefataflPieceView view))
            {
                pieces.Remove(move.From);
                pieces[move.To] = view;
                SnapPiece(view, move.To);
            }

            SyncWithBoard();
        }

        public IEnumerator PlayMoveAnimation(HnefataflMove move, float duration)
        {
            LayoutPlayableGrid();
            if (!pieces.TryGetValue(move.From, out HnefataflPieceView view) || view.Rect == null)
            {
                RebuildPieces();
                yield break;
            }

            pieces.Remove(move.From);
            pieces[move.To] = view;
            view.Rect.SetAsLastSibling();

            Vector2 start = CoordToLocal(move.From);
            Vector2 end = CoordToLocal(move.To);
            view.Rect.anchoredPosition = start;
            Vector3 restScale = Vector3.one;
            float time = Mathf.Max(0.08f, duration);
            float elapsed = 0f;

            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / time);
                float eased = 1f - ((1f - t) * (1f - t));
                view.Rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
                float pulse = 1f + (0.12f * Mathf.Sin(t * Mathf.PI));
                view.Rect.localScale = restScale * pulse;
                yield return null;
            }

            view.Rect.anchoredPosition = end;
            view.Rect.localScale = restScale;
        }

        public void Wire(RectTransform board, Image image, RectTransform piecesLayer)
        {
            boardRect = board;
            boardImage = image;
            piecesRoot = piecesLayer;
            LayoutPlayableGrid();
        }

        private void Update()
        {
            if (!inputEnabled || board == null || board.Result != HnefataflGameResult.None)
            {
                return;
            }

            if (board.SideToMove != playerSide)
            {
                return;
            }

            if (dragging != null)
            {
                FollowPointer(dragging.Rect);
                if (UnityEngine.Input.GetMouseButtonUp(0))
                {
                    TryDrop();
                }

                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TryBeginDrag();
            }
        }

        private void TryBeginDrag()
        {
            if (!TryScreenToCoord(UnityEngine.Input.mousePosition, out HnefataflCoord coord))
            {
                return;
            }

            if (!pieces.TryGetValue(coord, out HnefataflPieceView piece))
            {
                return;
            }

            HnefataflPieceKind kind = board.Get(coord);
            if (!HnefataflBoardState.BelongsToSide(kind, playerSide))
            {
                return;
            }

            dragging = piece;
            dragFrom = coord;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                piecesRoot,
                UnityEngine.Input.mousePosition,
                null,
                out Vector2 local);
            dragOffset = piece.Rect.anchoredPosition - local;
            piece.Rect.SetAsLastSibling();
        }

        private void FollowPointer(RectTransform pieceRect)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                piecesRoot,
                UnityEngine.Input.mousePosition,
                null,
                out Vector2 local);
            pieceRect.anchoredPosition = local + dragOffset;
        }

        private void TryDrop()
        {
            if (dragging == null)
            {
                return;
            }

            HnefataflPieceView piece = dragging;
            dragging = null;

            if (!TryScreenToCoord(UnityEngine.Input.mousePosition, out HnefataflCoord to))
            {
                SnapPiece(piece, dragFrom);
                return;
            }

            var move = new HnefataflMove(dragFrom, to);
            if (board == null || !board.IsLegalMove(move))
            {
                SnapPiece(piece, dragFrom);
                return;
            }

            onPlayerMove?.Invoke(move);
        }

        private void CancelDrag()
        {
            if (dragging == null)
            {
                return;
            }

            SnapPiece(dragging, dragFrom);
            dragging = null;
        }

        private void RebuildPieces()
        {
            ClearPieces();
            LayoutPlayableGrid();
            if (board == null || piecesRoot == null)
            {
                return;
            }

            for (int row = 0; row < HnefataflBoardState.Size; row++)
            {
                for (int col = 0; col < HnefataflBoardState.Size; col++)
                {
                    HnefataflPieceKind kind = board[row, col];
                    if (kind == HnefataflPieceKind.None)
                    {
                        continue;
                    }

                    var coord = new HnefataflCoord(row, col);
                    Sprite sprite = SpriteFor(kind, coord);
                    if (sprite == null)
                    {
                        continue;
                    }

                    HnefataflPieceView view = CreatePiece(sprite, kind);
                    SnapPiece(view, coord);
                    pieces[coord] = view;
                }
            }
        }

        private void SyncWithBoard()
        {
            LayoutPlayableGrid();
            if (board == null)
            {
                return;
            }

            var stale = new List<HnefataflCoord>();
            foreach (KeyValuePair<HnefataflCoord, HnefataflPieceView> pair in pieces)
            {
                HnefataflPieceKind expected = board.Get(pair.Key);
                if (expected == HnefataflPieceKind.None || expected != pair.Value.Kind)
                {
                    stale.Add(pair.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                if (pieces.TryGetValue(stale[i], out HnefataflPieceView view) && view.Rect != null)
                {
                    Destroy(view.Rect.gameObject);
                }

                pieces.Remove(stale[i]);
            }

            for (int row = 0; row < HnefataflBoardState.Size; row++)
            {
                for (int col = 0; col < HnefataflBoardState.Size; col++)
                {
                    HnefataflPieceKind kind = board[row, col];
                    if (kind == HnefataflPieceKind.None)
                    {
                        continue;
                    }

                    var coord = new HnefataflCoord(row, col);
                    if (pieces.ContainsKey(coord))
                    {
                        SnapPiece(pieces[coord], coord);
                        continue;
                    }

                    Sprite sprite = SpriteFor(kind, coord);
                    if (sprite == null)
                    {
                        continue;
                    }

                    HnefataflPieceView view = CreatePiece(sprite, kind);
                    SnapPiece(view, coord);
                    pieces[coord] = view;
                }
            }
        }

        private Sprite SpriteFor(HnefataflPieceKind kind, HnefataflCoord coord)
        {
            bool useB = ((coord.Row + coord.Col) & 1) == 1;
            return kind switch
            {
                HnefataflPieceKind.Attacker => useB ? attackerSpriteB : attackerSpriteA,
                HnefataflPieceKind.Defender => useB ? defenderSpriteB : defenderSpriteA,
                HnefataflPieceKind.King => kingSprite,
                _ => null,
            };
        }

        private HnefataflPieceView CreatePiece(Sprite sprite, HnefataflPieceKind kind)
        {
            var go = new GameObject($"Piece_{kind}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(piecesRoot, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            float cell = GetCellSize();
            rect.sizeDelta = new Vector2(cell * pieceScale, cell * pieceScale);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.useSpriteMesh = false;
            image.raycastTarget = false;
            return new HnefataflPieceView(rect, image, kind);
        }

        private void SnapPiece(HnefataflPieceView piece, HnefataflCoord coord)
        {
            piece.Rect.anchoredPosition = CoordToLocal(coord);
            piece.Rect.localScale = Vector3.one;
        }

        private void ClearPieces()
        {
            foreach (KeyValuePair<HnefataflCoord, HnefataflPieceView> pair in pieces)
            {
                if (pair.Value?.Rect != null)
                {
                    Destroy(pair.Value.Rect.gameObject);
                }
            }

            pieces.Clear();
        }

        private bool TryScreenToCoord(Vector2 screen, out HnefataflCoord coord)
        {
            coord = default;
            if (piecesRoot == null)
            {
                return false;
            }

            LayoutPlayableGrid();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    piecesRoot,
                    screen,
                    null,
                    out Vector2 local))
            {
                return false;
            }

            Rect grid = piecesRoot.rect;
            if (grid.width < 1f || grid.height < 1f)
            {
                return false;
            }

            float u = (local.x - grid.xMin) / grid.width;
            float vFromTop = (grid.yMax - local.y) / grid.height;
            if (u < 0f || u > 1f || vFromTop < 0f || vFromTop > 1f)
            {
                return false;
            }

            int col = Mathf.Clamp(
                Mathf.FloorToInt(u * HnefataflBoardState.Size),
                0,
                HnefataflBoardState.Size - 1);
            int row = Mathf.Clamp(
                Mathf.FloorToInt(vFromTop * HnefataflBoardState.Size),
                0,
                HnefataflBoardState.Size - 1);
            coord = new HnefataflCoord(row, col);
            return true;
        }

        private Vector2 CoordToLocal(HnefataflCoord coord)
        {
            Rect grid = piecesRoot.rect;
            float cellW = grid.width / HnefataflBoardState.Size;
            float cellH = grid.height / HnefataflBoardState.Size;
            float x = grid.xMin + (coord.Col + 0.5f) * cellW;
            float y = grid.yMax - (coord.Row + 0.5f) * cellH;
            return new Vector2(x, y);
        }

        private float GetCellSize()
        {
            Rect grid = piecesRoot.rect;
            return Mathf.Min(grid.width, grid.height) / HnefataflBoardState.Size;
        }

        private void LayoutPlayableGrid()
        {
            if (boardRect == null || piecesRoot == null)
            {
                return;
            }

            Rect spriteLocal = GetBoardSpriteLocalRect();
            float left = Mathf.Lerp(spriteLocal.xMin, spriteLocal.xMax, GridLeft / TextureWidth);
            float right = Mathf.Lerp(spriteLocal.xMin, spriteLocal.xMax, GridRight / TextureWidth);
            float top = Mathf.Lerp(spriteLocal.yMax, spriteLocal.yMin, GridTop / TextureHeight);
            float bottom = Mathf.Lerp(spriteLocal.yMax, spriteLocal.yMin, GridBottom / TextureHeight);

            piecesRoot.anchorMin = new Vector2(0.5f, 0.5f);
            piecesRoot.anchorMax = new Vector2(0.5f, 0.5f);
            piecesRoot.pivot = new Vector2(0.5f, 0.5f);
            piecesRoot.sizeDelta = new Vector2(right - left, top - bottom);
            piecesRoot.anchoredPosition = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
        }

        private Rect GetBoardSpriteLocalRect()
        {
            Rect rect = boardRect.rect;
            if (boardImage == null || boardImage.sprite == null || !boardImage.preserveAspect)
            {
                return rect;
            }

            Rect spriteRect = boardImage.sprite.rect;
            float spriteAspect = spriteRect.width / Mathf.Max(1f, spriteRect.height);
            float rectAspect = rect.width / Mathf.Max(1f, rect.height);
            float width = rect.width;
            float height = rect.height;
            if (spriteAspect > rectAspect)
            {
                height = width / spriteAspect;
            }
            else
            {
                width = height * spriteAspect;
            }

            float x = rect.xMin + (rect.width - width) * 0.5f;
            float y = rect.yMin + (rect.height - height) * 0.5f;
            return new Rect(x, y, width, height);
        }

        private sealed class HnefataflPieceView
        {
            public RectTransform Rect { get; }
            public Image Image { get; }
            public HnefataflPieceKind Kind { get; }

            public HnefataflPieceView(RectTransform rect, Image image, HnefataflPieceKind kind)
            {
                Rect = rect;
                Image = image;
                Kind = kind;
            }
        }
    }
}
