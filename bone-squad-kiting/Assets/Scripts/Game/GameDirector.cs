using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BoneSquadKiting.Audio;
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
        public WaveSpawner Waves;           // optional: this stage's endless mob waves
        public string StageTitle = "";      // shown when the stage starts and on its clear screen, e.g. "스테이지 1"
        public string NextScene = "";       // scene to load after this stage is cleared; empty = this is the last stage

        public bool IsFinalStage => Run != null ? Run.IsLast && string.IsNullOrEmpty(Run.NextScene) : string.IsNullOrEmpty(NextScene);

        /// <summary>Set when this director's stages are run by a StageRun (several stages in one scene).</summary>
        [System.NonSerialized] public StageRun Run;
        /// <summary>While true nothing advances and orders are ignored (a stage switch is under way).</summary>
        [System.NonSerialized] public bool Frozen;

        // Set when moving on to the next stage, so that stage starts playing at once instead of showing the title screen.
        static bool _continueRun;

        [Header("Tuning")]
        [Tooltip("Shared numbers for every stage. When set, it overrides the three fields below and the squad's and mobs' tuning.")]
        public GameTuning Tuning;
        [Min(0f)] public float UnitFireRange = GameConstants.FireRange;            // how far one unit can shoot
        [Min(0f)] public float SquadEngageRange = GameConstants.SquadEngageRange;  // from the anchor: attack orders stop here

        [Min(0.1f)] public float ReviveSeconds = 3f;                              // how long one revive takes

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
        public EliteSystem Elites { get; private set; }

        /// <summary>Every pattern being cast right now: the boss's and each mini boss's.</summary>
        public readonly List<BossAttackSystem.Active> Telegraphs = new();

        /// <summary>The telegraph that will go off first, if any. The HUD warns about this one.</summary>
        public BossAttackSystem.Active? NextTelegraph
        {
            get
            {
                BossAttackSystem.Active? best = null;
                foreach (var t in Telegraphs) if (best == null || t.Remaining < best.Value.Remaining) best = t;
                return best;
            }
        }

        /// <summary>
        /// True while an intro / death moment is playing: the simulation is frozen and input is ignored,
        /// but effects and the camera keep moving (Time.timeScale is untouched).
        /// </summary>
        public bool InCutscene => _cutsceneRemaining > 0f;
        float _cutsceneRemaining;
        int _aliveLastStep;
        MiniBossSpawn[] _miniBosses = new MiniBossSpawn[0];

        /// <summary>Whether the end point currently counts (for rules that open it only after the mini boss).</summary>
        public bool EndOpen
        {
            get
            {
                if (ClearRule != StageClearRule.DefeatMiniBossThenReachEnd) return ClearRule == StageClearRule.ReachEnd;
                foreach (var m in _miniBosses) if (!m.Defeated) return false;
                return true;
            }
        }
        public ReviveSystem Revives { get; private set; }
        public int DeadCount => Squad.Units.Count - Squad.AliveCount;
        public FlowField Flow { get; private set; }

        // Stable hooks for HUD / FX: the systems above are recreated on every Restart.
        public event System.Action<PatternDefinition> PatternStarted;
        public event System.Action<int, int> PatternResolved; // units hit, units lost
        public event System.Action<string> FormationChanged;
        public event System.Action<float> FormationRotated; // new rotation in degrees, clockwise
        public event System.Action PhaseTwoStarted;
        public event System.Action<int, int> WaveStarted;    // wave number (1-based), mobs spawned
        /// <summary>Focus point, seconds, title to show (may be null), effect prefab to spawn (may be null).</summary>
        public event System.Action<Vector2, float, string, GameObject> CutsceneStarted;
        public event System.Action<Elite> MiniBossAppeared;
        public event System.Action<Elite> MiniBossDefeated;
        public event System.Action<SquadUnit, SquadUnit> ReviveStarted;     // reviver, dead unit
        public event System.Action<SquadUnit, SquadUnit> ReviveCompleted;   // reviver, revived unit
        public event System.Action<SquadUnit, SquadUnit> ReviveCancelled;   // reviver (now dead), unit that stays dead

        readonly List<(float t, float d)> _hits = new();

        public int AliveCount => Squad.AliveCount;

        public void Restart()
        {
            Boss = HasBoss ? new Boss() : null;
            Enemies = new EnemyRegistry();
            if (Boss != null) Enemies.Register(Boss);
            Mobs = new MobSystem();
            Elites = new EliteSystem();
            Elites.OnPatternStarted += (e, p) => PatternStarted?.Invoke(p);
            Elites.OnPatternResolved += (e, hit, lost) =>
            {
                if (lost > 0) Squad.Reform();
                PatternResolved?.Invoke(hit, lost);
            };
            Elites.OnDied += OnMiniBossDied;
            Elites.OnArrived += OnMiniBossArrived;
            // Mini boss markers placed in this director's own scene.
            var found = new List<MiniBossSpawn>();
            foreach (var m in FindObjectsByType<MiniBossSpawn>(FindObjectsSortMode.None))
                if (m.gameObject.scene == gameObject.scene) found.Add(m);
            _miniBosses = found.ToArray();
            foreach (var m in _miniBosses) m.ResetSpawn();
            _cutsceneRemaining = 0f;
            _aliveLastStep = GameConstants.SquadSize;
            Telegraphs.Clear();
            Revives = new ReviveSystem();
            Revives.OnStarted += (a, b) => ReviveStarted?.Invoke(a, b);
            Revives.OnCompleted += (a, b) =>
            {
                SoundManager.Current?.PlaySfx("squad.revive");
                ReviveCompleted?.Invoke(a, b);
            };
            Revives.OnCancelled += (a, b) => ReviveCancelled?.Invoke(a, b);
            Flow = Squad.Grid != null ? new FlowField(Squad.Grid) : null;

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
            if (Waves != null)
            {
                Vector2? end = Stage != null && Stage.EndPoint != null
                    ? new Vector2(Stage.EndPoint.position.x, Stage.EndPoint.position.z) : null;
                Waves.Begin(Squad.Grid, Squad.Anchor, end);
            }
            if (BossView != null && Boss != null) BossView.Bind(Boss);
            ApplyTuning();
            Phase = GamePhase.Play;
            var sound = SoundManager.Current;
            if (sound != null && !sound.PlayBgm(HasBoss ? "bgm.boss" : "bgm.stage"))
                sound.StopBgm();
            FormationChanged?.Invoke(Squad.Formation);
        }

        /// <summary>
        /// After a win: go on. Loads the next stage (which starts right away with a fresh squad), or after
        /// the last stage goes back to the first scene's title screen. Does nothing unless the stage is won.
        /// </summary>
        public void Proceed()
        {
            if (Phase != GamePhase.Win) return;
            if (Run != null && !Run.IsLast) { Run.Advance(); return; }   // next map in this same scene
            string next = Run != null ? Run.NextScene : NextScene;
            if (IsFinalStage) { _continueRun = false; SceneManager.LoadScene(0); }
            else { _continueRun = true; SceneManager.LoadScene(next); }
        }

        /// <summary>Load a stage scene so that it starts playing at once, without its title screen.</summary>
        public static void LoadStageScene(string scene)
        {
            _continueRun = true;
            SceneManager.LoadScene(scene);
        }

        // A cleared stage moves "continue" on to the next one; clearing the last stage ends the save.
        void SaveProgress()
        {
            if (Run != null) GameProgress.Save(Run.Current + 1, Run.IsLast ? Run.NextScene : "");
            else if (IsFinalStage && HasBoss) GameProgress.Clear();   // the boss is down: the game is finished
        }

        public void TogglePause() { if (Phase == GamePhase.Play && !InCutscene) Paused = !Paused; }

        public void ChangeFormation(string id)
        {
            if (Phase != GamePhase.Play || Paused || InCutscene) return;
            if (Squad.SetFormation(id)) FormationChanged?.Invoke(id);
        }

        /// <summary>
        /// Revive action (F): one free living unit starts bringing one dead unit back. Returns false when
        /// nobody is dead or nobody is free. Can be used again while earlier revives are still running.
        /// </summary>
        public bool TryRevive()
        {
            if (Phase != GamePhase.Play || Paused || InCutscene) return false;
            return Revives.TryStart(Squad.Units);
        }

        /// <summary>Stop where the squad is: the anchor halts and the order becomes a plain hold.</summary>
        public void StopSquad()
        {
            if (Phase != GamePhase.Play || Paused || InCutscene) return;
            Squad.Halt();
            Squad.Order = SquadOrder.Move(Squad.Anchor);
        }

        /// <summary>Turn the formation by mouse-wheel notches. Positive = clockwise seen from above.</summary>
        public void RotateFormation(int clockwiseNotches)
        {
            if (Phase != GamePhase.Play || Paused || InCutscene || clockwiseNotches == 0) return;
            Squad.Rotate(clockwiseNotches);
            FormationRotated?.Invoke(Squad.RotationDegrees);
        }

        /// <summary>
        /// Move / AttackMove walk the anchor to the point (units do not fire while moving, see FireRules).
        /// AttackTarget walks toward the target. Both attack orders stop once their enemy is within squad
        /// range of the anchor (see Step); an AttackTarget already in range never starts walking.
        /// </summary>
        public bool IssueOrder(SquadOrder o)
        {
            if (Phase != GamePhase.Play || Paused || InCutscene || Frozen) return false;
            Squad.Order = o;
            // Only a plain move turns a ring to put its leader in front. An attack order leaves the ring as it
            // stands (and stops a turn in progress): turning units cannot fire, and the point of the order is to.
            if (o.Kind == OrderKind.Move) Squad.FaceToward(o.Point);
            else Squad.HoldFacing();
            if (o.Kind == OrderKind.AttackTarget && o.Target != null)
            {
                bool inRange = (o.Target.Pos - Squad.Anchor).magnitude < SquadEngageRange;
                Squad.SetDestination(inRange ? Squad.Anchor : o.Target.Pos);
            }
            else Squad.SetDestination(o.Point);
            return true;
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

        /// <summary>
        /// Copy the shared tuning asset's numbers into this stage's systems. Runs every step, so the asset
        /// can be edited while playing. Without an asset the fields on the components themselves apply.
        /// </summary>
        public void ApplyTuning()
        {
            if (Tuning == null) return;
            UnitFireRange = Tuning.UnitFireRange;
            SquadEngageRange = Tuning.SquadEngageRange;
            ReviveSeconds = Tuning.ReviveSeconds;
            if (Squad != null)
            {
                Squad.RotationStepDegrees = Tuning.RotationStepDegrees;
                Squad.RotationSpeed = Tuning.RotationSpeed;
                Squad.SeatSize = Tuning.SeatSize;
                Squad.AnchorSpeed = Tuning.AnchorSpeed; Squad.AnchorArrive = Tuning.AnchorArrive;
                Squad.UnitBaseSpeed = Tuning.UnitBaseSpeed; Squad.UnitSpeedDistGain = Tuning.UnitSpeedDistGain;
                Squad.UnitSpeedDistCap = Tuning.UnitSpeedDistCap; Squad.UnitArrive = Tuning.UnitArrive;
            }
            if (Shots != null) { Shots.ThreatRadius = Tuning.ThreatRadius; Shots.AttackMoveRadius = Tuning.AttackMoveRadius; }
            if (Revives != null) Revives.AppearRadius = Tuning.ReviveAppearRadius;
            if (Mobs != null) { Mobs.SwitchDistance = Tuning.MobSwitchDistance; Mobs.MaxAlive = Tuning.MobMaxAlive; }
        }

        void OnValidate() => ApplyTuning();   // keeps the Scene-view range gizmos in step with the asset

        void Start()
        {
            ApplyTuning();
            // Stand the squad on the field before the match starts, so the menu is not an empty arena.
            if (Squad != null && Squad.Units.Count == 0) Squad.Init();
            if (_continueRun) { _continueRun = false; Restart(); }
        }

        // A mini boss appears, with its intro, when the squad's anchor comes within its trigger radius.
        void TriggerMiniBosses()
        {
            foreach (var m in _miniBosses)
            {
                if (m.Triggered || m.Definition == null || !m.Unlocked) continue;
                if (!m.PresentFromStart)
                {
                    // By progress when asked for (and the stage measures it), otherwise by distance.
                    bool byProgress = m.AtProgress >= 0f && Waves != null;
                    if (byProgress ? Waves.MaxProgress < m.AtProgress : (m.Position - Squad.Anchor).magnitude > m.TriggerRadius) continue;
                }
                SpawnMiniBoss(m);
            }
        }

        /// <summary>Make a marker's mini boss appear now, whatever its conditions. Does nothing if it already has.</summary>
        public Elite SpawnMiniBoss(MiniBossSpawn m)
        {
            if (m == null || m.Triggered || m.Definition == null || Elites == null) return null;
            var pos = Squad.Grid != null ? Squad.Grid.NearestWalkable(m.Position) : m.Position;
            var route = m.PresentFromStart ? null : m.RoutePoints();
            if (route != null) route[route.Count - 1] = pos;
            var elite = Elites.Spawn(m.Definition, pos, Enemies, route, m.EntrySpeed);
            m.MarkTriggered(elite);
            MiniBossAppeared?.Invoke(elite);
            if (!m.PresentFromStart && (m.IntroWhen == MiniBossIntroWhen.OnAppear || !elite.Entering)) PlayMiniBossIntro(m, elite);
            return elite;
        }

        // The intro moment: the game freezes, the camera goes to the mini boss, and the marker's effect plays.
        void PlayMiniBossIntro(MiniBossSpawn m, Elite elite)
        {
            if (!m.PlayIntro) return;
            if (!string.IsNullOrEmpty(m.IntroEffectId)) BoneSquadKiting.Fx.PresentationManager.Current?.Play(m.IntroEffectId);
            StartCutscene(elite.Pos, m.IntroSeconds > 0f ? m.IntroSeconds : m.Definition.IntroDuration, m.Definition.DisplayName, m.Definition.IntroFx);
        }

        void OnMiniBossArrived(Elite elite)
        {
            foreach (var m in _miniBosses)
                if (m.Elite == elite && m.IntroWhen == MiniBossIntroWhen.OnArrive) PlayMiniBossIntro(m, elite);
        }

        // A mini boss fell. Only the stage's last one gets the big finish: its death moment plays, the swarm
        // is swept away and the waves stop, so the walk to the end point is clear. Earlier ones just die and
        // the waves keep coming.
        void OnMiniBossDied(Elite elite)
        {
            MiniBossDefeated?.Invoke(elite);
            foreach (var m in _miniBosses) if (!m.Defeated) return;   // others remain (including ones yet to appear)
            Mobs.Clear(Enemies);
            Shots.Shots.Clear();
            if (Waves != null) Waves.Stopped = true;
            StartCutscene(elite.Pos, elite.Def.DeathDuration, null, elite.Def.DeathFx);
        }

        void StartCutscene(Vector2 focus, float seconds, string title, GameObject fx)
        {
            if (seconds <= 0f) return;
            _cutsceneRemaining = seconds;
            CutsceneStarted?.Invoke(focus, seconds, title, fx);
        }

        bool StageCleared()
        {
            bool reachedEnd = Stage != null && Stage.ReachedEnd;
            switch (ClearRule)
            {
                case StageClearRule.DefeatBoss: return Boss != null && !Boss.Alive;
                case StageClearRule.ReachEnd: return reachedEnd;
                case StageClearRule.DefeatMiniBossThenReachEnd: return EndOpen && reachedEnd;
                default: return false;
            }
        }

        void Update() => Step(Mathf.Min(MaxDt, Time.deltaTime));

        /// <summary>Advance the match by dt. Public so tests can drive time.</summary>
        public void Step(float dt)
        {
            if (Phase != GamePhase.Play || Paused || Frozen) return;
            ApplyTuning();
            if (InCutscene)
            {
                _cutsceneRemaining -= dt;   // nothing else advances: nobody gets hit while the camera is away
                return;
            }
            Elapsed += dt;

            if (Squad.IsMoving && EnemyWithinSquadRange(Squad.Order)) Squad.Halt();

            Squad.Tick(dt);

            if (Waves != null)
            {
                int spawned = Waves.Tick(Squad, Mobs, Enemies, dt);
                if (spawned > 0) WaveStarted?.Invoke(Waves.WavesSpawned, spawned);
            }
            Mobs.Tick(Squad, Enemies, Squad.Grid, Flow, dt);
            if (Mobs.UnitsKilledLastTick > 0) Squad.Reform();   // close up around the survivors

            Revives.Duration = ReviveSeconds;
            Revives.Tick(Squad.Anchor, dt);
            if (Revives.CompletedLastTick > 0) Squad.Reform();  // make room: the revived walk to free slots

            Shots.FireRange = UnitFireRange;
            Shots.Tick(Squad.Units, Enemies, Squad.Order, dt);
            if (Boss != null) Attacks.Tick(Squad, Boss, dt);

            // Deaths are processed before new arrivals, so a mini boss that waits for another one to die
            // appears on the same frame, and a newcomer does not act on the frame it appears.
            Elites.Tick(Squad, Enemies, Squad.Grid, Flow, dt);
            if (InCutscene) return;   // the last mini boss just died: its moment starts now
            TriggerMiniBosses();
            if (InCutscene) return;   // one just appeared with an intro: freeze from this frame on

            Telegraphs.Clear();
            if (Attacks.Current.HasValue) Telegraphs.Add(Attacks.Current.Value);
            foreach (var e in Elites.Elites) if (e.Attacks.Current.HasValue) Telegraphs.Add(e.Attacks.Current.Value);

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

            // Whatever just killed units (a burst, a sustained tick, a mob), close the formation up.
            int aliveNow = AliveCount;
            if (aliveNow < _aliveLastStep) Squad.Reform();
            _aliveLastStep = aliveNow;

            if (AliveCount == 0) Phase = GamePhase.Lose;
            else if (StageCleared()) Phase = GamePhase.Win;
            if (Phase == GamePhase.Win) SaveProgress();
            if (Phase == GamePhase.Win || Phase == GamePhase.Lose)
            {
                SoundManager.Current?.StopBgm();
                SoundManager.Current?.PlaySfx(Phase == GamePhase.Win ? "stage.clear" : "squad.defeat");
            }
        }
    }
}
