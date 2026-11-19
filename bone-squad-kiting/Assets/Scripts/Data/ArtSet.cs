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

        /// <summary>How a mob or a mini boss leaves when it dies: one clip for every humanoid enemy, then ash.</summary>
        [System.Serializable]
        public class EnemyDeathLook
        {
            public AnimationClip Clip;                  // humanoid; empty = enemies vanish at once, as before
            public float DissolveStart = 0.9f;          // seconds into the clip the body reaches the ground
            public float DissolveSeconds = 0.9f;        // how long it takes to turn to ash and go
            public Color AshColor = new(0.16f, 0.15f, 0.14f, 1f);
            [ColorUsage(false, true)] public Color EdgeColor = new(1.5f, 0.75f, 0.35f, 1f);
            [Range(0f, 0.25f)] public float EdgeWidth = 0.08f;
            public Shader DissolveShader;               // used for models whose own material cannot dissolve
            public int MaxAtOnce = 40;                  // more deaths than this at the same time just vanish
        }

        [Header("Enemy death (mobs and mini bosses)")]
        public EnemyDeathLook EnemyDeath = new();

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
