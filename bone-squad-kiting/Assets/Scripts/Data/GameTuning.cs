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

        [Header("Formation")]
        public float RotationStepDegrees = 15f;           // one mouse-wheel notch
        [Tooltip("Degrees per second the formation turns toward the wheel's angle. 0 = instantly.")]
        [Min(0f)] public float RotationSpeed = 0f;
        [Min(0.1f)] public float SeatSize = 0.5f;         // spacing of the seats pressed units take along a wall

        [Header("Revive")]
        [Min(0.1f)] public float ReviveSeconds = 3f;
        [Min(0f)] public float ReviveAppearRadius = 0.6f; // revived units appear this far from the formation's center

        [Header("Camera")]
        [Min(0f)] public float CameraScrollSpeed = 14f;   // world units per second at a screen edge
        [Min(1f)] public float CameraEdgeWidth = 18f;     // pixels from the screen edge that count as "at the edge"
        [Min(1f)] public float CameraDistance = 34f;      // zoom: distance from the point the camera looks at

        [Header("Mobs")]
        [Min(0f)] public float MobSwitchDistance = 5f;    // closer to the squad than this, a mob chases the nearest unit directly
        [Min(1)] public int MobMaxAlive = 200;
    }
}
