using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Game
{
    public enum GamePhase { Menu, Play, Win, Lose }

    /// <summary>Owns the match: creates the systems, ticks them in order, and decides win / lose.</summary>
    public class GameDirector : MonoBehaviour
    {
        const float MaxDt = 0.042f;

        public FormationController Squad;   // assigned in the scene
        public AttackSequence Sequence;     // boss pattern sequence asset
        public BossView BossView;           // optional scene visual (collider + EnemyRef)

        public GamePhase Phase = GamePhase.Menu;
        public bool Paused;
        public float Elapsed, LastDps, TotalDamage;
        public int BossPhase = 1;

        public Boss Boss { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public BossAttackSystem Attacks { get; private set; }
        public ShotSystem Shots { get; private set; }

        // Stable hooks for HUD / FX: the systems above are recreated on every Restart.
        public event System.Action<PatternDefinition> PatternStarted;
        public event System.Action<int, int> PatternResolved; // units hit, units lost
        public event System.Action<string> FormationChanged;
        public event System.Action PhaseTwoStarted;

        readonly List<(float t, float d)> _hits = new();

        public int AliveCount
        {
            get { int n = 0; foreach (var u in Squad.Units) if (!u.Dead) n++; return n; }
        }

        public void Restart()
        {
            Boss = new Boss();
            Enemies = new EnemyRegistry();
            Enemies.Register(Boss);
            Attacks = new BossAttackSystem { Sequence = Sequence };
            Attacks.OnStart += p => PatternStarted?.Invoke(p);
            Attacks.OnResolved += (hit, lost) => PatternResolved?.Invoke(hit, lost);
            Shots = new ShotSystem();
            Shots.OnHit += d => { _hits.Add((Elapsed, d)); TotalDamage += d; };

            _hits.Clear();
            TotalDamage = 0; Elapsed = 0; LastDps = 0; BossPhase = 1; Paused = false;
            Squad.Init();
            if (BossView != null) BossView.Bind(Boss);
            Phase = GamePhase.Play;
            FormationChanged?.Invoke(Squad.Formation);
        }

        public void TogglePause() { if (Phase == GamePhase.Play) Paused = !Paused; }

        public void ChangeFormation(string id)
        {
            if (Phase != GamePhase.Play || Paused) return;
            if (Squad.SetFormation(id)) FormationChanged?.Invoke(id);
        }

        /// <summary>
        /// Move / AttackMove walk the anchor to the point (units do not fire while moving, see FireRules).
        /// AttackTarget advances only when the target is out of range; in range the squad holds and fires.
        /// </summary>
        public void IssueOrder(SquadOrder o)
        {
            if (Phase != GamePhase.Play || Paused) return;
            Squad.Order = o;
            if (o.Kind == OrderKind.AttackTarget && o.Target != null)
            {
                bool inRange = (o.Target.Pos - Squad.Anchor).magnitude < GameConstants.FireRange;
                Squad.SetDestination(inRange ? Squad.Anchor : o.Target.Pos);
            }
            else Squad.SetDestination(o.Point);
        }

        void Update() => Step(Mathf.Min(MaxDt, Time.deltaTime));

        /// <summary>Advance the match by dt. Public so tests can drive time.</summary>
        public void Step(float dt)
        {
            if (Phase != GamePhase.Play || Paused) return;
            Elapsed += dt;
            Squad.Tick(dt);
            Shots.Tick(Squad.Units, Enemies, Squad.Order, dt);
            Attacks.Tick(Squad, Boss, dt);

            _hits.RemoveAll(e => Elapsed - e.t >= GameConstants.DpsWindow);
            LastDps = CombatMath.Dps(_hits, Elapsed);
            Boss.Flash = Mathf.Max(0, Boss.Flash - dt * 3f);

            if (BossPhase == 1 && CombatMath.IsPhase2(Boss.HpFrac))
            {
                BossPhase = 2; Boss.Phase = 2;
                PhaseTwoStarted?.Invoke();
            }

            if (AliveCount == 0) Phase = GamePhase.Lose;
            else if (!Boss.Alive) Phase = GamePhase.Win;
        }
    }
}
