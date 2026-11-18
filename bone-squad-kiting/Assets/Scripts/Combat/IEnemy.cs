using UnityEngine;

namespace BoneSquadKiting.Combat
{
    /// <summary>Anything the squad can target. Pos is Vector2(x, z) in world space.</summary>
    public interface IEnemy
    {
        Vector2 Pos { get; }
        bool Alive { get; }
    }
}
