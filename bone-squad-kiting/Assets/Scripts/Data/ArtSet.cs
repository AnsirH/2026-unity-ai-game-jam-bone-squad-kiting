using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>
    /// Which Animator state to play for each thing a character can be doing. Names are matched against the
    /// states in the prefab's Animator; an empty name, or a name the Animator does not have, is simply skipped.
    /// </summary>
    [System.Serializable]
    public class AnimStates
    {
        public string Idle = "Idle";
        public string Run = "Run";
        public string Attack = "Attack";
        public string Die = "Die";
        public string Revive = "";       // while reviving a comrade
        public float CrossFade = 0.1f;   // blend time between states, in seconds
    }

    /// <summary>
    /// The shared look of the game: one asset that every stage's views read. Put a prefab in a slot and all
    /// five stages use it; leave a slot empty and the placeholder shape stays.
    /// </summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Art Set")]
    public class ArtSet : ScriptableObject
    {
        [System.Serializable]
        public class EnemyProjectile
        {
            public MobDefinition Enemy;
            public GameObject Prefab;
            public GameObject HitEffectPrefab;
            public float Scale = 1f;
            public Vector3 Rotation;
        }

        [Header("Enemy projectiles (one entry per enemy type)")]
        public EnemyProjectile[] EnemyProjectiles = System.Array.Empty<EnemyProjectile>();
        [Header("Squad units (24 soldiers share one model; the leader has its own)")]
        public GameObject UnitPrefab;
        public float UnitScale = 1f;          // multiplies the prefab's own scale, to fit it to the game's unit spacing
        public AnimStates UnitStates = new();

        public GameObject LeaderPrefab;
        public float LeaderScale = 1f;
        public AnimStates LeaderStates = new();

        [Tooltip("Degrees to turn the models around Y if their front is not +Z.")]
        public float ModelYawOffset = 0f;
        public float TurnSpeed = 720f;        // degrees per second a unit turns to face where it walks or shoots

        [Header("Revive fragments")]
        public GameObject BrokenBone;
        public GameObject Vertebra;
        public GameObject Scapula;

        [Header("Projectile")]
        public GameObject ShotPrefab;
        public GameObject LaunchFxPrefab;
        public float ShotScale = 1f;
        [Tooltip("Turn the model so its tip points along +Z (the way it flies).")]
        public Vector3 ShotRotation;

        [Header("The leader's projectile (falls back to the one above when empty)")]
        public GameObject LeaderShotPrefab;
        public float LeaderShotScale = 1f;
        [Tooltip("Turn the model so its tip points along +Z (the way it flies).")]
        public Vector3 LeaderShotRotation;
    }
}
