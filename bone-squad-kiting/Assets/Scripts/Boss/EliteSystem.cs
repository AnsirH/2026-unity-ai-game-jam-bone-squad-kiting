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
        public System.Action<Elite> OnArrived;   // finished walking its entry route

        /// <summary>
        /// Put a mini boss in play. With a route it appears at the route's first point and walks through
        /// the rest (the last point is where it ends up) before it starts to fight.
        /// </summary>
        public Elite Spawn(EliteDefinition def, Vector2 pos, EnemyRegistry registry, List<Vector2> route = null, float entrySpeed = 4f)
        {
            var e = new Elite { Def = def, X = pos.x, Z = pos.y, Health = def.MaxHp };
            if (route != null && route.Count > 1)
            {
                e.X = route[0].x; e.Z = route[0].y;
                e.Path = route; e.PathIndex = 1; e.EntrySpeed = entrySpeed;
            }
            e.Attacks = new BossAttackSystem
            {
                Sequence = def.Patterns,
                NextAttack = def.FirstAttackDelay + Random.value * def.AttackGapRandom,
                TrackSquad = false,          // the zone stays where it was announced, so it can be walked out of
                FixedGap = def.AttackGap,
                FixedGapRandom = def.AttackGapRandom,
                RotationSpeed = def.ZoneRotationSpeed,
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
                if (e.Entering)
                {
                    // Walking in along its route: straight from point to point, walls or not.
                    float left = e.EntrySpeed * dt;
                    while (left > 0f && e.Path != null)
                    {
                        var to = e.Path[e.PathIndex] - e.Pos;
                        float d = to.magnitude;
                        if (d > 1e-4f) e.Heading = to / d;
                        if (d <= left) { e.X = e.Path[e.PathIndex].x; e.Z = e.Path[e.PathIndex].y; left -= d; if (++e.PathIndex >= e.Path.Count) e.Path = null; }
                        else { e.X += to.x / d * left; e.Z += to.y / d * left; left = 0f; }
                    }
                    if (!e.Entering) OnArrived?.Invoke(e);
                    continue;
                }
                // A cast in progress always finishes; a new one only starts while the squad is within reach.
                bool inReach = e.Def.AttackRange <= 0f || (squad.Anchor - e.Pos).magnitude <= e.Def.AttackRange;
                if (e.Casting || inReach) e.Attacks.Tick(squad, null, dt);
                if (e.Casting || e.Def.Speed <= 0f) continue;   // it stands still while casting: the squad's chance to shoot — or to move

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
