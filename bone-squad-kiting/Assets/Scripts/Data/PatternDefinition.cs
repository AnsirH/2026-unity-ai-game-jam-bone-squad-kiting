using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Data
{
    /// <summary>Burst: goes off once when the telegraph ends. Sustained: then stays and damages at intervals.</summary>
    public enum PatternKind { Burst, Sustained }

    /// <summary>One boss attack: telegraph time, the formation that dodges it, and its danger geometry.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Pattern")]
    public class PatternDefinition : ScriptableObject
    {
        public PatternId Id;
        public string DisplayName, Advice, Detail;
        public string Recommend;   // FormationDefinition.Id that dodges this pattern
        public float Time;         // telegraph duration in seconds
        public DangerShape Shape;  // size for a full squad
        // When set, the shape shrinks with the squad (alive / full), the same way the ring formation does,
        // so the formation that dodges it keeps dodging it as units are lost.
        public bool ScaleWithSquad;
        [Tooltip("Use a dark crimson warning for a lethal attack. Visual only.")]
        public bool LethalTelegraph;

        [Tooltip("One effect to spawn at the center of the entire burst when its warning ends.")]
        public GameObject BurstEffect;
        public Material LaneWarningMaterial;

        [Header("Sustained patterns only")]
        public PatternKind Kind = PatternKind.Burst;
        public float SustainSeconds = 4f;   // how long the zone stays live after the telegraph
        public float TickInterval = 0.5f;   // seconds between damage ticks while it is live
        public float TickDamage = 15f;      // damage per tick to each unit inside
    }
}
