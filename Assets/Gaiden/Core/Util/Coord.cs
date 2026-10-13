using System;

namespace Gaiden.Core
{
    /// <summary>Board tile coordinate. X = column (0..Cols-1, left→right), Y = row (0..Rows-1, top→bottom).</summary>
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y) { X = x; Y = y; }

        public static readonly Coord Up = new Coord(0, -1);
        public static readonly Coord Down = new Coord(0, 1);
        public static readonly Coord Left = new Coord(-1, 0);
        public static readonly Coord Right = new Coord(1, 0);
        public static readonly Coord[] Cardinals = { Up, Right, Down, Left };

        public static Coord operator +(Coord a, Coord b) => new Coord(a.X + b.X, a.Y + b.Y);
        public static Coord operator -(Coord a, Coord b) => new Coord(a.X - b.X, a.Y - b.Y);
        public static Coord operator *(Coord a, int k) => new Coord(a.X * k, a.Y * k);
        public static bool operator ==(Coord a, Coord b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Coord a, Coord b) => !(a == b);

        public int Manhattan(Coord o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y);
        public int Chebyshev(Coord o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Y - o.Y));
        public bool IsCardinalNeighbor(Coord o) => Manhattan(o) == 1;
        public bool IsDiagonalNeighbor(Coord o) => Chebyshev(o) == 1 && Manhattan(o) == 2;

        public bool Equals(Coord o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Coord c && Equals(c);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }
}
