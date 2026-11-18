using BoneSquadKiting.Combat;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// The one place that decides whether a unit may fire right now. ShotSystem asks here before
    /// acquiring a target, so changing the rule never touches projectile code.
    /// </summary>
    public static class FireRules
    {
        /// <summary>
        /// A unit fires only while it stands still. This holds for every order: walking with the squad,
        /// or sliding to a new slot after a formation change, both silence it until it arrives.
        /// Units that are stopped but out of range simply do not fire; they never leave formation to close in.
        /// A unit that is reviving a comrade does not fire either.
        /// </summary>
        /// <param name="order">Unused today. Kept so an order-specific rule (e.g. when an attack-move
        /// squad should stop) can be added here without changing callers.</param>
        public static bool CanFire(SquadUnit unit, SquadOrder order) => !unit.Moving && !unit.Reviving;
    }
}
