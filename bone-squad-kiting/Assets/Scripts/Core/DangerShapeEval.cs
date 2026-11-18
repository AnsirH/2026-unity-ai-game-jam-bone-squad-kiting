using UnityEngine;

namespace BoneSquadKiting.Core
{
    public static class DangerShapeEval
    {
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
