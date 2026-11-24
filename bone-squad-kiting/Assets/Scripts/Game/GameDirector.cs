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

        [Min(0f)] public float NearMissMargin = 1f;                               // how close outside a zone counts as a near miss (0 = off)
        [Min(1f)] public float NearMissDamageMultiplier = 1.2f;                   // squad damage while the near-miss buff lasts
        [Min(0f)] public float NearMissBuffSeconds = 5f;                          // buff length; a new near miss starts it over
        /// <summary>Seconds of the near-miss damage buff left; 0 = no buff.</summary>
        public float NearMissBuffLeft { get; private set; }
        /// <summary>What the squad's shots are multiplied by right now.</summary>
        public float DamageMultiplier => NearMissBuffLeft > 0f ? NearMissDamageMultiplier : 1f;

        [Min(0.1f)] public float ReviveSeconds = 3f;                              // how long one revive takes

        [Header("State")]
        public GamePhase Phase = GamePhase.Menu;
        public bool Paused { get; private set; }
        float _timeScaleBeforePause;
        bool _audioPausedBeforePause;
        public float Elapsed, LastDps, TotalDamage;
        public int BossPhase = 1;

        public Boss Boss { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public BossAttackSystem Attacks { get; private set; }
        public ShotSystem Shots { get; private set; }
        public MobSystem Mobs { get; private set; }
        public EliteSystem Elites { get; private set; }
        /// <summary>The Frost Knight fight, on a stage that has a BossArena; null on every other stage.</summary>
        public FrostKnightFight Fight { get; private set; }

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
        Vector2? _lastPursuitPosition;
        float _bossBodyHeight;
        Transform _bossTorso;
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
        public event System.Action DefaultRingRequested;
        public event System.Action<string> FormationChanged;
        public event System.Action<float> FormationRotated; // new rotation in degrees, clockwise
        public event System.Action PhaseTwoStarted;
        /// <summary>A zone just missed the squad (units that stood right outside it): the damage buff starts.</summary>
        public event System.Action<int> NearMissed;
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
            SetPaused(false);
            Boss = HasBoss ? new Boss() : null;
            Enemies = new EnemyRegistry();
            if (Boss != null) Enemies.Register(Boss);
            Mobs = new MobSystem();
            Mobs.OnMobDied += _ => SoundManager.Current?.PlayVolleySfx("enemy.mob.death");
            Mobs.OnProjectileHit += (shot, _) => { if (shot.Def != null && shot.Def.JavelinThrowClip == null) SoundManager.Current?.PlayVolleySfx("enemy.caster.hit"); };
            Elites = new EliteSystem();
            Elites.OnPatternStarted += (e, p) => PatternStarted?.Invoke(p);
            Elites.OnPatternResolved += (e, hit, lost) =>
            {
                if (lost > 0) Squad.Reform();
                if (e.Attacks.OnImpact == null) DefaultRingRequested?.Invoke();
                PatternResolved?.Invoke(hit, lost);
            };
            Elites.OnNearMiss += (e, near) => OnNearMiss(near);
            Elites.OnDied += OnMiniBossDied;
            Elites.OnArrived += OnMiniBossArrived;
            // Mini boss markers placed in this director's own scene.
            var found = new List<MiniBossSpawn>();
            foreach (var m in FindObjectsByType<MiniBossSpawn>(FindObjectsSortMode.None))
                if (m.gameObject.scene == gameObject.scene) found.Add(m);
            _miniBosses = found.ToArray();
            foreach (var m in _miniBosses) m.ResetSpawn();
            _cutsceneRemaining = 0f;
            _lastPursuitPosition = null;
            _aliveLastStep = GameConstants.SquadSize;
            Telegraphs.Clear();
            Revives = new ReviveSystem();
            Revives.OnStarted += (a, b) =>
            {
                SoundManager.Current?.PlaySfx("squad.revive.magic");
                ReviveStarted?.Invoke(a, b);
            };
            Revives.OnAssemblyStarted += () => SoundManager.Current?.PlayVolleySfx("squad.revive");
            Revives.OnCompleted += (a, b) => ReviveCompleted?.Invoke(a, b);
            Revives.OnCancelled += (a, b) => ReviveCancelled?.Invoke(a, b);
            Flow = Squad.Grid != null ? new FlowField(Squad.Grid) : null;

            Attacks = new BossAttackSystem { Sequence = Sequence };
            Attacks.OnStart += p => PatternStarted?.Invoke(p);
            Attacks.OnNearMiss += OnNearMiss;
            Attacks.OnResolved += (hit, lost) =>
            {
                DefaultRingRequested?.Invoke();
                if (lost > 0) Squad.Reform(); // close the formation up around the survivors
                PatternResolved?.Invoke(hit, lost);
            };
            Shots = new ShotSystem();
            Shots.OnLaunched += shot =>
            {
                SoundManager.Current?.PlayVolleySfx(shot.Heavy ? "squad.leader.throw" : "squad.crossbow.fire");
            };
            Shots.OnTargetHit += shot =>
            {
                if (shot.Heavy) { SoundManager.Current?.PlayVolleySfx("squad.leader.hit"); return; }
                bool metal;
                if (shot.Target is Mob mob) metal = mob.Def.Armored;
                else if (shot.Target is Elite elite) metal = elite.Def.Armored;
                else return;
                SoundManager.Current?.PlayVolleySfx(metal ? "squad.arrow.hit.armor" : "squad.arrow.hit.humanoid");
            };
            Shots.OnHit += d => { _hits.Add((Elapsed, d)); TotalDamage += d; };

            _hits.Clear();
            TotalDamage = 0; Elapsed = 0; LastDps = 0; BossPhase = 1;
            NearMissBuffLeft = 0f;
            Squad.Init();
            StartBossFight();
            if (Waves != null)
            {
                Vector2? end = Stage != null && Stage.EndPoint != null
                    ? new Vector2(Stage.EndPoint.position.x, Stage.EndPoint.position.z) : null;
                Waves.Begin(Squad.Grid, Squad.Anchor, end);
            }
            if (BossView != null && Boss != null) BossView.Bind(Boss);
            ApplyTuning();
            Phase = GamePhase.Play;
            PlayStageMusic();
            FormationChanged?.Invoke(Squad.Formation);
        }

        void PlayStageMusic(StageRun.Entry entry = null)
        {
            var sound = SoundManager.Current;
            string id = (entry ?? Run?.CurrentEntry)?.BgmId ?? (HasBoss || Fight != null ? "bgm.boss" : "bgm.stage");
            if (sound != null && !sound.PlayBgm(id))
                sound.StopBgm();
        }

        // A stage with a BossArena is the Frost Knight's: the fight's own rules replace the pattern sequence,
        // and the stage is cleared when the boss is dead, whatever its clear rule says.
        void StartBossFight()
        {
            if (Fight != null) Fight.OnState -= OnBossCameraState;
            if (Stage != null && Stage.Cam != null) Stage.Cam.CancelBossDeathFocus();
            Fight = null;
            BossArena arena = null;
            foreach (var a in FindObjectsByType<BossArena>(FindObjectsSortMode.None))
                if (a.isActiveAndEnabled && a.gameObject.scene == gameObject.scene) { arena = a; break; }
            if (arena == null) return;
            if (Boss == null) Boss = new Boss(); else Enemies.Unregister(Boss);     // the fight decides when it can be hit
            var knight = arena.GetComponentInChildren<FrostKnightView>(true);
            _bossBodyHeight = knight != null && knight.TryGetComponent<CapsuleCollider>(out var body)
                ? body.height * Mathf.Abs(body.transform.lossyScale.y) : 8.4f;
            var animator = knight != null ? knight.GetComponentInChildren<Animator>(true) : null;
            _bossTorso = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (_bossTorso == null && animator != null)
                _bossTorso = System.Array.Find(animator.GetComponentsInChildren<Transform>(true), bone => bone.name == "mixamorig:Spine");
            Fight = new FrostKnightFight(arena, Boss, Squad, Enemies, Elites);
            Fight.OnState += OnBossCameraState;
            Fight.OnPatternStarted += p => PatternStarted?.Invoke(p);
            Fight.OnNearMiss += OnNearMiss;
            Fight.OnHit += (attack, cast, hit, lost) =>
            {
                if (lost > 0) Squad.Reform();
                PatternResolved?.Invoke(hit, lost);
            };
        }

        void OnBossCameraState(FrostKnightState state)
        {
            var cam = Stage != null ? Stage.Cam : null;
            if (cam == null || !cam.isActiveAndEnabled) return;
            if (state == FrostKnightState.Dying)
            {
                Vector2 feet = Fight.Boss.Position;
                cam.BeginBossDeathFocus(feet, GroundHeight.Y(feet.x, feet.y) + .5f * _bossBodyHeight, _bossBodyHeight, _bossTorso);
            }
            if (state == FrostKnightState.Dead) cam.EndBossDeathFocus();
        }

        bool ReturningFromBossDeath => Fight != null && Fight.Won && Stage != null &&
            Stage.Cam != null && Stage.Cam.BossDeathFocusActive;

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
            if (IsFinalStage) { if (Run != null || HasBoss) GameProgress.Clear(); }   // the last stage is cleared: the game is finished
            else if (Run != null) GameProgress.Save(Run.Current + 1, Run.IsLast ? Run.NextScene : "");
        }

        // A zone went off right next to the squad without catching anyone: the squad hits harder for a while.
        void OnNearMiss(int unitsNear)
        {
            if (NearMissBuffSeconds <= 0f) return;
            NearMissBuffLeft = NearMissBuffSeconds;      // not stacked: a new near miss starts the time over
            NearMissed?.Invoke(unitsNear);
        }

        public void TogglePause()
        {
            if (Phase == GamePhase.Play && !Frozen) SetPaused(!Paused);
        }

        void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            if (paused)
            {
                _timeScaleBeforePause = Time.timeScale;
                _audioPausedBeforePause = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                Time.timeScale = _timeScaleBeforePause;
                AudioListener.pause = _audioPausedBeforePause;
            }
        }

        void OnDisable() => SetPaused(false);

        public void ChangeFormation(string id)
        {
            if (Phase != GamePhase.Play || Paused || InCutscene) return;
            if (Squad.SetFormation(id))
            {
                SoundManager.Current?.PlaySfx("squad.formation.command");
                FormationChanged?.Invoke(id);
            }
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
            _lastPursuitPosition = null;
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
            _lastPursuitPosition = null;
            // Only a plain move turns a ring to put its leader in front. An attack order leaves the ring as it
            // stands (and stops a turn in progress): turning units cannot fire, and the point of the order is to.
            if (o.Kind == OrderKind.Move) Squad.FaceToward(o.Point);
            else Squad.HoldFacing();
            if (o.Kind == OrderKind.AttackTarget)
            {
                FollowAttackTarget(o.Target);
                if (Squad.Order.Kind == OrderKind.AttackTarget)
                    Shots.FocusPendingShots(Squad.Units, o.Target, UnitFireRange);
            }
            else Squad.SetDestination(o.Point);
            return true;
        }

        void FollowAttackTarget(IEnemy target)
        {
            if (!Enemies.CanTarget(target)) { StopSquad(); return; }
            var point = target.Pos;
            if ((point - Squad.Anchor).sqrMagnitude <= SquadEngageRange * SquadEngageRange)
            {
                Squad.Halt();
                _lastPursuitPosition = null;
                return;
            }

            // SetDestination rebuilds the navigation field. Refresh it when the target moves a cell.
            float cellSize = Squad.Grid?.CellSize ?? 0.5f;
            if (_lastPursuitPosition is Vector2 last && (point - last).sqrMagnitude < cellSize * cellSize) return;
            Squad.SetDestination(point);
            _lastPursuitPosition = point;
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
            NearMissMargin = Tuning.NearMissMargin; NearMissDamageMultiplier = Tuning.NearMissDamageMultiplier; NearMissBuffSeconds = Tuning.NearMissBuffSeconds;
            if (Squad != null)
            {
                Squad.RotationStepDegrees = Tuning.RotationStepDegrees;
                Squad.RotationSpeed = Tuning.RotationSpeed;
                Squad.SeatSize = Tuning.SeatSize;
                Squad.AnchorSpeed = Tuning.AnchorSpeed; Squad.AnchorArrive = Tuning.AnchorArrive;
                Squad.UnitBaseSpeed = Tuning.UnitBaseSpeed; Squad.UnitSpeedDistGain = Tuning.UnitSpeedDistGain;
                Squad.UnitSpeedDistCap = Tuning.UnitSpeedDistCap; Squad.UnitArrive = Tuning.UnitArrive;
            }
            if (Shots != null)
            {
                Shots.ThreatRadius = Tuning.ThreatRadius; Shots.AttackMoveRadius = Tuning.AttackMoveRadius;
                Shots.UnitDamage = Tuning.UnitDamage; Shots.UnitInterval = Tuning.UnitAttackInterval; Shots.UnitIntervalRandom = Tuning.UnitAttackIntervalRandom;
                Shots.LeaderDamage = Tuning.LeaderDamage; Shots.LeaderInterval = Tuning.LeaderAttackInterval; Shots.LeaderIntervalRandom = Tuning.LeaderAttackIntervalRandom;
                Shots.StationaryRampEnabled = Tuning.StationaryRampEnabled;
                Shots.SpeedGainPerHit = Tuning.SpeedGainPerHit;
                Shots.MaxAttackSpeedMultiplier = Tuning.MaxAttackSpeedMultiplier;
                Shots.MovingSpeedDecayPerSecond = Tuning.MovingSpeedDecayPerSecond;
                Shots.AttackStreakGraceSeconds = Tuning.AttackStreakGraceSeconds;
                Shots.HitCounterHoldSeconds = Tuning.HitCounterHoldSeconds;
            }
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
            else if (Run != null) PlayStageMusic();
        }

        /// <summary>The exit is open: clear the remaining wave and retire encounters not reached in this run.</summary>
        public void EndStageReinforcements()
        {
            if (Waves != null) Waves.Stopped = true;
            Mobs?.Clear(Enemies);
            foreach (var marker in _miniBosses)
                if (!marker.Triggered) marker.MarkTriggered(null);
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
            // One that never moves (a cannon) stands exactly on its marker, walkable ground or not: it has
            // nowhere to walk, so it cannot get stuck. One that moves is put on the nearest walkable point.
            var pos = Squad.Grid != null && !m.Definition.Fixed ? Squad.Grid.NearestWalkable(m.Position) : m.Position;
            var route = m.PresentFromStart ? null : m.RoutePoints();
            if (route != null) route[route.Count - 1] = pos;
            var elite = Elites.Spawn(m.Definition, pos, Enemies, route, m.EntrySpeed);
            if (m.PatternCycle != null && m.PatternCycle.Length > 0) elite.Attacks.Patterns = m.PatternCycle;
            if (m.OpeningDelay >= 0f) elite.Attacks.NextAttack = m.OpeningDelay;
            if (m.PresentFromStart) elite.HealthBarApproachRadius = Mathf.Max(0f, m.TriggerRadius);
            m.MarkTriggered(elite);
            MiniBossAppeared?.Invoke(elite);
            if (!m.PresentFromStart) SoundManager.Current?.PlaySfx("enemy.miniboss.appear");
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
            foreach (var marker in _miniBosses)
                if (marker.Elite == elite && marker.DefeatGuide != null) marker.DefeatGuide.SetActive(true);
            MiniBossDefeated?.Invoke(elite);
            SoundManager.Current?.PlaySfx("enemy.miniboss.death");
            if (Fight != null) return;                                // the boss fight's mages: the fight goes on
            foreach (var m in _miniBosses) if (!m.Defeated) return;   // others remain (including ones yet to appear)
            // A guard can fall before its gate. Keep the siege going until the gate itself breaks.
            foreach (var target in Enemies.Enemies)
                if (target is BreakableWall { EndsReinforcements: true, Alive: true }) return;
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
            if (Fight != null) return Fight.Won && !ReturningFromBossDeath;
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
            if (Phase != GamePhase.Play || Paused || Frozen || ReturningFromBossDeath) return;
            ApplyTuning();
            if (InCutscene)
            {
                _cutsceneRemaining -= dt;   // nothing else advances: nobody gets hit while the camera is away
                return;
            }
            Elapsed += dt;

            var order = Squad.Order;
            if (order.Kind == OrderKind.AttackTarget) FollowAttackTarget(order.Target);
            else if (order.Kind == OrderKind.AttackMove && Squad.IsMoving &&
                     Enemies.NearestInRange(Squad.Anchor, SquadEngageRange) != null) Squad.Halt();

            Squad.Tick(dt);

            if (Waves != null)
            {
                int spawned = Waves.Tick(Squad, Mobs, Enemies, dt);
                if (spawned > 0)
                {
                    WaveStarted?.Invoke(Waves.WavesSpawned, spawned);
                    SoundManager.Current?.PlaySfx("stage.wave.start");
                }
            }
            Mobs.Tick(Squad, Enemies, Squad.Grid, Flow, dt);
            if (Mobs.UnitsKilledLastTick > 0) Squad.Reform();   // close up around the survivors

            Revives.Duration = ReviveSeconds;
            Revives.Tick(Squad.Anchor, dt);
            if (Revives.CompletedLastTick > 0) Squad.Reform();  // make room: the revived walk to free slots

            Shots.FireRange = UnitFireRange;
            NearMissBuffLeft = Mathf.Max(0f, NearMissBuffLeft - dt);
            Shots.DamageMultiplier = DamageMultiplier;
            BossAttackSystem.NearMissMargin = NearMissMargin;
            Shots.Tick(Squad.Units, Enemies, Squad.Order, dt);
            if (Fight != null) Fight.Tick(dt);
            else if (Boss != null) Attacks.Tick(Squad, Boss, dt);

            // Deaths are processed before new arrivals, so a mini boss that waits for another one to die
            // appears on the same frame, and a newcomer does not act on the frame it appears.
            Elites.Tick(Squad, Enemies, Squad.Grid, Flow, dt);
            if (InCutscene) return;   // the last mini boss just died: its moment starts now
            TriggerMiniBosses();
            if (InCutscene) return;   // one just appeared with an intro: freeze from this frame on

            Telegraphs.Clear();
            if (Fight != null) Telegraphs.AddRange(Fight.Telegraphs);
            else if (Attacks.Current.HasValue) Telegraphs.Add(Attacks.Current.Value);
            foreach (var e in Elites.Elites) if (e.Attacks.Current.HasValue) Telegraphs.Add(e.Attacks.Current.Value);

            _hits.RemoveAll(e => Elapsed - e.t >= GameConstants.DpsWindow);
            LastDps = CombatMath.Dps(_hits, Elapsed);
            if (Boss != null)
            {
                Boss.Flash = Mathf.Max(0, Boss.Flash - dt * 3f);
                if (BossPhase == 1 && (Fight != null ? Fight.Phase == 2 : CombatMath.IsPhase2(Boss.HpFrac)))
                {
                    BossPhase = 2; Boss.Phase = 2;
                    PhaseTwoStarted?.Invoke();
                }
            }

            // Whatever just killed units (a burst, a sustained tick, a mob), close the formation up.
            int aliveNow = AliveCount;
            if (aliveNow < _aliveLastStep) Squad.Reform();
            _aliveLastStep = aliveNow;

            if (AliveCount == 0)
            {
                Phase = GamePhase.Lose;
                SoundManager.Current?.StopBgm();
                SoundManager.Current?.PlaySfx("squad.defeat");
            }
            else if (StageCleared())
            {
                Phase = GamePhase.Win;
                SaveProgress();
                if (Run != null && !Run.IsLast) PlayStageMusic(Run.Stages[Run.Current + 1]);
                else if (IsFinalStage) SoundManager.Current?.PlaySfx("stage.clear");
            }
        }
    }
}
