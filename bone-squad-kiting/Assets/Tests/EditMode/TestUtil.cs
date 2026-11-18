using System.Collections.Generic;
using UnityEngine;

public static class TestUtil
{
    /// <summary>Vector2 comparer with a per-component tolerance, for NUnit's Using(...).</summary>
    public static IComparer<Vector2> V2(float eps) => new Cmp(eps);

    class Cmp : IComparer<Vector2>
    {
        readonly float _eps;
        public Cmp(float eps) { _eps = eps; }
        public int Compare(Vector2 a, Vector2 b)
            => (Mathf.Abs(a.x - b.x) < _eps && Mathf.Abs(a.y - b.y) < _eps) ? 0 : 1;
    }
}
