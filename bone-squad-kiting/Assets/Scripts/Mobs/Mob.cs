using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Mobs
{
    /// <summary>One swarm enemy. Plain data; MobSystem moves it and MobView draws it.</summary>
    public class Mob : IEnemy, IDamageable
    {
        public MobDefinition Def;
        public float X, Z;
        public float Health;
        public float AttackCd;
        public float Flash;

        public Vector2 Pos => new(X, Z);
        public bool Alive => Health > 0f;
        public float Radius => Def.Radius;
        public EnemyKind Kind => EnemyKind.Mob;
        public float Hp => Health;
        public float Reserved { get; set; }

        public void ApplyDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            Flash = 1f;
        }
    }

    /// <summary>A ranged mob's shot in the air. Plain data; MobSystem moves it.</summary>
    public class MobShot
    {
        public MobDefinition Def;
        public float X, Z;
        public Vector2 Dir;
        public float Left;      // distance it may still travel
        public Vector2 Pos => new(X, Z);
    }
}
