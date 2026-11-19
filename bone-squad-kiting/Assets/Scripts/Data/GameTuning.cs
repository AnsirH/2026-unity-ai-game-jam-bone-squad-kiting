using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>
    /// Numbers that are the same in every stage, in one asset. Every stage scene points at the same
    /// GameTuning, so a value changed here applies everywhere — and because it is an asset, a value changed
    /// while playing stays changed after play mode ends.
    /// </summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Game Tuning")]
    public class GameTuning : ScriptableObject
    {
        [Header("Shooting")]
        [Min(0f)] public float UnitFireRange = 5.5f;      // how far one unit can shoot
        [Min(0f)] public float SquadEngageRange = 5f;     // from the formation's center: attack orders stop here
        [Min(0f)] public float ThreatRadius = 4f;         // a holding unit shoots mobs this close before anything else
        [Min(0f)] public float AttackMoveRadius = 4f;     // attack-move prefers mobs this close to the clicked point

        [Header("Movement")]
        [Min(0f)] public float AnchorSpeed = 7.4f;        // how fast the formation's center travels
        [Min(0f)] public float UnitBaseSpeed = 9.7f;      // a unit's speed toward its slot; must exceed AnchorSpeed or units trail behind
        [Min(0f)] public float UnitSpeedDistGain = 2.4f;  // extra unit speed per unit of distance from its slot (catching up)...
        [Min(0f)] public float UnitSpeedDistCap = 9f;     // ...up to this much
        [Min(0.001f)] public float AnchorArrive = 0.024f; // closer than this to the destination = the squad has arrived
        [Min(0.001f)] public float UnitArrive = 0.015f;   // closer than this to its slot = the unit is standing and may fire

        [Header("Formation")]
        public float RotationStepDegrees = 15f;           // one mouse-wheel notch
        [Tooltip("Degrees per second the formation turns toward the wheel's angle. 0 = instantly.")]
        [Min(0f)] public float RotationSpeed = 0f;
        [Min(0.1f)] public float SeatSize = 0.5f;         // spacing of the seats pressed units take along a wall

        [Header("Attack: soldiers")]
        [Min(0f)] public float UnitDamage = 33f;                 // what one soldier's shot deals
        [Min(0.05f)] public float UnitAttackInterval = 0.76f;    // seconds from one shot to the next...
        [Min(0f)] public float UnitAttackIntervalRandom = 0.16f; // ...plus up to this much, rolled for every shot

        [Header("Attack: squad leader")]
        [Min(0f)] public float LeaderDamage = 33f;               // what the leader's throw deals
        [Min(0.05f)] public float LeaderAttackInterval = 1.35f;  // seconds from one throw to the next; shorter than the throw animation = the animation plays faster
        [Min(0f)] public float LeaderAttackIntervalRandom = 0f;

        [Header("Stationary attack streak")]
        public bool StationaryRampEnabled = true;
        [Min(0f)] public float SpeedGainPerHit = 0.01f;
        [Min(1f)] public float MaxAttackSpeedMultiplier = 1.25f;
        [Min(0f)] public float MovingSpeedDecayPerSecond = 0.1f;
        [Min(0f)] public float AttackStreakGraceSeconds = 1f;
        [Min(0.1f)] public float HitCounterHoldSeconds = 2f;

        [Header("Near miss")]
        [Min(0f)] public float NearMissMargin = 1f;           // a zone that catches nobody, with a unit this close outside its edge = near miss (0 = off)
        [Min(1f)] public float NearMissDamageMultiplier = 1.2f; // the squad's damage while the near-miss buff lasts
        [Min(0f)] public float NearMissBuffSeconds = 5f;      // how long the buff lasts; another near miss starts it over

        [Header("Revive")]
        [Min(0.1f)] public float ReviveSeconds = 3f;
        [Min(0f)] public float ReviveAppearRadius = 0.6f; // revived units appear this far from the reviver

        [Header("Camera")]
        [Min(0f)] public float CameraScrollSpeed = 14f;   // world units per second at a screen edge
        [Min(1f)] public float CameraEdgeWidth = 18f;     // pixels from the screen edge that count as "at the edge"
        [Min(1f)] public float CameraDistance = 34f;      // zoom: distance from the point the camera looks at
        [Tooltip("On: the camera stays on the squad and leans toward the mouse. Off: it only moves when the mouse pushes a screen edge.")]
        public bool CameraFollow = true;
        [Range(0f, 1f)] public float CameraLookAhead = 1f;      // how far it leans toward the mouse: 1 = until the squad's center reaches the keep margin
        [Range(0f, 0.45f)] public float CameraKeepMargin = 0.14f; // the squad's center never gets closer to a screen edge than this share of the screen
        [Range(0f, 0.9f)] public float CameraDeadZone = 0.12f;  // mouse this close to the screen center (share of the half screen) does not lean the camera
        [Min(0f)] public float CameraSmoothTime = 0.22f;        // seconds the camera takes to catch up with the mouse

        [Header("Mobs")]
        [Min(0f)] public float MobSwitchDistance = 5f;    // closer to the squad than this, a mob chases the nearest unit directly
        [Min(1)] public int MobMaxAlive = 200;
    }
}
