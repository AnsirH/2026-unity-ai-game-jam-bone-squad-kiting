using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Combat
{
    /// <summary>Mob = one of the swarm. Elite = a boss or epic monster, the squad's default target.</summary>
    public enum EnemyKind { Mob, Elite }

    /// <summary>Anything the squad can target. Pos is Vector2(x, z) in world space.</summary>
    public interface IEnemy
    {
        Vector2 Pos { get; }
        bool Alive { get; }

        /// <summary>Body radius: shots hit inside it, mobs are pushed out of it.</summary>
        float Radius => GameConstants.ShotHitRadius;

        EnemyKind Kind => EnemyKind.Elite;

        /// <summary>Remaining health, for deciding whether more shots are worth sending.</summary>
        float Hp => Alive ? float.MaxValue : 0f;

        /// <summary>Damage already in flight toward this enemy. Shots add on firing and remove on landing.</summary>
        float Reserved { get => 0f; set { } }
    }
}
