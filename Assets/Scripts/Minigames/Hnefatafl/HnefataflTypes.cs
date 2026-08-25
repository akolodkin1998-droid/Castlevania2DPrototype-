namespace Castlevania2D.Minigames.Hnefatafl
{
    public enum HnefataflSide
    {
        Attackers = 0,
        Defenders = 1,
    }

    public enum HnefataflPieceKind
    {
        None = 0,
        Attacker = 1,
        Defender = 2,
        King = 3,
    }

    public enum HnefataflGameResult
    {
        None = 0,
        AttackersWin = 1,
        DefendersWin = 2,
    }

    public readonly struct HnefataflCoord
    {
        public readonly int Row;
        public readonly int Col;

        public HnefataflCoord(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(HnefataflCoord other) => Row == other.Row && Col == other.Col;

        public override bool Equals(object obj) => obj is HnefataflCoord other && Equals(other);

        public override int GetHashCode() => (Row * 31) ^ Col;

        public override string ToString() => $"({Row},{Col})";
    }

    public readonly struct HnefataflMove
    {
        public readonly HnefataflCoord From;
        public readonly HnefataflCoord To;

        public HnefataflMove(HnefataflCoord from, HnefataflCoord to)
        {
            From = from;
            To = to;
        }
    }
}
