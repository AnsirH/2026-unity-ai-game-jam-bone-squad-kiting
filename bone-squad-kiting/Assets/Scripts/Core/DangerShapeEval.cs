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

        /// <summary>
        /// True when the point is outside the danger area but within <paramref name="margin"/> of it: a
        /// close shave. The zone may be turned, as in InDanger.
        /// </summary>
        public static bool NearDanger(DangerShape s, float cx, float cz, float x, float z, float clockwiseDegrees, float margin)
        {
            if (margin <= 0f || InDanger(s, cx, cz, x, z, clockwiseDegrees)) return false;
            var grown = s;
            switch (s.Type)
            {
                case DangerShapeType.Disc: grown.RMax += margin; break;
                case DangerShapeType.Annulus: grown.RMin = Mathf.Max(0f, s.RMin - margin); grown.ROut += margin; break;
                case DangerShapeType.CrossBandsX: grown.Inner = s.Inner - margin; grown.Outer += margin; grown.HalfZ += margin; break;
                case DangerShapeType.QuadrantBoxes: grown.Inner = s.Inner - margin; grown.Outer += margin; break;
            }
            return InDanger(grown, cx, cz, x, z, clockwiseDegrees);
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
