using BoneSquadKiting.Combat;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// The one place that decides whether a unit may fire right now. ShotSystem asks here before
    /// acquiring a target, so changing the rule never touches projectile code.
    /// </summary>
    public static class FireRules
    {
        /// <param name="unit">The unit that wants to fire (unused today; here for a per-unit rule).</param>
        /// <param name="order">The squad's current order.</param>
        /// <param name="squadMoving">True while the squad anchor is still walking to its destination.</param>
        public static bool CanFire(SquadUnit unit, SquadOrder order, bool squadMoving)
        {
            // Decided: a Move order is a pure move. No firing on the way; auto-fire resumes on arrival.
            if (order.Kind == OrderKind.Move && squadMoving) return false;

            // Open questions, deliberately not guessed at:
            //  1. Whether "moving" should be judged per unit (e.g. still sliding to a new formation slot)
            //     instead of by the squad anchor.
            //  2. What AttackMove does on the way to its point. Today it keeps firing while it walks.
            return true;
        }
    }
}
