using UnityEngine;

namespace BoneSquadKiting.Audio
{
    /// <summary>
    /// The squad's footsteps: while the formation is travelling, one of a few step sounds plays at a walking
    /// beat. It is one sound for the whole squad, not one per soldier.
    /// </summary>
    public sealed class Footsteps
    {
        public static readonly string[] Ids = { "squad.footstep.1", "squad.footstep.2", "squad.footstep.3" };
        public float Interval = 0.3f;         // seconds between steps while moving
        public float MinSpeed = 0.5f;         // slower than this (metres per second) is standing still

        float _untilNext;
        int _last = -1;

        /// <summary>Call once per game step with how far the formation's center moved in it.</summary>
        public void Tick(float moved, float dt)
        {
            if (dt <= 0f || moved / dt < MinSpeed) { _untilNext = 0f; return; }   // the first step sounds as soon as it sets off
            _untilNext -= dt;
            if (_untilNext > 0f) return;
            _untilNext = Interval;
            int pick = Random.Range(0, Ids.Length);
            if (pick == _last) pick = (pick + 1) % Ids.Length;                    // never the same step twice in a row
            _last = pick;
            SoundManager.Current?.PlaySfx(Ids[pick]);
        }
    }
}
