namespace BoneSquadKiting.Core
{
    /// <summary>
    /// All gameplay constants, copied 1:1 from the KITE SQUAD 25 HTML prototype.
    /// World coordinates map directly to Unity's XZ plane (prototype x -> x, z -> z, height -> y).
    /// This is the single source of truth for tuning numbers.
    /// </summary>
    public static class GameConstants
    {
        public const float BossMax = 24000f;
        public const float UnitMax = 100f;
        public const float UnitDamage = 33f;
        public const int SquadSize = 25;

        // Boss arena position (prototype BOSS)
        public const float BossX = 0f;
        public const float BossZ = -7.4f;

        // Hard arena limits (prototype LIMIT)
        public const float LimitXMin = -12.3f, LimitXMax = 12.3f;
        public const float LimitZMin = -10.2f, LimitZMax = 10.2f;

        // Destination clamp for the commander (prototype moveToPoint)
        public const float MoveClampX = 8.0f;
        public const float MoveClampZMin = -3.85f, MoveClampZMax = 4.2f;

        // Movement
        public const float AnchorSpeed = 7.4f;
        public const float UnitBaseSpeed = 9.7f;   // + min(9, dist*2.4)
        public const float UnitSpeedDistGain = 2.4f, UnitSpeedDistCap = 9f;
        public const float AnchorArrive = 0.024f, UnitArrive = 0.015f;

        // Combat
        public const float FireRange = 15.9f;
        public const float ShotSpeed = 22f;
        public const float ShotLife = 1.35f;
        public const float ShotHitRadius = 1.15f;
        public const float UnitCdMin = 0.76f, UnitCdRand = 0.16f;
        public const float PatternUnitDamage = 55f;

        // DPS window
        public const float DpsWindow = 2.4f;

        // Boss phase 2
        public const float Phase2HpFrac = 0.54f;
        public const float NextAttackPhase1 = 1.9f, NextAttackPhase2 = 1.45f;
        public const float FirstAttackDelay = 3.1f;
        // Attack cadence switches at 0.55 while the phase flag switches at 0.54. The prototype really uses two
        // different values here (resolvePattern vs update), so this is intentional.
        public const float FastAttackHpFrac = 0.55f;

        // Telegraph lock: last 0.38s the zone stops tracking the commander
        public const float CastTrackLockTime = 0.38f;
    }
}
