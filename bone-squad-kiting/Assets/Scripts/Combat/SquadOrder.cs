using UnityEngine;

namespace BoneSquadKiting.Combat
{
    public enum OrderKind { Move, AttackMove, AttackTarget }

    /// <summary>The squad's current command. Point is Vector2(x, z).</summary>
    public struct SquadOrder
    {
        public OrderKind Kind;
        public Vector2 Point;
        public IEnemy Target;

        public static SquadOrder Move(Vector2 p) => new() { Kind = OrderKind.Move, Point = p };
        public static SquadOrder AttackMove(Vector2 p) => new() { Kind = OrderKind.AttackMove, Point = p };
        public static SquadOrder Attack(IEnemy t) => new() { Kind = OrderKind.AttackTarget, Target = t };
    }
}
