using System;
using System.Collections.Generic;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// 9x9 Tablut / Kiev-Rus tavlei: attackers move first, king escapes via corners.
    /// </summary>
    public sealed class HnefataflBoardState
    {
        public const int Size = 9;
        public const int ThroneRow = 4;
        public const int ThroneCol = 4;
        public const int KingMaxSteps = 3;

        private readonly HnefataflPieceKind[] cells = new HnefataflPieceKind[Size * Size];

        public HnefataflSide SideToMove { get; private set; }
        public HnefataflGameResult Result { get; private set; }

        public HnefataflPieceKind this[int row, int col]
        {
            get => cells[Index(row, col)];
            private set => cells[Index(row, col)] = value;
        }

        public HnefataflPieceKind Get(HnefataflCoord coord) => this[coord.Row, coord.Col];

        public static bool IsInside(int row, int col) =>
            row >= 0 && row < Size && col >= 0 && col < Size;

        public static bool IsThrone(int row, int col) =>
            row == ThroneRow && col == ThroneCol;

        public static bool IsCorner(int row, int col) =>
            (row == 0 || row == Size - 1) && (col == 0 || col == Size - 1);

        public static bool IsRestricted(int row, int col) =>
            IsThrone(row, col) || IsCorner(row, col);

        public void SetupNewGame()
        {
            Array.Clear(cells, 0, cells.Length);
            Result = HnefataflGameResult.None;
            SideToMove = HnefataflSide.Attackers;

            this[ThroneRow, ThroneCol] = HnefataflPieceKind.King;

            // Defenders — cross arms.
            Place(HnefataflPieceKind.Defender, 2, 4);
            Place(HnefataflPieceKind.Defender, 3, 4);
            Place(HnefataflPieceKind.Defender, 5, 4);
            Place(HnefataflPieceKind.Defender, 6, 4);
            Place(HnefataflPieceKind.Defender, 4, 2);
            Place(HnefataflPieceKind.Defender, 4, 3);
            Place(HnefataflPieceKind.Defender, 4, 5);
            Place(HnefataflPieceKind.Defender, 4, 6);

            // Attackers — four groups of four at board edges.
            Place(HnefataflPieceKind.Attacker, 0, 3);
            Place(HnefataflPieceKind.Attacker, 0, 4);
            Place(HnefataflPieceKind.Attacker, 0, 5);
            Place(HnefataflPieceKind.Attacker, 1, 4);

            Place(HnefataflPieceKind.Attacker, 8, 3);
            Place(HnefataflPieceKind.Attacker, 8, 4);
            Place(HnefataflPieceKind.Attacker, 8, 5);
            Place(HnefataflPieceKind.Attacker, 7, 4);

            Place(HnefataflPieceKind.Attacker, 3, 0);
            Place(HnefataflPieceKind.Attacker, 4, 0);
            Place(HnefataflPieceKind.Attacker, 5, 0);
            Place(HnefataflPieceKind.Attacker, 4, 1);

            Place(HnefataflPieceKind.Attacker, 3, 8);
            Place(HnefataflPieceKind.Attacker, 4, 8);
            Place(HnefataflPieceKind.Attacker, 5, 8);
            Place(HnefataflPieceKind.Attacker, 4, 7);
        }

        public HnefataflBoardState Clone()
        {
            var copy = new HnefataflBoardState
            {
                SideToMove = SideToMove,
                Result = Result,
            };
            Array.Copy(cells, copy.cells, cells.Length);
            return copy;
        }

        public bool TryApplyMove(HnefataflMove move)
        {
            if (Result != HnefataflGameResult.None)
            {
                return false;
            }

            if (!IsLegalMove(move))
            {
                return false;
            }

            HnefataflPieceKind piece = Get(move.From);
            this[move.From.Row, move.From.Col] = HnefataflPieceKind.None;
            this[move.To.Row, move.To.Col] = piece;

            ResolveCaptures(move.To, piece);

            if (piece == HnefataflPieceKind.King && IsCorner(move.To.Row, move.To.Col))
            {
                Result = HnefataflGameResult.DefendersWin;
                return true;
            }

            if (!TryFindKing(out HnefataflCoord kingPos))
            {
                Result = HnefataflGameResult.AttackersWin;
                return true;
            }

            if (IsKingCaptured(kingPos))
            {
                this[kingPos.Row, kingPos.Col] = HnefataflPieceKind.None;
                Result = HnefataflGameResult.AttackersWin;
                return true;
            }

            SideToMove = Opposite(SideToMove);
            if (!HasAnyLegalMove(SideToMove))
            {
                Result = SideToMove == HnefataflSide.Attackers
                    ? HnefataflGameResult.DefendersWin
                    : HnefataflGameResult.AttackersWin;
            }

            return true;
        }

        public bool IsLegalMove(HnefataflMove move)
        {
            if (!IsInside(move.From.Row, move.From.Col) || !IsInside(move.To.Row, move.To.Col))
            {
                return false;
            }

            if (move.From.Equals(move.To))
            {
                return false;
            }

            HnefataflPieceKind piece = Get(move.From);
            if (!BelongsToSide(piece, SideToMove))
            {
                return false;
            }

            if (Get(move.To) != HnefataflPieceKind.None)
            {
                return false;
            }

            bool isKing = piece == HnefataflPieceKind.King;
            if (IsRestricted(move.To.Row, move.To.Col) && !isKing)
            {
                return false;
            }

            if (move.From.Row != move.To.Row && move.From.Col != move.To.Col)
            {
                return false;
            }

            int steps = Math.Abs(move.To.Row - move.From.Row) + Math.Abs(move.To.Col - move.From.Col);
            if (isKing && steps > KingMaxSteps)
            {
                return false;
            }

            return IsPathClear(move.From, move.To);
        }

        public void CollectLegalMoves(HnefataflSide side, List<HnefataflMove> buffer)
        {
            buffer.Clear();
            if (Result != HnefataflGameResult.None)
            {
                return;
            }

            HnefataflSide saved = SideToMove;
            SideToMove = side;
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    HnefataflPieceKind piece = this[row, col];
                    if (!BelongsToSide(piece, side))
                    {
                        continue;
                    }

                    CollectMovesFrom(new HnefataflCoord(row, col), buffer);
                }
            }

            SideToMove = saved;
        }

        public bool TryFindKing(out HnefataflCoord king)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (this[row, col] == HnefataflPieceKind.King)
                    {
                        king = new HnefataflCoord(row, col);
                        return true;
                    }
                }
            }

            king = default;
            return false;
        }

        public int CountPieces(HnefataflPieceKind kind)
        {
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == kind)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountKingHostiles(HnefataflCoord king)
        {
            int blocked = 0;
            if (IsHostileToKing(king.Row - 1, king.Col)) blocked++;
            if (IsHostileToKing(king.Row + 1, king.Col)) blocked++;
            if (IsHostileToKing(king.Row, king.Col - 1)) blocked++;
            if (IsHostileToKing(king.Row, king.Col + 1)) blocked++;
            return blocked;
        }

        public static int HostilesNeededToCaptureKing(HnefataflCoord king)
        {
            if (IsThrone(king.Row, king.Col))
            {
                return 4;
            }

            bool nextToThrone =
                Math.Abs(king.Row - ThroneRow) + Math.Abs(king.Col - ThroneCol) == 1;
            // Three attacker cells plus the throne anvil.
            return nextToThrone ? 4 : 2;
        }

        public static HnefataflSide Opposite(HnefataflSide side) =>
            side == HnefataflSide.Attackers ? HnefataflSide.Defenders : HnefataflSide.Attackers;

        public static bool BelongsToSide(HnefataflPieceKind piece, HnefataflSide side)
        {
            return side == HnefataflSide.Attackers
                ? piece == HnefataflPieceKind.Attacker
                : piece == HnefataflPieceKind.Defender || piece == HnefataflPieceKind.King;
        }

        private void Place(HnefataflPieceKind kind, int row, int col) => this[row, col] = kind;

        private static int Index(int row, int col) => row * Size + col;

        private bool HasAnyLegalMove(HnefataflSide side)
        {
            var moves = new List<HnefataflMove>(64);
            CollectLegalMoves(side, moves);
            return moves.Count > 0;
        }

        private void CollectMovesFrom(HnefataflCoord from, List<HnefataflMove> buffer)
        {
            TryRay(from, -1, 0, buffer);
            TryRay(from, 1, 0, buffer);
            TryRay(from, 0, -1, buffer);
            TryRay(from, 0, 1, buffer);
        }

        private void TryRay(HnefataflCoord from, int dRow, int dCol, List<HnefataflMove> buffer)
        {
            HnefataflPieceKind piece = this[from.Row, from.Col];
            int maxSteps = piece == HnefataflPieceKind.King ? KingMaxSteps : Size;
            int steps = 0;
            int row = from.Row + dRow;
            int col = from.Col + dCol;
            while (IsInside(row, col) && steps < maxSteps)
            {
                if (this[row, col] != HnefataflPieceKind.None)
                {
                    break;
                }

                var to = new HnefataflCoord(row, col);
                var move = new HnefataflMove(from, to);
                if (IsLegalMove(move))
                {
                    buffer.Add(move);
                }

                row += dRow;
                col += dCol;
                steps++;
            }
        }

        private bool IsPathClear(HnefataflCoord from, HnefataflCoord to)
        {
            int dRow = Math.Sign(to.Row - from.Row);
            int dCol = Math.Sign(to.Col - from.Col);
            int row = from.Row + dRow;
            int col = from.Col + dCol;
            while (row != to.Row || col != to.Col)
            {
                if (this[row, col] != HnefataflPieceKind.None)
                {
                    return false;
                }

                row += dRow;
                col += dCol;
            }

            return true;
        }

        private void ResolveCaptures(HnefataflCoord landed, HnefataflPieceKind movedPiece)
        {
            TryCaptureVictim(landed.Row - 1, landed.Col, movedPiece, -1, 0);
            TryCaptureVictim(landed.Row + 1, landed.Col, movedPiece, 1, 0);
            TryCaptureVictim(landed.Row, landed.Col - 1, movedPiece, 0, -1);
            TryCaptureVictim(landed.Row, landed.Col + 1, movedPiece, 0, 1);
        }

        private void TryCaptureVictim(
            int victimRow,
            int victimCol,
            HnefataflPieceKind mover,
            int dRow,
            int dCol)
        {
            if (!IsInside(victimRow, victimCol))
            {
                return;
            }

            HnefataflPieceKind victim = this[victimRow, victimCol];
            if (victim == HnefataflPieceKind.None || victim == HnefataflPieceKind.King)
            {
                return;
            }

            bool moverIsAttacker = mover == HnefataflPieceKind.Attacker;
            bool victimIsAttacker = victim == HnefataflPieceKind.Attacker;
            if (moverIsAttacker == victimIsAttacker)
            {
                return;
            }

            int farRow = victimRow + dRow;
            int farCol = victimCol + dCol;
            if (!IsHostileTo(farRow, farCol, victim))
            {
                return;
            }

            this[victimRow, victimCol] = HnefataflPieceKind.None;
        }

        private bool IsHostileTo(int row, int col, HnefataflPieceKind victim)
        {
            if (!IsInside(row, col))
            {
                return false;
            }

            HnefataflPieceKind occupant = this[row, col];
            if (occupant != HnefataflPieceKind.None)
            {
                bool victimAttacker = victim == HnefataflPieceKind.Attacker;
                bool occupantAttacker = occupant == HnefataflPieceKind.Attacker;
                if (occupant == HnefataflPieceKind.King)
                {
                    return victimAttacker;
                }

                return victimAttacker != occupantAttacker;
            }

            // Empty restricted squares act as hostile anvils.
            if (IsThrone(row, col))
            {
                return true;
            }

            return IsCorner(row, col);
        }

        private bool IsKingCaptured(HnefataflCoord king)
        {
            if (IsThrone(king.Row, king.Col))
            {
                return CountKingHostiles(king) >= 4;
            }

            bool nextToThrone =
                Math.Abs(king.Row - ThroneRow) + Math.Abs(king.Col - ThroneCol) == 1;
            if (nextToThrone)
            {
                return CountKingHostiles(king) >= 4;
            }

            return IsHostileToKing(king.Row - 1, king.Col) && IsHostileToKing(king.Row + 1, king.Col)
                || IsHostileToKing(king.Row, king.Col - 1) && IsHostileToKing(king.Row, king.Col + 1);
        }

        private bool IsHostileToKing(int row, int col)
        {
            if (!IsInside(row, col))
            {
                return false;
            }

            HnefataflPieceKind occupant = this[row, col];
            if (occupant == HnefataflPieceKind.Attacker)
            {
                return true;
            }

            if (occupant != HnefataflPieceKind.None)
            {
                return false;
            }

            return IsThrone(row, col) || IsCorner(row, col);
        }
    }
}
