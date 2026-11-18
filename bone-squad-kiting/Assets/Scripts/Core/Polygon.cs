using UnityEngine;

namespace BoneSquadKiting.Core
{
    public static class Polygon
    {
        /// <summary>
        /// True when <paramref name="p"/> lies inside the closed polygon through <paramref name="points"/>
        /// (the last point connects back to the first). Works for concave shapes and either winding order.
        /// Fewer than three points is not a shape and contains nothing.
        /// </summary>
        public static bool Contains(Vector2[] points, Vector2 p)
        {
            if (points == null || points.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i], b = points[j];
                // Count edges that a ray going +x from p crosses.
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
