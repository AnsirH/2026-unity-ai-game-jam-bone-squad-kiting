using UnityEngine;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Bosses
{
    /// <summary>Numbers of the Frost Knight fight. Edited on the stage's Boss Arena object.</summary>
    [System.Serializable]
    public class FrostKnightTuning
    {
        [Header("Health")]
        public float MaxHp = 30000f;
        [Tooltip("Phase 1 ends when the boss is down to this share of its health. It cannot drop lower before that.")]
        [Range(0.05f, 0.95f)] public float PhaseOneEndsAt = 0.6f;

        [Header("Phase 1: on the throne")]
        [Tooltip("Seconds between the squad entering and the first attack.")]
        public float FirstAttackDelay = 2f;
        [Tooltip("From raising the hands to the sword touching the floor: the warning time of the parallel throne attack.")]
        public float SlamHitSeconds = 2.9f;
        [Tooltip("The circle warning and sword impact share this duration.")]
        [Min(.1f)] public float CircleHitSeconds = 1.6f;
        [Tooltip("After the sword lands: the rest of the motion, then this pause before the next attack.")]
        public float SlamRecoverSeconds = 1.7f;
        public float ThroneAttackGap = 1.2f;
        public float HitDamage = 55f;
        [Tooltip("The two parallel lines: each line's near and far edge from the middle, and half its length.")]
        public float ParallelInner = 0.9f;
        public float ParallelOuter = 3.3f;
        public float ParallelHalfLength = 6f;
        [Tooltip("The lines are turned by up to this many degrees either way, anew for every attack.")]
        public float ParallelMaxAngle = 45f;
        [Tooltip("Circle radius when squad scaling is off or the squad has no ring formation.")]
        public float CircleRadius = 3.1f;
        [Tooltip("The circle shrinks as the squad does, the same way the ring formation does, so the ring keeps dodging it.")]
        public bool CircleScalesWithSquad = true;
        [Range(.1f, .99f)] public float CircleRingRatio = .9f;

        [Header("Between the phases: the mages")]
        public EliteDefinition[] Mages = new EliteDefinition[0];
        [Tooltip("Seconds after the boss is stunned before the mages appear.")]
        public float MageDelay = 1.2f;
        [Tooltip("Standing up, then fading out of sight.")]
        public float StandUpSeconds = 4.2f;

        [Header("Phase 2: out of sight between attacks")]
        [Tooltip("Attacks between two groggy moments, in order. C = charge across, L = lethal slam.")]
        public string AttackOrder = "CCLC";
        public float AttackGap = 1.2f;
        [Tooltip("Base playback speed of the charged slash. Windup takes 35% of its normal duration; recovery plays at base speed.")]
        public float ChargeAnimSpeed = 1.5f;
        [Tooltip("Width of each of the three horizontal charge lanes.")]
        public float ChargeRowWidth = 5f;
        [Tooltip("Added to the line formation's soldier spacing before doubling the gap between charge lanes.")]
        [Min(0f)] public float ChargeGapPadding = .12f;
        [Tooltip("The pair of rows is moved up or down the room by up to this much, anew for every charge.")]
        public float ChargeShift = 3f;
        [Tooltip("While it hangs in the air with the sword raised, the squad can hit it: from when it has appeared until the sword comes down.")]
        public bool ChargeCanBeHit = true;
        [Tooltip("Warning time of the lethal slam.")]
        public float LethalWarnSeconds = 2.9f;
        [Tooltip("The middle zone of the lethal slam is this share of the room's width.")]
        [Range(0.1f, 0.9f)] public float LethalCenterShare = .5f;
        [Tooltip("If the squad leader stands in the lethal zone, the whole squad falls.")]
        public bool LethalKillsLeader = true;
        public float LethalHoldSeconds = 1f;
        [Tooltip("How long it takes the boss to fade out of sight.")]
        public float VanishSeconds = 1.3f;
        public float GroggySeconds = 6f;

        [Header("Death")]
        public float DieAnimSpeed = 1.6f;
    }

    /// <summary>
    /// Marks a stage as the Frost Knight's: the room the fight happens in, the throne, and where the mages
    /// appear. This object stands at the middle of the room's floor; its +Z points at the throne, so +X is
    /// the room's right when looking at the throne. The fight starts when the squad walks into the room.
    /// </summary>
    public class BossArena : MonoBehaviour
    {
        [Tooltip("Width (left to right) and depth (entrance to throne) of the floor the fight uses.")]
        public Vector2 Size = new(24f, 22f);
        [Tooltip("Where the boss sits: position and facing of the throne's seat.")]
        public Transform Seat;
        public Transform[] MageSpawns = new Transform[0];
        public string BossName = "마력의 주인";
        public FrostKnightTuning Tuning = new();

        public Vector2 Center => new(transform.position.x, transform.position.z);
        public Vector2 Right { get { var r = transform.right; return new Vector2(r.x, r.z).normalized; } }
        public Vector2 Forward { get { var f = transform.forward; return new Vector2(f.x, f.z).normalized; } }
        /// <summary>The room's turn around Y in degrees, clockwise seen from above (0 = the throne is toward +Z).</summary>
        public float Yaw => transform.eulerAngles.y;

        /// <summary>A point of the room: x to the right, z toward the throne, from the middle.</summary>
        public Vector2 Point(float x, float z) => Center + Right * x + Forward * z;

        public bool Contains(Vector2 p, float margin = 0f)
        {
            var d = p - Center;
            return Mathf.Abs(Vector2.Dot(d, Right)) <= Size.x * 0.5f - margin && Mathf.Abs(Vector2.Dot(d, Forward)) <= Size.y * 0.5f - margin;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(Size.x, 0.2f, Size.y));
            Gizmos.DrawLine(Vector3.up * 0.1f, new Vector3(0f, 0.1f, Size.y * 0.5f));
        }
    }
}
