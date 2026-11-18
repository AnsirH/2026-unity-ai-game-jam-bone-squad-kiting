using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>Flat XZ mesh covering the danger area of a shape, centered on the origin. Used to draw telegraphs.</summary>
    public static class DangerShapeMesh
    {
        const int Segments = 64;

        /// <summary>
        /// <paramref name="maxStep"/> above 0 cuts the area into pieces no longer than that along each side,
        /// so the mesh has points inside it that can be lifted onto uneven ground. 0 = as few triangles as possible.
        /// </summary>
        public static Mesh Build(DangerShape s, float maxStep = 0f)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            switch (s.Type)
            {
                case DangerShapeType.Disc: Ring(v, t, 0f, s.RMax, maxStep); break;
                case DangerShapeType.Annulus: Ring(v, t, s.RMin, s.ROut, maxStep); break;
                case DangerShapeType.CrossBandsX:
                    Box(v, t, s.Inner, s.Outer, -s.HalfZ, s.HalfZ, maxStep);
                    Box(v, t, -s.Outer, -s.Inner, -s.HalfZ, s.HalfZ, maxStep);
                    break;
                case DangerShapeType.ThreeLanes:
                    Box(v, t, -s.ThreeLaneHalfSpan, s.ThreeLaneHalfSpan, -s.HalfZ, s.HalfZ, maxStep);
                    break;
                case DangerShapeType.QuadrantBoxes:
                    Box(v, t, s.Inner, s.Outer, s.Inner, s.Outer, maxStep);
                    Box(v, t, -s.Outer, -s.Inner, s.Inner, s.Outer, maxStep);
                    Box(v, t, s.Inner, s.Outer, -s.Outer, -s.Inner, maxStep);
                    Box(v, t, -s.Outer, -s.Inner, -s.Outer, -s.Inner, maxStep);
                    break;
            }
            var m = new Mesh { name = "DangerShape " + s.Type };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static int Steps(float length, float maxStep) =>
            maxStep <= 0f ? 1 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(length) / maxStep), 1, 64);

        static void Ring(List<Vector3> v, List<int> t, float rIn, float rOut, float maxStep)
        {
            int bands = Steps(rOut - rIn, maxStep), row = bands + 1;
            int b = v.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.PI * 2f * i / Segments;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                for (int k = 0; k <= bands; k++)
                {
                    float r = Mathf.Lerp(rIn, rOut, (float)k / bands);
                    v.Add(new Vector3(c * r, 0, sn * r));
                }
            }
            for (int i = 0; i < Segments; i++)
                for (int k = 0; k < bands; k++)
                {
                    int p = b + i * row + k, q = p + row;   // p: this spoke, q: the next spoke
                    t.Add(p); t.Add(q + 1); t.Add(p + 1);
                    t.Add(p); t.Add(q); t.Add(q + 1);
                }
        }

        static void Box(List<Vector3> v, List<int> t, float x0, float x1, float z0, float z1, float maxStep)
        {
            int nx = Steps(x1 - x0, maxStep), nz = Steps(z1 - z0, maxStep), row = nz + 1;
            int b = v.Count;
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                    v.Add(new Vector3(Mathf.Lerp(x0, x1, (float)i / nx), 0, Mathf.Lerp(z0, z1, (float)k / nz)));
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    int p = b + i * row + k, q = p + row;
                    t.Add(p); t.Add(p + 1); t.Add(q + 1);
                    t.Add(p); t.Add(q + 1); t.Add(q);
                }
        }
    }
}
