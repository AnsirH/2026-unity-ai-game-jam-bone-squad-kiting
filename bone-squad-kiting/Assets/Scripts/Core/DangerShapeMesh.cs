using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>Flat XZ mesh covering the danger area of a shape, centered on the origin. Used to draw telegraphs.</summary>
    public static class DangerShapeMesh
    {
        const int Segments = 64;

        public static Mesh Build(DangerShape s)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            switch (s.Type)
            {
                case DangerShapeType.Disc: Ring(v, t, 0f, s.RMax); break;
                case DangerShapeType.Annulus: Ring(v, t, s.RMin, s.ROut); break;
                case DangerShapeType.CrossBandsX:
                    Box(v, t, s.Inner, s.Outer, -s.HalfZ, s.HalfZ);
                    Box(v, t, -s.Outer, -s.Inner, -s.HalfZ, s.HalfZ);
                    break;
                case DangerShapeType.ThreeLanes:
                    Box(v, t, -s.ThreeLaneHalfSpan, s.ThreeLaneHalfSpan, -s.HalfZ, s.HalfZ);
                    break;
                case DangerShapeType.QuadrantBoxes:
                    Box(v, t, s.Inner, s.Outer, s.Inner, s.Outer);
                    Box(v, t, -s.Outer, -s.Inner, s.Inner, s.Outer);
                    Box(v, t, s.Inner, s.Outer, -s.Outer, -s.Inner);
                    Box(v, t, -s.Outer, -s.Inner, -s.Outer, -s.Inner);
                    break;
            }
            var m = new Mesh { name = "DangerShape " + s.Type };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void Ring(List<Vector3> v, List<int> t, float rIn, float rOut)
        {
            int b = v.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.PI * 2f * i / Segments;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                v.Add(new Vector3(c * rIn, 0, sn * rIn));
                v.Add(new Vector3(c * rOut, 0, sn * rOut));
            }
            for (int i = 0; i < Segments; i++)
            {
                int k = b + i * 2;
                t.Add(k); t.Add(k + 3); t.Add(k + 1);
                t.Add(k); t.Add(k + 2); t.Add(k + 3);
            }
        }

        static void Box(List<Vector3> v, List<int> t, float x0, float x1, float z0, float z1)
        {
            int b = v.Count;
            v.Add(new Vector3(x0, 0, z0)); v.Add(new Vector3(x0, 0, z1));
            v.Add(new Vector3(x1, 0, z1)); v.Add(new Vector3(x1, 0, z0));
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }
}
