using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// The revive action. Each use pairs one living unit (the reviver) with one dead unit; after
    /// <see cref="Duration"/> seconds the dead unit comes back at full health near the formation center.
    /// Several revives can run side by side. The reviver keeps its place in the formation and cannot
    /// fire while reviving; if it dies, that revive is cancelled.
    /// </summary>
    public class ReviveSystem
    {
        public const float ConvergenceStart = 0.84f;

        public class Revive
        {
            public SquadUnit Reviver, Target;
            public Vector2 AppearOffset;
            public float Elapsed;
            public bool Assembling;
        }

        public float Duration = 3f;
        public float AppearRadius = 0.6f;   // revived units appear this far from the anchor (the center itself is kept free)

        public readonly List<Revive> Active = new();

        public System.Action<SquadUnit, SquadUnit> OnStarted;     // reviver, dead unit
        public System.Action OnAssemblyStarted;
        public System.Action<SquadUnit, SquadUnit> OnCompleted;   // reviver, revived unit
        public System.Action<SquadUnit, SquadUnit> OnCancelled;   // reviver (now dead), unit that stays dead

        /// <summary>Units completed by the last Tick. The caller reforms the squad when it is non-zero.</summary>
        public int CompletedLastTick { get; private set; }

        public void Clear()
        {
            foreach (var r in Active) r.Reviver.Reviving = false;
            Active.Clear();
        }

        /// <summary>
        /// Start one revive. Does nothing (returns false) when no dead unit is waiting or no living unit is free.
        /// </summary>
        public bool TryStart(IReadOnlyList<SquadUnit> units)
        {
            SquadUnit target = null;
            foreach (var u in units)
                if (u.Dead && !IsBeingRevived(u)) { target = u; break; }
            if (target == null) return false;

            var reviver = PickReviver(units);
            if (reviver == null) return false;

            reviver.Reviving = true;
            Active.Add(new Revive
            {
                Reviver = reviver,
                Target = target,
                AppearOffset = Random.insideUnitCircle.normalized * AppearRadius
            });
            OnStarted?.Invoke(reviver, target);
            return true;
        }

        // The one place that chooses who revives: at random among living units that are free and not busy
        // shooting; if every free unit is shooting, at random among all free units.
        SquadUnit PickReviver(IReadOnlyList<SquadUnit> units)
        {
            var idle = new List<SquadUnit>(); var busy = new List<SquadUnit>();
            foreach (var u in units)
            {
                if (u.Dead || u.Reviving) continue;
                (IsShooting(u) ? busy : idle).Add(u);
            }
            var pool = idle.Count > 0 ? idle : busy;
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        // A unit counts as shooting while its weapon is still cooling down from a recent shot.
        static bool IsShooting(SquadUnit u) => u.Cd > 0f;

        bool IsBeingRevived(SquadUnit u)
        {
            foreach (var r in Active) if (r.Target == u) return true;
            return false;
        }

        /// <summary>Advance all revives. The gauge keeps filling whether or not the squad is moving.</summary>
        public void Tick(Vector2 anchor, float dt)
        {
            CompletedLastTick = 0;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var r = Active[i];
                if (r.Reviver.Dead)
                {
                    r.Reviver.Reviving = false;
                    Active.RemoveAt(i);
                    OnCancelled?.Invoke(r.Reviver, r.Target);
                    continue;
                }
                r.Elapsed += dt;
                if (!r.Assembling && r.Elapsed >= Duration * ConvergenceStart)
                {
                    r.Assembling = true;
                    OnAssemblyStarted?.Invoke();
                }
                if (r.Elapsed < Duration) continue;

                r.Reviver.Reviving = false;
                var spot = anchor + r.AppearOffset;
                r.Target.Dead = false;
                r.Target.Hp = GameConstants.UnitMax;
                r.Target.X = spot.x; r.Target.Z = spot.y;
                r.Target.Flash = 1f;
                Active.RemoveAt(i);
                CompletedLastTick++;
                OnCompleted?.Invoke(r.Reviver, r.Target);
            }
        }

        /// <summary>0..1 for a reviver's current revive, or -1 if it is not reviving.</summary>
        public float ProgressOf(SquadUnit reviver)
        {
            foreach (var r in Active) if (r.Reviver == reviver) return Mathf.Clamp01(r.Elapsed / Duration);
            return -1f;
        }
    }
}
