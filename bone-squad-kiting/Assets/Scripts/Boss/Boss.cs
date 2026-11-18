using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Bosses
{
    /// <summary>Boss state. Stationary in M1; M2 enemies carry their own moving Pos.</summary>
    public class Boss : IEnemy, IDamageable
    {
        public float Hp = GameConstants.BossMax;
        public int Phase = 1;
        public float Flash;

        public float HpFrac => Hp / GameConstants.BossMax;
        public Vector2 Pos => new(GameConstants.BossX, GameConstants.BossZ);
        public bool Alive => Hp > 0;
        public float Radius => GameConstants.ShotHitRadius;
        public EnemyKind Kind => EnemyKind.Elite;
        float IEnemy.Hp => Hp;

        public void ApplyDamage(float amount)
        {
            Hp = Mathf.Max(0, Hp - amount);
            Flash = 0.8f;
        }
    }
}
