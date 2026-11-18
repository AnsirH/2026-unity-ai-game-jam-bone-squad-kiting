using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    /// <summary>One squad member. Plain data + movement; rendering lives in SquadView.</summary>
    public class SquadUnit
    {
        public int Id;
        public float X, Z;
        public Vector2 Slot;                       // offset from the anchor, Vector2(x, z)
        public float Hp = GameConstants.UnitMax;
        public bool Dead;
        public bool Moving;                        // true while still travelling to its slot (see FireRules)
        public float Cd, Flash, Anim;
        public int ShotCount;

        /// <summary>
        /// Walk toward a world position, faster the further away it is (prototype update()). The caller
        /// decides the position — normally anchor + slot, pulled inside the playable area if needed.
        /// <see cref="Moving"/> is judged against this position, so a unit pressed against a wall counts
        /// as stopped once it reaches where it was actually sent.
        /// </summary>
        public void MoveToward(Vector2 target, float dt)
        {
            if (Dead) return;
            float ux = target.x - X, uz = target.y - Z;
            float dist = Mathf.Sqrt(ux * ux + uz * uz);
            Moving = dist > GameConstants.UnitArrive;
            if (Moving)
            {
                float speed = GameConstants.UnitBaseSpeed
                              + Mathf.Min(GameConstants.UnitSpeedDistCap, dist * GameConstants.UnitSpeedDistGain);
                float step = Mathf.Min(dist, speed * dt);
                X += ux / dist * step;
                Z += uz / dist * step;
            }
            Anim += dt * 5f;
            Flash = Mathf.Max(0, Flash - dt * 3f);
            Cd -= dt;
        }
    }
}
