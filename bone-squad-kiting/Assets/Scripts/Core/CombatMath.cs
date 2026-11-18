using System.Collections.Generic;

namespace BoneSquadKiting.Core
{
    public static class CombatMath
    {
        /// <summary>Damage per second over the trailing DPS window. Events are (time, damage).</summary>
        public static float Dps(IList<(float t, float d)> events, float now)
        {
            float sum = 0;
            foreach (var e in events)
                if (now - e.t < GameConstants.DpsWindow) sum += e.d;
            return sum / GameConstants.DpsWindow;
        }

        public static bool IsPhase2(float hpFrac) => hpFrac < GameConstants.Phase2HpFrac;
    }
}
