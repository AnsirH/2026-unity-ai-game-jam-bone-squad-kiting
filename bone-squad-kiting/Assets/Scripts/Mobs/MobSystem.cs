using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Mobs
{
    /// <summary>
    /// Simulates the swarm. Each frame: steer (flow field far away, nearest unit up close), move,
    /// separate mobs from each other, push them out of units and elites, keep them on walkable ground,
    /// then let the ones in reach hit a unit.
    /// </summary>
    public class MobSystem
    {
        public readonly List<Mob> Mobs = new();

        public float SwitchDistance = 5f;   // closer to the squad anchor than this: chase the nearest unit directly
        public float UnitRadius = 0.25f;
        public int MaxAlive = 200;

        /// <summary>Shots in the air from ranged mobs. MobView draws them.</summary>
        public readonly List<MobShot> Shots = new();

        public System.Action<SquadUnit, float> OnUnitHit;   // unit, damage
        public event System.Action<MobShot, SquadUnit> OnProjectileHit;
        public System.Action<Mob> OnMobDied;

        /// <summary>Units killed by mobs during the last Tick. The caller reforms the squad when it is non-zero.</summary>
        public int UnitsKilledLastTick { get; private set; }

        public int AliveCount
        {
            get { int n = 0; foreach (var m in Mobs) if (m.Alive) n++; return n; }
        }

        /// <summary>Add a mob. Returns null when the alive cap is reached.</summary>
        public Mob Spawn(MobDefinition def, Vector2 pos, EnemyRegistry registry)
        {
            if (AliveCount >= MaxAlive) return null;
            var m = new Mob { Def = def, X = pos.x, Z = pos.y, Health = def.Hp, AttackCd = Random.value * def.AttackPeriod };
            Mobs.Add(m);
            registry?.Register(m);
            return m;
        }

        public void Clear(EnemyRegistry registry)
        {
            foreach (var m in Mobs)
            {
                m.PendingJavelin = null;
                registry?.Unregister(m);
            }
            Mobs.Clear();
            Shots.Clear();
        }

        public void Tick(FormationController squad, EnemyRegistry registry, WalkGrid grid, FlowField flow, float dt)
        {
            UnitsKilledLastTick = 0;
            RemoveDead(registry);
            StepShots(squad, grid, dt);
            if (Mobs.Count == 0) return;

            flow?.Retarget(squad.Anchor);

            // 1-2. steer and move
            var before = new Vector2[Mobs.Count];
            for (int i = 0; i < Mobs.Count; i++)
            {
                var m = Mobs[i];
                before[i] = m.Pos;
                m.Flash = Mathf.Max(0f, m.Flash - dt * 3f);
                m.AttackCd -= dt;
                var dir = Steer(m, squad, flow);
                m.X += dir.x * m.Def.Speed * dt;
                m.Z += dir.y * m.Def.Speed * dt;
            }

            // 3. mobs push each other apart (half the overlap each)
            for (int i = 0; i < Mobs.Count; i++)
                for (int j = i + 1; j < Mobs.Count; j++)
                    Separate(Mobs[i], Mobs[j]);

            // 4. units and elites do not move for mobs: the mob takes the whole overlap
            foreach (var m in Mobs)
            {
                foreach (var u in squad.Units)
                    if (!u.Dead) PushOut(m, new Vector2(u.X, u.Z), UnitRadius);
                if (registry == null) continue;
                foreach (var e in registry.Enemies)
                    if (e.Kind == EnemyKind.Elite && e.Alive) PushOut(m, e.Pos, e.Radius);
            }

            // 5. stay on walkable ground, sliding along walls
            if (grid != null)
                for (int i = 0; i < Mobs.Count; i++)
                {
                    var m = Mobs[i];
                    if (grid.IsWalkable(m.Pos)) continue;
                    var p = grid.Slide(before[i], m.Pos);
                    m.X = p.x; m.Z = p.y;
                }

            // 6. attack
            foreach (var m in Mobs)
            {
                if (m.PendingJavelin != null)
                {
                    m.WindupLeft -= dt;
                    if (m.WindupLeft <= 0f) ReleaseJavelin(m);
                    continue;
                }
                if (m.AttackCd > 0f) continue;
                if (m.Def.IsRanged)
                {
                    var aim = NearestUnit(squad, m.Pos, m.Def.AttackRange);
                    if (aim == null) continue;
                    m.AttackCd = m.Def.AttackPeriod;
                    var to = new Vector2(aim.X, aim.Z) - m.Pos;
                    var dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.up;
                    var shot = new MobShot { X = m.X, Z = m.Z, Dir = dir, Def = m.Def, Left = m.Def.AttackRange * 1.5f };
                    if (m.Def.JavelinThrowClip == null) Shots.Add(shot);
                    else
                    {
                        m.PendingJavelin = shot;
                        m.WindupLeft = m.HasThrowAnimator ? m.Def.AttackAnimSeconds : m.Def.JavelinReleaseTime;
                    }
                    continue;
                }
                var target = NearestUnit(squad, m.Pos, m.Def.Radius + UnitRadius + m.Def.AttackReach);
                if (target == null) continue;
                m.AttackCd = m.Def.AttackPeriod;
                var took = SquadDamage.Apply(squad.Units, target, m.Def.AttackDamage);   // the leader's hits land on someone else
                OnUnitHit?.Invoke(took, m.Def.AttackDamage);
                if (took.Dead) UnitsKilledLastTick++;
            }
        }

        // Far from the squad every mob shares one goal (the anchor) so a single flow field serves them all.
        // Up close each chases the unit nearest to it, and stops once it can reach that unit.
        Vector2 Steer(Mob m, FormationController squad, FlowField flow)
        {
            // A ranged mob stops a little inside its range, so a small step by the squad does not put it out of range.
            if (m.Def.IsRanged && NearestUnit(squad, m.Pos, m.Def.AttackRange * 0.85f) != null) return Vector2.zero;
            var toAnchor = squad.Anchor - m.Pos;
            if (toAnchor.magnitude < SwitchDistance)
            {
                var unit = NearestUnit(squad, m.Pos, float.MaxValue);
                if (unit == null) return Vector2.zero;
                var toUnit = new Vector2(unit.X, unit.Z) - m.Pos;
                if (toUnit.magnitude <= m.Def.Radius + UnitRadius + m.Def.AttackReach * 0.5f) return Vector2.zero;
                return toUnit.normalized;
            }
            if (flow != null)
            {
                var dir = flow.DirectionAt(m.Pos);
                if (dir != Vector2.zero) return dir;
            }
            return toAnchor.sqrMagnitude > 1e-6f ? toAnchor.normalized : Vector2.zero;
        }

        public bool ReleaseJavelin(Mob mob)
        {
            var shot = mob.PendingJavelin;
            if (!mob.Alive || shot == null) return false;
            mob.PendingJavelin = null;
            shot.X = mob.X;
            shot.Z = mob.Z;
            Shots.Add(shot);
            Audio.CastSounds.ThrowJavelin();
            return true;
        }

        // Shots fly straight. One hits the first living unit it touches; walls and its travel limit end it.
        void StepShots(FormationController squad, WalkGrid grid, float dt)
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                float multiplier = s.Def.ShotLifetime > 0f && s.Def.ShotSpeedCurve != null
                    ? Mathf.Max(0f, s.Def.ShotSpeedCurve.Evaluate(s.Age / s.Def.ShotLifetime)) : 1f;
                float step = s.Def.ShotSpeed * multiplier * dt;
                s.Age += dt;
                s.X += s.Dir.x * step; s.Z += s.Dir.y * step; s.Left -= step;
                var hit = NearestUnit(squad, s.Pos, s.Def.ShotRadius + UnitRadius);
                if (hit != null)
                {
                    var took = SquadDamage.Apply(squad.Units, hit, s.Def.AttackDamage);
                    OnUnitHit?.Invoke(took, s.Def.AttackDamage);
                    OnProjectileHit?.Invoke(s, took);
                    if (took.Dead) UnitsKilledLastTick++;
                }
                bool expired = s.Def.ShotLifetime > 0f ? s.Age >= s.Def.ShotLifetime : s.Left <= 0f;
                if (hit != null || expired || (grid != null && !grid.IsWalkable(s.Pos))) Shots.RemoveAt(i);
            }
        }

        static SquadUnit NearestUnit(FormationController squad, Vector2 from, float range)
        {
            SquadUnit best = null; float bestSqr = range * range;
            foreach (var u in squad.Units)
            {
                if (u.Dead) continue;
                float d = (new Vector2(u.X, u.Z) - from).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = u; }
            }
            return best;
        }

        /// <summary>If two mobs overlap, move each half the overlap away from the other.</summary>
        public static void Separate(Mob a, Mob b)
        {
            var d = b.Pos - a.Pos;
            float min = a.Radius + b.Radius, dist = d.magnitude;
            if (dist >= min) return;
            var n = dist > 1e-5f ? d / dist : new Vector2(1f, 0f); // exactly stacked: pick a direction
            float half = (min - dist) * 0.5f;
            a.X -= n.x * half; a.Z -= n.y * half;
            b.X += n.x * half; b.Z += n.y * half;
        }

        /// <summary>If the mob overlaps a fixed circle, move the mob out by the whole overlap.</summary>
        public static void PushOut(Mob m, Vector2 center, float radius)
        {
            var d = m.Pos - center;
            float min = m.Radius + radius, dist = d.magnitude;
            if (dist >= min) return;
            var n = dist > 1e-5f ? d / dist : new Vector2(1f, 0f);
            m.X = center.x + n.x * min; m.Z = center.y + n.y * min;
        }

        void RemoveDead(EnemyRegistry registry)
        {
            for (int i = Mobs.Count - 1; i >= 0; i--)
            {
                if (Mobs[i].Alive) continue;
                registry?.Unregister(Mobs[i]);
                OnMobDied?.Invoke(Mobs[i]);
                Mobs.RemoveAt(i);
            }
        }
    }
}
