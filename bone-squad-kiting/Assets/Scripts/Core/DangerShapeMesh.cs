using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>
    /// Flat XZ mesh covering the danger area of a shape, centered on the origin. Used to draw telegraphs.
    /// For the telegraph shader (BoneSquad/Attack Telegraph) every piece also says where each point is inside
    /// it: UV0 = the point on a unit square (a disc or ring: the square around it; a box: across, then along
    /// the way it grows, which is away from the center), UV1 = that square's size in metres.
    /// </summary>
    public static class DangerShapeMesh
    {
        const int Segments = 64;

        /// <summary>
        /// <paramref name="maxStep"/> above 0 cuts the area into pieces no longer than that along each side,
        /// so the mesh has points inside it that can be lifted onto uneven ground. 0 = as few triangles as possible.
        /// </summary>
        public static Mesh Build(DangerShape s, float maxStep = 0f, bool separateLanes = false)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();
            var size = new List<Vector2>();
            switch (s.Type)
            {
                case DangerShapeType.Disc: Ring(v, t, uv, size, 0f, s.RMax, maxStep); break;
                case DangerShapeType.Annulus: Ring(v, t, uv, size, s.RMin, s.ROut, maxStep); break;
                case DangerShapeType.Rectangle: Box(v, t, uv, size, -s.Outer, s.Outer, -s.HalfZ, s.HalfZ, maxStep); break;
                case DangerShapeType.CrossBandsX:
                    Box(v, t, uv, size, s.Inner, s.Outer, -s.HalfZ, s.HalfZ, maxStep);
                    Box(v, t, uv, size, -s.Outer, -s.Inner, -s.HalfZ, s.HalfZ, maxStep);
                    break;
                case DangerShapeType.ThreeLanes:
                    if (separateLanes)
                    {
                        for (int lane = -1; lane <= 1; lane++)
                        {
                            float x = lane * s.LaneSpacing;
                            Box(v, t, uv, size, x - s.LaneHalfWidth, x + s.LaneHalfWidth, -s.HalfZ, s.HalfZ, maxStep, true);
                        }
                        break;
                    }
                    Box(v, t, uv, size, -s.ThreeLaneHalfSpan, s.ThreeLaneHalfSpan, -s.HalfZ, s.HalfZ, maxStep);
                    break;
                case DangerShapeType.QuadrantBoxes:
                    Box(v, t, uv, size, s.Inner, s.Outer, s.Inner, s.Outer, maxStep);
                    Box(v, t, uv, size, -s.Outer, -s.Inner, s.Inner, s.Outer, maxStep);
                    Box(v, t, uv, size, s.Inner, s.Outer, -s.Outer, -s.Inner, maxStep);
                    Box(v, t, uv, size, -s.Outer, -s.Inner, -s.Outer, -s.Inner, maxStep);
                    break;
            }
            var m = new Mesh { name = "DangerShape " + s.Type };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.SetUVs(0, uv);
            m.SetUVs(1, size);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static int Steps(float length, float maxStep) =>
            maxStep <= 0f ? 1 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(length) / maxStep), 1, 64);

        static void Ring(List<Vector3> v, List<int> t, List<Vector2> uv, List<Vector2> size, float rIn, float rOut, float maxStep)
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
                    uv.Add(new Vector2(c * r, sn * r) / (2f * rOut) + new Vector2(0.5f, 0.5f));
                    size.Add(new Vector2(2f * rOut, 2f * rOut));
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

        // A box grows away from the center: along x when it sits to one side of it in x, along z otherwise.
        static void Box(List<Vector3> v, List<int> t, List<Vector2> uv, List<Vector2> size, float x0, float x1, float z0, float z1, float maxStep, bool forward = false)
        {
            int nx = Steps(x1 - x0, maxStep), nz = Steps(z1 - z0, maxStep), row = nz + 1;
            int b = v.Count;
            bool alongX = !forward && x0 * x1 >= 0f;
            bool outwardIsUp = forward || (alongX ? x0 + x1 >= 0f : z0 + z1 >= 0f);
            var extent = alongX ? new Vector2(Mathf.Abs(z1 - z0), Mathf.Abs(x1 - x0)) : new Vector2(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0));
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= nz; k++)
                {
                    float fx = (float)i / nx, fz = (float)k / nz;
                    v.Add(new Vector3(Mathf.Lerp(x0, x1, fx), 0, Mathf.Lerp(z0, z1, fz)));
                    float along = alongX ? fx : fz, across = alongX ? fz : fx;
                    uv.Add(new Vector2(across, outwardIsUp ? along : 1f - along));
                    size.Add(extent);
                }
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
