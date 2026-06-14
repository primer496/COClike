namespace AStarPathfinding 
{
    public struct Vector2Int : System.IEquatable<Vector2Int>
    {
        public int X;
        public int Y;

        public Vector2Int(int x, int y) 
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"[{X},{Y}]";
        public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vector2Int other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(X, Y);
        public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;
        public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => lhs.X != rhs.X || lhs.Y != rhs.Y;
    }
}