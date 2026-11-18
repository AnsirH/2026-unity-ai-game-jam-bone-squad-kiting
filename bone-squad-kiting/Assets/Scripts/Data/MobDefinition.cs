using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>One kind of swarm enemy. All numbers are starting values to tune by playing.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Mob")]
    public class MobDefinition : ScriptableObject
    {
        public string DisplayName = "Grunt";
        public float Radius = 0.3f;
        public float Speed = 3.2f;          // well under the squad's 7.4 so it can always be outrun
        public float Hp = 66f;              // two unit shots
        public float AttackDamage = 10f;
        public float AttackPeriod = 1.0f;   // seconds between hits
        public float AttackReach = 0.1f;    // extra distance beyond touching at which it can hit

        [Header("Ranged attack (Attack Range 0 = melee)")]
        public float AttackRange = 0f;      // stops this far from a unit and shoots instead of walking up to it
        public float ShotSpeed = 6f;        // the shot flies straight at where the unit was; a moving squad can step out of it
        public float ShotRadius = 0.25f;
        [Tooltip("0 keeps the existing constant-speed, distance-limited shot.")]
        [Min(0f)] public float ShotLifetime;
        public AnimationCurve ShotSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [Range(0f, 0.99f)] public float ShotFadeStart = 0.55f;
        public bool IsRanged => AttackRange > 0f;

        [Header("Looks (optional: empty = the placeholder sphere)")]
        public GameObject Prefab;           // model with an Animator; drawn with its feet on the ground
        public float ModelScale = 1f;       // multiplies the prefab's own scale
        public float ModelYawOffset = 0f;   // degrees around Y if the model's front is not +Z
        public float AttackAnimSeconds = 0.35f;   // how long the attack animation shows before going back to idle/run
        public AnimStates States = new() { Idle = "", Run = "Run", Attack = "Attack", Die = "" };
    }
}
