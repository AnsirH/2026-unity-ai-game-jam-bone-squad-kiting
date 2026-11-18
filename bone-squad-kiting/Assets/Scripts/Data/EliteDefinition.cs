using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>
    /// A mini boss (epic monster): tougher than a mob, walks toward the squad and stops to cast area
    /// attacks from its pattern sequence. Numbers are starting values to tune by playing.
    /// </summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Elite (Mini Boss)")]
    public class EliteDefinition : ScriptableObject
    {
        public string DisplayName = "미니 보스";
        public float MaxHp = 4000f;
        public float Speed = 2f;              // slower than mobs (3.2): the swarm arrives first. 0 = fixed in place (a cannon)
        public float Radius = 0.8f;
        public float StopDistance = 3f;       // stops walking this close to the nearest unit

        [Header("Area attacks")]
        public AttackSequence Patterns;
        public float FirstAttackDelay = 3f;   // seconds after appearing before the first cast
        public float AttackGap = 4.5f;        // seconds between casts
        public float AttackGapRandom = 0f;    // up to this many extra seconds, picked anew for each gap (irregular fire)
        public float ZoneRotationSpeed = 0f;  // degrees per second its zones turn; sign = direction (+ = clockwise), 0 = no turning
        public float AttackRange = 16f;       // only casts while the squad's anchor is this close; 0 = any distance

        [Header("Intro / death moments (game is frozen, camera goes to the mini boss)")]
        public float IntroDuration = 2f;
        public float DeathDuration = 1.5f;
        public GameObject IntroFx;            // optional prefab spawned at the mini boss when it appears
        public GameObject DeathFx;            // optional prefab spawned where it dies
    }
}
