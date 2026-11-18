using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Runs the mini bosses: each walks toward the squad, stops to telegraph an area attack (the zone is
    /// fixed where the squad stood when the cast began), resolves it, and walks on.
    /// </summary>
    public class EliteSystem
    {
        public readonly List<Elite> Elites = new();
        public float UnitRadius = 0.25f;

        public System.Action<Elite, PatternDefinition> OnPatternStarted;
        public System.Action<Elite, int, int> OnPatternResolved;   // elite, units hit, units lost
        public System.Action<Elite> OnDied;

        public Elite Spawn(EliteDefinition def, Vector2 pos, EnemyRegistry registry)
        {
            var e = new Elite { Def = def, X = pos.x, Z = pos.y, Health = def.MaxHp };
            e.Attacks = new BossAttackSystem
            {
                Sequence = def.Patterns,
                NextAttack = def.FirstAttackDelay,
                TrackSquad = false,          // the zone stays where it was announced, so it can be walked out of
                FixedGap = def.AttackGap,
            };
            e.Attacks.OnStart += p => OnPatternStarted?.Invoke(e, p);
            e.Attacks.OnResolved += (hit, lost) => OnPatternResolved?.Invoke(e, hit, lost);
            Elites.Add(e);
            registry?.Register(e);
            return e;
        }

        public void Clear(EnemyRegistry registry)
        {
            foreach (var e in Elites) registry?.Unregister(e);
            Elites.Clear();
        }

        public void Tick(FormationController squad, EnemyRegistry registry, WalkGrid grid, FlowField flow, float dt)
        {
            for (int i = Elites.Count - 1; i >= 0; i--)
            {
                var e = Elites[i];
                if (!e.Alive)
                {
                    registry?.Unregister(e);
                    Elites.RemoveAt(i);
                    OnDied?.Invoke(e);
                    continue;
                }
                e.Flash = Mathf.Max(0f, e.Flash - dt * 3f);
                e.Attacks.Tick(squad, null, dt);
                if (e.Casting) continue;   // it stands still while casting: the squad's chance to shoot — or to move

                var dir = Steer(e, squad, flow);
                if (dir == Vector2.zero) continue;
                var next = e.Pos + dir * (e.Def.Speed * dt);
                if (grid != null) next = grid.Sweep(e.Pos, next);
                e.X = next.x; e.Z = next.y;
            }
        }

        Vector2 Steer(Elite e, FormationController squad, FlowField flow)
        {
            float nearest = float.MaxValue; Vector2 toNearest = default;
            foreach (var u in squad.Units)
            {
                if (u.Dead) continue;
                var d = new Vector2(u.X, u.Z) - e.Pos;
                if (d.magnitude < nearest) { nearest = d.magnitude; toNearest = d; }
            }
            if (nearest <= e.Def.StopDistance) return Vector2.zero;
            if (flow != null)
            {
                var routed = flow.DirectionAt(e.Pos);
                if (routed != Vector2.zero) return routed;
            }
            var toAnchor = squad.Anchor - e.Pos;
            return toAnchor.sqrMagnitude > 1e-6f ? toAnchor.normalized : Vector2.zero;
        }
    }
}
