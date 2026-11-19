using System;
using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Axial hex coordinate (q, r). The implied cube coordinate s = -q - r.
    /// Reference: https://www.redblobgames.com/grids/hexagons/
    /// </summary>
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int q;
        public int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public int S => -q - r;

        /// <summary>The six axial neighbour directions (pointy-top order: E, NE, NW, W, SW, SE).</summary>
        public static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1),
        };

        public HexCoord Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = a.q - b.q;
            int dr = a.r - b.r;
            int ds = a.S - b.S;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }

        /// <summary>Rounds fractional axial coordinates to the nearest hex (cube rounding).</summary>
        public static HexCoord Round(float fq, float fr)
        {
            float fs = -fq - fr;
            int rq = Mathf.RoundToInt(fq);
            int rr = Mathf.RoundToInt(fr);
            int rs = Mathf.RoundToInt(fs);

            float dq = Mathf.Abs(rq - fq);
            float dr = Mathf.Abs(rr - fr);
            float ds = Mathf.Abs(rs - fs);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            return new HexCoord(rq, rr);
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.q + b.q, a.r + b.r);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.q - b.q, a.r - b.r);
        public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (q * 397) ^ r;
        public override string ToString() => $"({q}, {r})";
    }
}
