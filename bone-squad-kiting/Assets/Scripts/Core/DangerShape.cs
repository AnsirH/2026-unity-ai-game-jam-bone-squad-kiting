namespace BoneSquadKiting.Core
{
    public enum PatternId { Core, Gate, Quadrant, Shell }

    public enum DangerShapeType { Disc, Annulus, CrossBandsX, QuadrantBoxes }

    /// <summary>
    /// Danger zone geometry as data. Evaluated by <see cref="DangerShapeEval"/>.
    /// Add a new <see cref="DangerShapeType"/> only when a pattern needs geometry these cannot express.
    /// </summary>
    [System.Serializable]
    public struct DangerShape
    {
        public DangerShapeType Type;
        public float RMax;                 // Disc: danger when r < RMax
        public float RMin, ROut;           // Annulus: danger when RMin < r < ROut
        public float Inner, Outer, HalfZ;  // CrossBandsX: Inner < |dx| < Outer and |dz| < HalfZ
                                           // QuadrantBoxes: |dx| and |dz| both in (Inner, Outer)

        /// <summary>The same shape with every dimension multiplied by <paramref name="k"/>.</summary>
        public DangerShape Scaled(float k) => new()
        {
            Type = Type, RMax = RMax * k, RMin = RMin * k, ROut = ROut * k,
            Inner = Inner * k, Outer = Outer * k, HalfZ = HalfZ * k,
        };

        public static DangerShape Disc(float r) => new() { Type = DangerShapeType.Disc, RMax = r };
        public static DangerShape Annulus(float rMin, float rOut) => new() { Type = DangerShapeType.Annulus, RMin = rMin, ROut = rOut };
        public static DangerShape BandsX(float inner, float outer, float halfZ) => new() { Type = DangerShapeType.CrossBandsX, Inner = inner, Outer = outer, HalfZ = halfZ };
        public static DangerShape Quadrants(float inner, float outer) => new() { Type = DangerShapeType.QuadrantBoxes, Inner = inner, Outer = outer };
    }
}
