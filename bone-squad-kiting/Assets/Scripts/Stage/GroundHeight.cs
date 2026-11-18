using UnityEngine;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// How high the ground is at a point, for drawing things on uneven terrain. The game itself is flat
    /// (everything is simulated on the x/z plane); this only lifts what is drawn. With no mask set — the
    /// built placeholder stages — the ground is at 0 everywhere and nothing is raycast.
    /// </summary>
    public static class GroundHeight
    {
        public static LayerMask Mask;      // which colliders count as ground; 0 = flat world
        public static float RayTop = 200f;

        public static float Y(float x, float z)
        {
            if (Mask.value == 0) return 0f;
            return Physics.Raycast(new Vector3(x, RayTop, z), Vector3.down, out var hit, RayTop * 2f, Mask, QueryTriggerInteraction.Ignore)
                ? hit.point.y : 0f;
        }
    }
}
