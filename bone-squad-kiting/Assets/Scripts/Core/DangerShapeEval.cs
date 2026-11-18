using UnityEngine;

namespace BoneSquadKiting.Core
{
    public static class DangerShapeEval
    {
        /// <summary>
        /// The same test for a zone that has been turned <paramref name="clockwiseDegrees"/> around its
        /// center (clockwise seen from above): the point is turned back by that angle and tested as usual.
        /// </summary>
        public static bool InDanger(DangerShape s, float cx, float cz, float x, float z, float clockwiseDegrees)
        {
            if (clockwiseDegrees == 0f) return InDanger(s, cx, cz, x, z);
            float r = clockwiseDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            float dx = x - cx, dz = z - cz;
            return InDanger(s, cx, cz, cx + dx * cos - dz * sin, cz + dx * sin + dz * cos);
        }

        /// <summary>True when world point (x, z) is inside the danger area of a shape centered at (cx, cz).</summary>
        public static bool InDanger(DangerShape s, float cx, float cz, float x, float z)
        {
            float dx = x - cx, dz = z - cz;
            float ax = Mathf.Abs(dx), az = Mathf.Abs(dz);
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            switch (s.Type)
            {
                case DangerShapeType.Disc:          return r < s.RMax;
                case DangerShapeType.Annulus:       return r > s.RMin && r < s.ROut;
                case DangerShapeType.CrossBandsX:   return ax > s.Inner && ax < s.Outer && az < s.HalfZ;
                case DangerShapeType.QuadrantBoxes: return ax > s.Inner && az > s.Inner && ax < s.Outer && az < s.Outer;
                default: return false;
            }
        }
    }
}
