using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

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

        [Header("Stage")]
        public bool HasBoss = true;                                   // false for stages fought against rounds of mobs only
        public StageClearRule ClearRule = StageClearRule.DefeatBoss;
        public StageSetup Stage;            // optional: start / end points and the walkable grid
        public RoundSpawner Rounds;         // optional: this stage's rounds of mobs

        [Header("Tuning (editable while playing)")]
        [Min(0f)] public float UnitFireRange = GameConstants.FireRange;            // how far one unit can shoot
        [Min(0f)] public float SquadEngageRange = GameConstants.SquadEngageRange;  // from the anchor: attack orders stop here

        [Header("State")]
        public GamePhase Phase = GamePhase.Menu;
        public bool Paused;
        public float Elapsed, LastDps, TotalDamage;
        public int BossPhase = 1;

        public Boss Boss { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public BossAttackSystem Attacks { get; private set; }
        public ShotSystem Shots { get; private set; }
        public MobSystem Mobs { get; private set; }
        public FlowField Flow { get; private set; }

        /// <summary>True once every round has been started and no mob is left alive (always true with no rounds).</summary>
        public bool EnemiesCleared => (Rounds == null || Rounds.AllStarted) && (Mobs == null || Mobs.AliveCount == 0);

        // Stable hooks for HUD / FX: the systems above are recreated on every Restart.
        public event System.Action<PatternDefinition> PatternStarted;
        public event System.Action<int, int> PatternResolved; // units hit, units lost
        public event System.Action<string> FormationChanged;
        public event System.Action<float> FormationRotated; // new rotation in degrees, clockwise
        public event System.Action PhaseTwoStarted;
        public event System.Action<int, int> RoundStarted;   // round number (1-based), total rounds
        public event System.Action AllEnemiesCleared;         // last round wiped out

        bool _clearedAnnounced;

        readonly List<(float t, float d)> _hits = new();

        public int AliveCount => Squad.AliveCount;

        public void Restart()
        {
            Boss = HasBoss ? new Boss() : null;
            Enemies = new EnemyRegistry();
            if (Boss != null) Enemies.Register(Boss);
            Mobs = new MobSystem();
            Flow = Squad.Grid != null ? new FlowField(Squad.Grid) : null;
            if (Rounds != null) Rounds.ResetRounds();
            _clearedAnnounced = false;
            Attacks = new BossAttackSystem { Sequence = Sequence };
            Attacks.OnStart += p => PatternStarted?.Invoke(p);
            Attacks.OnResolved += (hit, lost) =>
            {
                if (lost > 0) Squad.Reform(); // close the formation up around the survivors
                PatternResolved?.Invoke(hit, lost);
            };
            Shots = new ShotSystem();
            Shots.OnHit += d => { _hits.Add((Elapsed, d)); TotalDamage += d; };

            _hits.Clear();
            TotalDamage = 0; Elapsed = 0; LastDps = 0; BossPhase = 1; Paused = false;
            Squad.Init();
            if (BossView != null && Boss != null) BossView.Bind(Boss);
            Phase = GamePhase.Play;
            FormationChanged?.Invoke(Squad.Formation);
        }

        public void TogglePause() { if (Phase == GamePhase.Play) Paused = !Paused; }

        public void ChangeFormation(string id)
        {
            if (Phase != GamePhase.Play || Paused) return;
            if (Squad.SetFormation(id)) FormationChanged?.Invoke(id);
        }

        /// <summary>Stop where the squad is: the anchor halts and the order becomes a plain hold.</summary>
        public void StopSquad()
        {
            if (Phase != GamePhase.Play || Paused) return;
            Squad.Halt();
            Squad.Order = SquadOrder.Move(Squad.Anchor);
        }

        /// <summary>Turn the formation by mouse-wheel notches. Positive = clockwise seen from above.</summary>
        public void RotateFormation(int clockwiseNotches)
        {
            if (Phase != GamePhase.Play || Paused || clockwiseNotches == 0) return;
            Squad.Rotate(clockwiseNotches);
            FormationRotated?.Invoke(Squad.RotationDegrees);
        }

        /// <summary>
        /// Move / AttackMove walk the anchor to the point (units do not fire while moving, see FireRules).
        /// AttackTarget walks toward the target. Both attack orders stop once their enemy is within squad
        /// range of the anchor (see Step); an AttackTarget already in range never starts walking.
        /// </summary>
        public void IssueOrder(SquadOrder o)
        {
            if (Phase != GamePhase.Play || Paused) return;
            Squad.Order = o;
            if (o.Kind == OrderKind.AttackTarget && o.Target != null)
            {
                bool inRange = (o.Target.Pos - Squad.Anchor).magnitude < SquadEngageRange;
                Squad.SetDestination(inRange ? Squad.Anchor : o.Target.Pos);
            }
            else Squad.SetDestination(o.Point);
        }

        /// <summary>
        /// Squad range is measured from the formation anchor. An attack-move squad stops for any enemy
        /// inside it; an attack-target squad stops for its ordered enemy.
        /// </summary>
        bool EnemyWithinSquadRange(SquadOrder order)
        {
            switch (order.Kind)
            {
                case OrderKind.AttackMove:
                    return Enemies.NearestInRange(Squad.Anchor, SquadEngageRange) != null;
                case OrderKind.AttackTarget:
                    return order.Target is { Alive: true }
                           && (order.Target.Pos - Squad.Anchor).magnitude < SquadEngageRange;
                default: return false;
            }
        }

        void Start()
        {
            // Stand the squad on the field before the match starts, so the menu is not an empty arena.
            if (Squad != null && Squad.Units.Count == 0) Squad.Init();
        }

        bool StageCleared()
        {
            bool reachedEnd = Stage != null && Stage.ReachedEnd;
            switch (ClearRule)
            {
                case StageClearRule.DefeatBoss: return Boss != null && !Boss.Alive;
                case StageClearRule.KillAllThenReachEnd: return EnemiesCleared && reachedEnd;
                case StageClearRule.KillAll: return EnemiesCleared;
                case StageClearRule.ReachEnd: return reachedEnd;
                default: return false;
            }
        }

        void Update() => Step(Mathf.Min(MaxDt, Time.deltaTime));

        /// <summary>Advance the match by dt. Public so tests can drive time.</summary>
        public void Step(float dt)
        {
            if (Phase != GamePhase.Play || Paused) return;
            Elapsed += dt;

            if (Squad.IsMoving && EnemyWithinSquadRange(Squad.Order)) Squad.Halt();

            Squad.Tick(dt);

            if (Rounds != null && Rounds.Tick(Mobs, Enemies, Squad.Grid, Elapsed))
                RoundStarted?.Invoke(Rounds.Started, Rounds.Total);
            Mobs.Tick(Squad, Enemies, Squad.Grid, Flow, dt);
            if (Mobs.UnitsKilledLastTick > 0) Squad.Reform();   // close up around the survivors
            if (!_clearedAnnounced && Rounds != null && Rounds.Total > 0 && EnemiesCleared)
            {
                _clearedAnnounced = true;
                AllEnemiesCleared?.Invoke();
            }

            Shots.FireRange = UnitFireRange;
            Shots.Tick(Squad.Units, Enemies, Squad.Order, dt);
            if (Boss != null) Attacks.Tick(Squad, Boss, dt);

            _hits.RemoveAll(e => Elapsed - e.t >= GameConstants.DpsWindow);
            LastDps = CombatMath.Dps(_hits, Elapsed);
            if (Boss != null)
            {
                Boss.Flash = Mathf.Max(0, Boss.Flash - dt * 3f);
                if (BossPhase == 1 && CombatMath.IsPhase2(Boss.HpFrac))
                {
                    BossPhase = 2; Boss.Phase = 2;
                    PhaseTwoStarted?.Invoke();
                }
            }

            if (AliveCount == 0) Phase = GamePhase.Lose;
            else if (StageCleared()) Phase = GamePhase.Win;
        }
    }
}
