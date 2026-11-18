using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Bosses
{
    /// <summary>A mini boss in play: position, health and its own pattern caster.</summary>
    public class Elite : IEnemy, IDamageable
    {
        public EliteDefinition Def;
        public float X, Z;
        public float Health;
        public float Flash;
        public BossAttackSystem Attacks;

        /// <summary>The rest of its entry route. While this is set it is walking in: it neither chases nor casts.</summary>
        public System.Collections.Generic.List<Vector2> Path;
        public int PathIndex;
        public float EntrySpeed = 4f;
        public bool Entering => Path != null;
        public Vector2 Heading;     // the way it last walked; used to turn its model while it walks in

        public Vector2 Pos => new(X, Z);
        public bool Alive => Health > 0f;
        public float Radius => Def.Radius;
        public EnemyKind Kind => EnemyKind.Elite;
        public float Hp => Health;
        public float HpFrac => Def.MaxHp > 0f ? Health / Def.MaxHp : 0f;
        public bool Casting => Attacks != null && Attacks.Current.HasValue;

        public void ApplyDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
            Flash = 0.8f;
        }
    }
}
