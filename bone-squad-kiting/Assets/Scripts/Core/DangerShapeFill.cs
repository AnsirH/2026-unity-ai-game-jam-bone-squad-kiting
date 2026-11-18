using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>Points spread evenly over a danger shape, for effects that fill the area when it goes off.</summary>
    public static class DangerShapeFill
    {
        /// <summary>
        /// Adds points (x, z around the shape's centre, before any turn) about <paramref name="spacing"/> apart.
        /// Every point lies inside the shape.
        /// </summary>
        public static void Points(DangerShape s, float spacing, List<Vector2> into)
        {
            spacing = Mathf.Max(0.25f, spacing);
            switch (s.Type)
            {
                case DangerShapeType.Disc: Ring(0f, s.RMax, spacing, into); break;
                case DangerShapeType.Annulus: Ring(s.RMin, s.ROut, spacing, into); break;
                case DangerShapeType.CrossBandsX:
                    Box(s.Inner, s.Outer, -s.HalfZ, s.HalfZ, spacing, into);
                    Box(-s.Outer, -s.Inner, -s.HalfZ, s.HalfZ, spacing, into);
                    break;
                case DangerShapeType.ThreeLanes:
                    Box(-s.ThreeLaneHalfSpan, s.ThreeLaneHalfSpan, -s.HalfZ, s.HalfZ, spacing, into);
                    break;
                case DangerShapeType.QuadrantBoxes:
                    Box(s.Inner, s.Outer, s.Inner, s.Outer, spacing, into);
                    Box(-s.Outer, -s.Inner, s.Inner, s.Outer, spacing, into);
                    Box(s.Inner, s.Outer, -s.Outer, -s.Inner, spacing, into);
                    Box(-s.Outer, -s.Inner, -s.Outer, -s.Inner, spacing, into);
                    break;
            }
        }

        // Cells of about spacing x spacing, one point in the middle of each.
        static void Box(float x0, float x1, float z0, float z1, float spacing, List<Vector2> into)
        {
            if (x1 <= x0 || z1 <= z0) return;
            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / spacing));
            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / spacing));
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                    into.Add(new Vector2(Mathf.Lerp(x0, x1, (i + 0.5f) / nx), Mathf.Lerp(z0, z1, (k + 0.5f) / nz)));
        }

        // Circles about spacing apart between the two radii, points about spacing apart along each.
        static void Ring(float rIn, float rOut, float spacing, List<Vector2> into)
        {
            if (rOut <= rIn) return;
            int bands = Mathf.Max(1, Mathf.RoundToInt((rOut - rIn) / spacing));
            for (int b = 0; b < bands; b++)
            {
                float r = Mathf.Lerp(rIn, rOut, (b + 0.5f) / bands);
                int n = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * r / spacing));
                float offset = (b % 2) * 0.5f;                       // neighbouring circles are staggered
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * (i + offset) / n;
                    into.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
            }
        }
    }
}
