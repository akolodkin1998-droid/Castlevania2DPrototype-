using System;
using System.Collections.Generic;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Goal-driven AI: win now, stop opponent wins, then hunt the king or run him to a corner.
    /// </summary>
    public sealed class HnefataflAi
    {
        private readonly List<HnefataflMove> rootMoves = new List<HnefataflMove>(128);
        private readonly List<HnefataflMove>[] plyMoves;
        private readonly int[] priorityBuffer = new int[160];
        private readonly int searchDepth;

        public HnefataflAi(int searchDepth = 3)
        {
            this.searchDepth = UnityEngine.Mathf.Clamp(searchDepth, 1, 4);
            plyMoves = new List<HnefataflMove>[5];
            for (int i = 0; i < plyMoves.Length; i++)
            {
                plyMoves[i] = new List<HnefataflMove>(96);
            }
        }

        public bool TryChooseMove(HnefataflBoardState board, out HnefataflMove bestMove)
        {
            bestMove = default;
            if (board == null || board.Result != HnefataflGameResult.None)
            {
                return false;
            }

            board.CollectLegalMoves(board.SideToMove, rootMoves);
            if (rootMoves.Count == 0)
            {
                return false;
            }

            HnefataflSide aiSide = board.SideToMove;
            OrderMoves(board, rootMoves);

            if (TryImmediateWin(board, rootMoves, out bestMove))
            {
                return true;
            }

            int bestScore = int.MinValue;
            bestMove = rootMoves[0];

            for (int i = 0; i < rootMoves.Count; i++)
            {
                HnefataflMove move = rootMoves[i];
                HnefataflBoardState next = board.Clone();
                if (!next.TryApplyMove(move))
                {
                    continue;
                }

                int score;
                if (next.Result != HnefataflGameResult.None)
                {
                    score = TerminalScore(next.Result, aiSide, searchDepth);
                }
                else
                {
                    score = Minimax(
                        next,
                        searchDepth - 1,
                        int.MinValue + 1,
                        int.MaxValue - 1,
                        maximizing: false,
                        aiSide);
                }

                score += MoveGoalBonus(board, move, aiSide);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            return true;
        }

        private int Minimax(
            HnefataflBoardState board,
            int depth,
            int alpha,
            int beta,
            bool maximizing,
            HnefataflSide aiSide)
        {
            if (board.Result != HnefataflGameResult.None)
            {
                return TerminalScore(board.Result, aiSide, depth);
            }

            if (depth <= 0)
            {
                return Evaluate(board, aiSide);
            }

            List<HnefataflMove> moves = plyMoves[depth];
            board.CollectLegalMoves(board.SideToMove, moves);
            if (moves.Count == 0)
            {
                return Evaluate(board, aiSide);
            }

            OrderMoves(board, moves);

            if (maximizing)
            {
                int best = int.MinValue + 1;
                for (int i = 0; i < moves.Count; i++)
                {
                    HnefataflBoardState next = board.Clone();
                    if (!next.TryApplyMove(moves[i]))
                    {
                        continue;
                    }

                    int score = Minimax(next, depth - 1, alpha, beta, false, aiSide);
                    if (score > best)
                    {
                        best = score;
                    }

                    if (score > alpha)
                    {
                        alpha = score;
                    }

                    if (alpha >= beta)
                    {
                        break;
                    }
                }

                return best;
            }

            int worst = int.MaxValue - 1;
            for (int i = 0; i < moves.Count; i++)
            {
                HnefataflBoardState next = board.Clone();
                if (!next.TryApplyMove(moves[i]))
                {
                    continue;
                }

                int score = Minimax(next, depth - 1, alpha, beta, true, aiSide);
                if (score < worst)
                {
                    worst = score;
                }

                if (score < beta)
                {
                    beta = score;
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            return worst;
        }

        private static bool TryImmediateWin(
            HnefataflBoardState board,
            List<HnefataflMove> moves,
            out HnefataflMove win)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                HnefataflBoardState next = board.Clone();
                if (!next.TryApplyMove(moves[i]))
                {
                    continue;
                }

                bool aiWon =
                    (board.SideToMove == HnefataflSide.Attackers
                     && next.Result == HnefataflGameResult.AttackersWin)
                    || (board.SideToMove == HnefataflSide.Defenders
                        && next.Result == HnefataflGameResult.DefendersWin);

                if (aiWon)
                {
                    win = moves[i];
                    return true;
                }
            }

            win = default;
            return false;
        }

        private void OrderMoves(HnefataflBoardState board, List<HnefataflMove> moves)
        {
            int count = moves.Count;
            if (count <= 1)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                priorityBuffer[i] = MovePriority(board, moves[i]);
            }

            for (int i = 1; i < count; i++)
            {
                HnefataflMove current = moves[i];
                int currentScore = priorityBuffer[i];
                int j = i - 1;
                while (j >= 0 && priorityBuffer[j] < currentScore)
                {
                    moves[j + 1] = moves[j];
                    priorityBuffer[j + 1] = priorityBuffer[j];
                    j--;
                }

                moves[j + 1] = current;
                priorityBuffer[j + 1] = currentScore;
            }
        }

        private static int MovePriority(HnefataflBoardState board, HnefataflMove move)
        {
            HnefataflPieceKind piece = board.Get(move.From);
            int score = 0;

            if (piece == HnefataflPieceKind.King && HnefataflBoardState.IsCorner(move.To.Row, move.To.Col))
            {
                return 1_000_000;
            }

            if (board.TryFindKing(out HnefataflCoord king))
            {
                int need = HnefataflBoardState.HostilesNeededToCaptureKing(king);
                int hostiles = board.CountKingHostiles(king);
                bool landsBesideKing = IsOrthogonalNeighbor(move.To, king);

                if (piece == HnefataflPieceKind.Attacker && landsBesideKing && hostiles >= need - 1)
                {
                    score += 90_000;
                }

                if (piece == HnefataflPieceKind.King)
                {
                    score += (MinCornerDistance(move.From.Row, move.From.Col)
                              - MinCornerDistance(move.To.Row, move.To.Col)) * 900;
                    if (IsEdge(move.To.Row, move.To.Col))
                    {
                        score += 250;
                    }
                }
                else if (piece == HnefataflPieceKind.Attacker)
                {
                    score += (Manhattan(move.From, king) - Manhattan(move.To, king)) * 50;
                    if (landsBesideKing)
                    {
                        score += 600;
                    }

                    if (move.To.Row == king.Row || move.To.Col == king.Col)
                    {
                        score += 120;
                    }
                }
                else
                {
                    score += (Manhattan(move.From, king) - Manhattan(move.To, king)) * 15;
                    if (landsBesideKing)
                    {
                        score += 180;
                    }
                }
            }

            score += CountLandingCaptures(board, move) * 800;
            return score;
        }

        private static int MoveGoalBonus(HnefataflBoardState board, HnefataflMove move, HnefataflSide aiSide)
        {
            // Breaks remaining eval ties so the chosen move still looks purposeful.
            return aiSide == HnefataflSide.Attackers
                ? MovePriority(board, move) / 20
                : MovePriority(board, move) / 18;
        }

        private static int TerminalScore(HnefataflGameResult result, HnefataflSide aiSide, int depthLeft)
        {
            bool aiWon =
                (result == HnefataflGameResult.AttackersWin && aiSide == HnefataflSide.Attackers)
                || (result == HnefataflGameResult.DefendersWin && aiSide == HnefataflSide.Defenders);

            return aiWon ? 100000 + depthLeft * 250 : -100000 - depthLeft * 250;
        }

        private static int Evaluate(HnefataflBoardState board, HnefataflSide aiSide)
        {
            int attackers = board.CountPieces(HnefataflPieceKind.Attacker);
            int defenders = board.CountPieces(HnefataflPieceKind.Defender);
            int scoreForAttackers = (attackers * 14) - (defenders * 22);

            if (!board.TryFindKing(out HnefataflCoord king))
            {
                scoreForAttackers += 8000;
                return aiSide == HnefataflSide.Attackers ? scoreForAttackers : -scoreForAttackers;
            }

            int cornerDist = MinCornerDistance(king.Row, king.Col);
            int hostiles = board.CountKingHostiles(king);
            int need = HnefataflBoardState.HostilesNeededToCaptureKing(king);
            int attackerToKing = SumPieceDistances(board, HnefataflPieceKind.Attacker, king);
            int shield = CountKindAround(board, king, HnefataflPieceKind.Defender);

            // Surround the king.
            scoreForAttackers += hostiles * 110;
            if (hostiles >= need - 1)
            {
                scoreForAttackers += 420;
            }

            scoreForAttackers -= attackerToKing * 4;

            // Keep the king far from corners; block his files.
            scoreForAttackers += cornerDist * 55;
            if (IsEdge(king.Row, king.Col))
            {
                scoreForAttackers -= 90;
            }

            if (KingCanReachCorner(board, king))
            {
                scoreForAttackers -= 900;
            }

            scoreForAttackers -= CountClearCornerRays(board, king) * 140;
            scoreForAttackers += CountEscapeBlockers(board, king) * 35;
            scoreForAttackers += CountNearCornerGuards(board) * 18;
            scoreForAttackers -= shield * 16;

            int score = aiSide == HnefataflSide.Attackers ? scoreForAttackers : -scoreForAttackers;

            if (aiSide == HnefataflSide.Defenders)
            {
                score += (8 - cornerDist) * 70;
                if (IsEdge(king.Row, king.Col))
                {
                    score += 80;
                }

                if (KingCanReachCorner(board, king))
                {
                    score += 1200;
                }

                score += shield * 22;
            }

            return score;
        }

        private static int CountLandingCaptures(HnefataflBoardState board, HnefataflMove move)
        {
            HnefataflPieceKind mover = board.Get(move.From);
            int captures = 0;
            captures += CaptureInDirection(board, move.To, mover, -1, 0) ? 1 : 0;
            captures += CaptureInDirection(board, move.To, mover, 1, 0) ? 1 : 0;
            captures += CaptureInDirection(board, move.To, mover, 0, -1) ? 1 : 0;
            captures += CaptureInDirection(board, move.To, mover, 0, 1) ? 1 : 0;
            return captures;
        }

        private static bool CaptureInDirection(
            HnefataflBoardState board,
            HnefataflCoord landed,
            HnefataflPieceKind mover,
            int dRow,
            int dCol)
        {
            int victimRow = landed.Row + dRow;
            int victimCol = landed.Col + dCol;
            if (!HnefataflBoardState.IsInside(victimRow, victimCol))
            {
                return false;
            }

            HnefataflPieceKind victim = board[victimRow, victimCol];
            if (victim == HnefataflPieceKind.None || victim == HnefataflPieceKind.King)
            {
                return false;
            }

            bool moverAttacker = mover == HnefataflPieceKind.Attacker;
            bool victimAttacker = victim == HnefataflPieceKind.Attacker;
            if (moverAttacker == victimAttacker)
            {
                return false;
            }

            int farRow = victimRow + dRow;
            int farCol = victimCol + dCol;
            if (!HnefataflBoardState.IsInside(farRow, farCol))
            {
                return false;
            }

            HnefataflPieceKind far = board[farRow, farCol];
            if (far != HnefataflPieceKind.None)
            {
                if (far == HnefataflPieceKind.King)
                {
                    return victimAttacker;
                }

                bool farAttacker = far == HnefataflPieceKind.Attacker;
                return victimAttacker != farAttacker;
            }

            return HnefataflBoardState.IsThrone(farRow, farCol)
                   || HnefataflBoardState.IsCorner(farRow, farCol);
        }

        private static bool KingCanReachCorner(HnefataflBoardState board, HnefataflCoord king)
        {
            return CanKingSlideTo(board, king, 0, 0)
                   || CanKingSlideTo(board, king, 0, HnefataflBoardState.Size - 1)
                   || CanKingSlideTo(board, king, HnefataflBoardState.Size - 1, 0)
                   || CanKingSlideTo(board, king, HnefataflBoardState.Size - 1, HnefataflBoardState.Size - 1);
        }

        private static bool CanKingSlideTo(
            HnefataflBoardState board,
            HnefataflCoord king,
            int toRow,
            int toCol)
        {
            if (king.Row != toRow && king.Col != toCol)
            {
                return false;
            }

            int steps = Math.Abs(toRow - king.Row) + Math.Abs(toCol - king.Col);
            if (steps == 0 || steps > HnefataflBoardState.KingMaxSteps)
            {
                return false;
            }

            int dRow = Math.Sign(toRow - king.Row);
            int dCol = Math.Sign(toCol - king.Col);
            int row = king.Row + dRow;
            int col = king.Col + dCol;
            while (row != toRow || col != toCol)
            {
                if (board[row, col] != HnefataflPieceKind.None)
                {
                    return false;
                }

                row += dRow;
                col += dCol;
            }

            return board[toRow, toCol] == HnefataflPieceKind.None;
        }

        private static int CountClearCornerRays(HnefataflBoardState board, HnefataflCoord king)
        {
            int clear = 0;
            if (CanKingSlideTo(board, king, 0, 0)) clear++;
            if (CanKingSlideTo(board, king, 0, HnefataflBoardState.Size - 1)) clear++;
            if (CanKingSlideTo(board, king, HnefataflBoardState.Size - 1, 0)) clear++;
            if (CanKingSlideTo(board, king, HnefataflBoardState.Size - 1, HnefataflBoardState.Size - 1)) clear++;
            return clear;
        }

        private static int CountEscapeBlockers(HnefataflBoardState board, HnefataflCoord king)
        {
            int blockers = 0;
            for (int row = 0; row < HnefataflBoardState.Size; row++)
            {
                for (int col = 0; col < HnefataflBoardState.Size; col++)
                {
                    if (board[row, col] != HnefataflPieceKind.Attacker)
                    {
                        continue;
                    }

                    if (row == king.Row || col == king.Col)
                    {
                        blockers++;
                    }
                }
            }

            return blockers;
        }

        private static int CountNearCornerGuards(HnefataflBoardState board)
        {
            int guards = 0;
            int last = HnefataflBoardState.Size - 1;
            guards += IsAttacker(board, 0, 1) ? 1 : 0;
            guards += IsAttacker(board, 1, 0) ? 1 : 0;
            guards += IsAttacker(board, 0, last - 1) ? 1 : 0;
            guards += IsAttacker(board, 1, last) ? 1 : 0;
            guards += IsAttacker(board, last, 1) ? 1 : 0;
            guards += IsAttacker(board, last - 1, 0) ? 1 : 0;
            guards += IsAttacker(board, last, last - 1) ? 1 : 0;
            guards += IsAttacker(board, last - 1, last) ? 1 : 0;
            return guards;
        }

        private static bool IsAttacker(HnefataflBoardState board, int row, int col) =>
            board[row, col] == HnefataflPieceKind.Attacker;

        private static int SumPieceDistances(
            HnefataflBoardState board,
            HnefataflPieceKind kind,
            HnefataflCoord target)
        {
            int sum = 0;
            for (int row = 0; row < HnefataflBoardState.Size; row++)
            {
                for (int col = 0; col < HnefataflBoardState.Size; col++)
                {
                    if (board[row, col] == kind)
                    {
                        sum += Math.Abs(row - target.Row) + Math.Abs(col - target.Col);
                    }
                }
            }

            return sum;
        }

        private static int CountKindAround(
            HnefataflBoardState board,
            HnefataflCoord coord,
            HnefataflPieceKind kind)
        {
            int count = 0;
            if (IsKindAt(board, coord.Row - 1, coord.Col, kind)) count++;
            if (IsKindAt(board, coord.Row + 1, coord.Col, kind)) count++;
            if (IsKindAt(board, coord.Row, coord.Col - 1, kind)) count++;
            if (IsKindAt(board, coord.Row, coord.Col + 1, kind)) count++;
            return count;
        }

        private static bool IsKindAt(HnefataflBoardState board, int row, int col, HnefataflPieceKind kind) =>
            HnefataflBoardState.IsInside(row, col) && board[row, col] == kind;

        private static bool IsOrthogonalNeighbor(HnefataflCoord a, HnefataflCoord b) =>
            Math.Abs(a.Row - b.Row) + Math.Abs(a.Col - b.Col) == 1;

        private static bool IsEdge(int row, int col) =>
            row == 0 || col == 0 || row == HnefataflBoardState.Size - 1 || col == HnefataflBoardState.Size - 1;

        private static int Manhattan(HnefataflCoord a, HnefataflCoord b) =>
            Math.Abs(a.Row - b.Row) + Math.Abs(a.Col - b.Col);

        private static int MinCornerDistance(int row, int col)
        {
            int last = HnefataflBoardState.Size - 1;
            int d00 = row + col;
            int d08 = row + (last - col);
            int d80 = (last - row) + col;
            int d88 = (last - row) + (last - col);
            return Math.Min(d00, Math.Min(d08, Math.Min(d80, d88)));
        }
    }
}
