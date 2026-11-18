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

        [Header("Fill effect (burst patterns)")]
        [Tooltip("When the area goes off, copies of this effect are spread over the whole area. Empty = none.")]
        public GameObject FillEffect;
        [Tooltip("About how far apart the copies stand, in metres. Smaller = more copies.")]
        public float FillSpacing = 2f;
        [Tooltip("Size of each copy, as a multiple of the prefab's own size.")]
        public float FillScale = 1f;
        [Tooltip("How long the copies stay before they are taken away.")]
        public float FillSeconds = 1f;

        [Header("Strike effect (burst patterns)")]
        [Tooltip("One copy per part of the area flies in from the caster's side and sticks in the ground, lying along the area, as it goes off. Empty = none.")]
        public GameObject StrikeEffect;
        [Tooltip("How long before the area goes off the copies appear and start flying.")]
        public float StrikeFlightSeconds = 0.35f;
        [Tooltip("Size on landing: 1 = as long as the part of the area it lands on.")]
        public float StrikeSize = 1f;
        [Tooltip("How steeply it sticks in, in degrees from the ground. Small = almost lying down.")]
        public float StrikePitch = 12f;
        [Tooltip("How far it flies before it lands, as a multiple of its landed length.")]
        public float StrikeTravel = 1f;
        [Tooltip("How long the copies stay stuck in the ground.")]
        public float StrikeStaySeconds = 0.8f;
        [Tooltip("How long the copies take to fade out after the stay.")]
        public float StrikeFadeSeconds = 0.4f;
        [Tooltip("Played where each copy sticks into the ground (debris). Empty = none.")]
        public GameObject StrikeImpactEffect;
        [Tooltip("Size of the impact effect, as a share of the landed copy's length.")]
        public float StrikeImpactScale = 0.3f;
        [Tooltip("Camera shake when the copies land: strength (0 = none) and how long it lasts.")]
        public float StrikeShake = 0.35f;
        public float StrikeShakeSeconds = 0.3f;

        [Header("Sustained patterns only")]
        public PatternKind Kind = PatternKind.Burst;
        public float SustainSeconds = 4f;   // how long the zone stays live after the telegraph
        public float TickInterval = 0.5f;   // seconds between damage ticks while it is live
        public float TickDamage = 15f;      // damage per tick to each unit inside
    }
}
