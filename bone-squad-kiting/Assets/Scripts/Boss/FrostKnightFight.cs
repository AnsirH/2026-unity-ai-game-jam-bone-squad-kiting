using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Bosses
{
    public enum FrostKnightState
    {
        Waiting,    // on the throne, head bowed, until the squad walks in
        Throne,     // phase 1: slams from the throne
        Stunned,    // between the phases: out cold on the throne while the mages fight
        Rising,     // stands up and fades out of sight
        Hidden,     // phase 2: out of sight between attacks
        Charging,   // phase 2: the charged slash across the room
        Slamming,   // phase 2: the lethal slam
        Groggy,     // phase 2: on the floor, open to attack
        Vanishing,  // fading out of sight after a slam or a groggy moment
        Dying,
        Dead,
    }

    public enum FrostKnightAttack { Parallel, Circle, Charge, LethalLeft, LethalRight, LethalCenter }

    /// <summary>
    /// The Frost Knight fight, as rules only (no objects, no animation): which state the boss is in, where it
    /// stands, which zone is being warned about, and who gets hurt when it goes off. Ticked by GameDirector.
    /// FrostKnightView shows it.
    /// </summary>
    public class FrostKnightFight
    {
        // Lengths of the model's motions at speed 1, and when the sword lands in them.
        public const float ChargeHitAt = 7f, ChargeLength = 7.67f, PlantHitAt = 0.25f, DieLength = 9.58f;
        const float PlantLead = 0.3f;          // the boss shows up this long before the plant motion starts

        public readonly Boss Boss;
        public readonly FrostKnightTuning Tuning;
        public string BossName = "서리 기사";

        public FrostKnightState State { get; private set; } = FrostKnightState.Waiting;
        public float StateTime { get; private set; }
        /// <summary>1 on the throne (and while the mages fight), 2 once it has stood up.</summary>
        public int Phase => State <= FrostKnightState.Stunned ? 1 : 2;
        /// <summary>The squad's shots can reach the boss right now.</summary>
        public bool Targetable { get; private set; }
        public bool Won => State == FrostKnightState.Dead;
        public bool Casting => _casting;
        public FrostKnightAttack Attack { get; private set; }
        /// <summary>How many groggy moments there have been.</summary>
        public int GroggyCount { get; private set; }

        /// <summary>The motion the model should be playing, and a number that goes up every time it is (re)started.</summary>
        public string Anim { get; private set; } = "HeadBowed";
        public int AnimSerial { get; private set; }
        public float AnimSpeed { get; private set; } = 1f;
        /// <summary>Start the motion at its very end (used to fade out: the stand-up motion's end pose dissolves).</summary>
        public bool AnimAtEnd { get; private set; }

        /// <summary>The zone being warned about, for the telegraph view and the HUD (none or one).</summary>
        public readonly List<BossAttackSystem.Active> Telegraphs = new();
        public readonly List<Elite> Mages = new();

        public System.Action<FrostKnightState> OnState;
        public System.Action<FrostKnightAttack, BossAttackSystem.Active> OnCast;     // a warning starts
        public System.Action<FrostKnightAttack, BossAttackSystem.Active, int, int> OnHit; // it goes off: units hit, units lost
        public System.Action<int> OnNearMiss;
        public System.Action<PatternDefinition> OnPatternStarted;

        // The room.
        readonly Vector2 _center, _right, _forward, _size, _seat;
        readonly float _yaw, _seatFacing;
        readonly Vector2[] _mageSpawns;

        readonly FormationController _squad;
        readonly EnemyRegistry _enemies;
        readonly EliteSystem _elites;
        readonly System.Func<float> _random;

        BossAttackSystem.Active _cast;
        bool _casting, _lethal, _magesSpawned, _plantStarted;
        float _wait, _afterHit;
        int _step, _lastLethal = -1;
        readonly PatternDefinition[] _patterns = new PatternDefinition[6];

        public FrostKnightFight(BossArena arena, Boss boss, FormationController squad, EnemyRegistry enemies, EliteSystem elites,
            System.Func<float> random = null)
        {
            Tuning = arena.Tuning; Boss = boss; BossName = arena.BossName;
            _squad = squad; _enemies = enemies; _elites = elites;
            _random = random ?? (() => Random.value);
            _center = arena.Center; _right = arena.Right; _forward = arena.Forward; _size = arena.Size; _yaw = arena.Yaw;
            var seat = arena.Seat != null ? arena.Seat : arena.transform;
            _seat = new Vector2(seat.position.x, seat.position.z);
            _seatFacing = arena.Seat != null ? seat.eulerAngles.y : _yaw + 180f;
            _mageSpawns = new Vector2[arena.MageSpawns != null ? arena.MageSpawns.Length : 0];
            for (int i = 0; i < _mageSpawns.Length; i++)
                _mageSpawns[i] = arena.MageSpawns[i] != null ? new Vector2(arena.MageSpawns[i].position.x, arena.MageSpawns[i].position.z) : _center;

            Boss.MaxHp = Tuning.MaxHp; Boss.Hp = Tuning.MaxHp;
            Boss.HpFloor = Tuning.MaxHp * Tuning.PhaseOneEndsAt;
            Boss.Position = _seat; Boss.Facing = _seatFacing;
            _patterns[(int)FrostKnightAttack.Parallel] = Pattern("평행 참격", "두 줄 사이나 바깥으로", "두 줄을 따라 벱니다. 줄을 피하세요!", "line", false);
            _patterns[(int)FrostKnightAttack.Circle] = Pattern("대검 낙하", "원 밖으로", "대검이 떨어집니다. 원 밖으로 나가세요!", "", false);
            _patterns[(int)FrostKnightAttack.Charge] = Pattern("가로 차징", "두 줄을 피해서", "방을 가로질러 두 줄을 벱니다!", "line", false);
            _patterns[(int)FrostKnightAttack.LethalLeft] = Pattern("즉사 내려찍기", "오른쪽으로", "왼쪽 전체가 즉사 범위입니다!", "", true);
            _patterns[(int)FrostKnightAttack.LethalRight] = Pattern("즉사 내려찍기", "왼쪽으로", "오른쪽 전체가 즉사 범위입니다!", "", true);
            _patterns[(int)FrostKnightAttack.LethalCenter] = Pattern("즉사 내려찍기", "양옆으로", "가운데가 즉사 범위입니다!", "", true);
        }

        static PatternDefinition Pattern(string name, string advice, string detail, string recommend, bool lethal)
        {
            var p = ScriptableObject.CreateInstance<PatternDefinition>();
            p.name = name; p.DisplayName = name; p.Advice = advice; p.Detail = detail; p.Recommend = recommend; p.LethalTelegraph = lethal;
            p.hideFlags = HideFlags.HideAndDontSave;
            return p;
        }

        Vector2 Point(float x, float z) => _center + _right * x + _forward * z;
        bool InRoom(Vector2 p) => Mathf.Abs(Vector2.Dot(p - _center, _right)) <= _size.x * 0.5f && Mathf.Abs(Vector2.Dot(p - _center, _forward)) <= _size.y * 0.5f;

        public void Tick(float dt)
        {
            StateTime += dt;
            if (State >= FrostKnightState.Hidden && State < FrostKnightState.Dying && !Boss.Alive) { Die(); return; }
            TickCast(dt);

            switch (State)
            {
                case FrostKnightState.Waiting:
                    if (InRoom(_squad.Anchor))
                    {
                        Go(FrostKnightState.Throne); Play("ThroneIdle");
                        SetTargetable(true);
                        _wait = Tuning.FirstAttackDelay;
                    }
                    break;

                case FrostKnightState.Throne:
                    if (_casting) break;
                    if (Boss.Hp <= Boss.HpFloor + 0.5f) { Stun(); break; }
                    _wait -= dt;
                    if (_wait <= 0f) StartThroneAttack();
                    break;

                case FrostKnightState.Stunned:
                    if (!_magesSpawned)
                    {
                        if (StateTime >= Tuning.MageDelay) SpawnMages();
                        break;
                    }
                    Mages.RemoveAll(m => !m.Alive);
                    if (Mages.Count == 0) { Go(FrostKnightState.Rising); Play("StandUp"); }
                    break;

                case FrostKnightState.Rising:
                    if (StateTime >= Tuning.StandUpSeconds) Hide();
                    break;

                case FrostKnightState.Hidden:
                    _wait -= dt;
                    if (_wait <= 0f) NextPhaseTwoMove();
                    break;

                case FrostKnightState.Charging:
                    if (!_casting && StateTime >= ChargeLength / Mathf.Max(0.1f, Tuning.ChargeAnimSpeed)) Hide();
                    break;

                case FrostKnightState.Slamming:
                    if (_casting)
                    {
                        if (!_plantStarted && _cast.Remaining <= PlantHitAt + PlantLead) { _plantStarted = true; Play("SwordPlant"); }
                    }
                    else
                    {
                        _afterHit += dt;
                        if (_afterHit >= Tuning.LethalHoldSeconds) Vanish();
                    }
                    break;

                case FrostKnightState.Groggy:
                    if (StateTime >= Tuning.GroggySeconds) Vanish();
                    break;

                case FrostKnightState.Vanishing:
                    if (StateTime >= Tuning.VanishSeconds) Hide();
                    break;

                case FrostKnightState.Dying:
                    if (StateTime >= DieLength / Mathf.Max(0.1f, Tuning.DieAnimSpeed)) Go(FrostKnightState.Dead);
                    break;
            }
        }

        // ---- phase 1 ----

        void StartThroneAttack()
        {
            Play("RaiseHands");            // the model goes on to the slam and back to idle by itself
            if (_random() < 0.5f)
            {
                float angle = _yaw + Mathf.Lerp(-Tuning.ParallelMaxAngle, Tuning.ParallelMaxAngle, _random());
                Cast(FrostKnightAttack.Parallel, DangerShape.BandsX(Tuning.ParallelInner, Tuning.ParallelOuter, Tuning.ParallelHalfLength),
                    _center, angle, Tuning.SlamHitSeconds, false);
            }
            else Cast(FrostKnightAttack.Circle, DangerShape.Disc(Tuning.CircleRadius), _squad.Anchor, 0f, Tuning.SlamHitSeconds, false);
            _wait = Tuning.SlamRecoverSeconds + Tuning.ThroneAttackGap;
        }

        void Stun()
        {
            Go(FrostKnightState.Stunned); Play("HeadBowed");
            SetTargetable(false);
            _magesSpawned = false;
        }

        void SpawnMages()
        {
            _magesSpawned = true;
            if (Tuning.Mages == null || _elites == null) return;
            for (int i = 0; i < Tuning.Mages.Length; i++)
            {
                if (Tuning.Mages[i] == null) continue;
                // Without placed points they stand left and right of the room's middle.
                var at = i < _mageSpawns.Length ? _mageSpawns[i] : Point((i % 2 == 0 ? -1f : 1f) * _size.x * 0.25f, _size.y * 0.15f);
                Mages.Add(_elites.Spawn(Tuning.Mages[i], at, _enemies));
            }
        }

        // ---- phase 2 ----

        void Hide()
        {
            Go(FrostKnightState.Hidden);
            SetTargetable(false);
            Boss.HpFloor = 0f;
            _wait = Tuning.AttackGap;
        }

        void Vanish()
        {
            Go(FrostKnightState.Vanishing);
            SetTargetable(false);
            Play("StandUp", 1f, atEnd: true);
        }

        void NextPhaseTwoMove()
        {
            string order = string.IsNullOrEmpty(Tuning.AttackOrder) ? "C" : Tuning.AttackOrder;
            if (_step >= order.Length) { _step = 0; StartGroggy(); return; }
            char move = char.ToUpperInvariant(order[_step++]);
            if (move == 'L') StartLethal(); else StartCharge();
        }

        void StartCharge()
        {
            Go(FrostKnightState.Charging);
            bool fromLeft = _random() < 0.5f;
            float shift = Mathf.Lerp(-Tuning.ChargeShift, Tuning.ChargeShift, _random());
            float side = fromLeft ? -1f : 1f;
            Boss.Position = Point(side * (_size.x * 0.5f - 1f), shift);
            Boss.Facing = _yaw + (fromLeft ? 90f : -90f);                       // looks across the room
            float speed = Mathf.Max(0.1f, Tuning.ChargeAnimSpeed);
            Play("ChargedSlash", speed);
            // Two rows across the whole room: the two bands, turned a quarter.
            float half = Tuning.ChargeRowWidth * 0.5f;
            var shape = DangerShape.BandsX(Mathf.Max(-0.001f, Tuning.ChargeRowOffset - half), Tuning.ChargeRowOffset + half, _size.x * 0.5f);
            Cast(FrostKnightAttack.Charge, shape, Point(0f, shift), _yaw + 90f, ChargeHitAt / speed, false);
        }

        void StartLethal()
        {
            Go(FrostKnightState.Slamming);
            int zone = Mathf.Min(2, (int)(_random() * 3f));     // 0 left, 1 right, 2 middle; not the same one twice running
            if (zone == _lastLethal) zone = (zone + 1) % 3;
            _lastLethal = zone;
            float halfRoom = _size.x * 0.5f;
            float halfWidth = zone == 2 ? halfRoom * Tuning.LethalCenterShare : halfRoom * 0.5f;
            float x = zone == 0 ? -halfRoom * 0.5f : zone == 1 ? halfRoom * 0.5f : 0f;
            Boss.Position = Point(x, 0f);
            Boss.Facing = _yaw + 180f;                          // faces the entrance, like on the throne
            _plantStarted = false; _afterHit = 0f;
            var attack = zone == 0 ? FrostKnightAttack.LethalLeft : zone == 1 ? FrostKnightAttack.LethalRight : FrostKnightAttack.LethalCenter;
            // One box: the two bands with no gap between them (a hair of overlap, so the seam is in it too).
            Cast(attack, DangerShape.BandsX(-0.001f, halfWidth, _size.y * 0.5f + 1f), Point(x, 0f), _yaw, Tuning.LethalWarnSeconds, true);
        }

        void StartGroggy()
        {
            Go(FrostKnightState.Groggy);
            GroggyCount++;
            Boss.Position = _center; Boss.Facing = _yaw + 180f;
            Play("Groggy");
            SetTargetable(true);
        }

        /// <summary>End the fight now, from any state: the boss dies. For tests and debugging.</summary>
        public void Kill() { if (State >= FrostKnightState.Dying) return; Boss.HpFloor = 0f; Boss.Hp = 0f; Die(); }

        void Die()
        {
            Go(FrostKnightState.Dying);
            SetTargetable(false);
            _casting = false; Telegraphs.Clear();
            Play("Die", Tuning.DieAnimSpeed);
        }

        // ---- shared ----

        void Go(FrostKnightState s) { State = s; StateTime = 0f; OnState?.Invoke(s); }

        void Play(string anim, float speed = 1f, bool atEnd = false) { Anim = anim; AnimSpeed = speed; AnimAtEnd = atEnd; AnimSerial++; }

        void SetTargetable(bool on)
        {
            if (Targetable == on) return;
            Targetable = on;
            if (on) _enemies?.Register(Boss); else _enemies?.Unregister(Boss);
        }

        void Cast(FrostKnightAttack attack, DangerShape shape, Vector2 at, float angle, float seconds, bool lethal)
        {
            var p = _patterns[(int)attack];
            p.Time = seconds; p.Shape = shape;
            Attack = attack; _lethal = lethal; _casting = true;
            _cast = new BossAttackSystem.Active
            {
                Pattern = p, Shape = shape, ShapeScale = 1f, Cx = at.x, Cz = at.y, Angle = Mathf.Repeat(angle, 360f),
                Elapsed = 0f, Remaining = seconds, Tracks = false,
            };
            Telegraphs.Clear(); Telegraphs.Add(_cast);
            OnPatternStarted?.Invoke(p);
            OnCast?.Invoke(attack, _cast);
        }

        void TickCast(float dt)
        {
            if (!_casting) return;
            _cast.Elapsed += dt;
            _cast.Remaining = Mathf.Max(0f, _cast.Pattern.Time - _cast.Elapsed);
            Telegraphs[0] = _cast;
            if (_cast.Remaining > 0f) return;
            _casting = false; Telegraphs.Clear();
            Resolve();
        }

        void Resolve()
        {
            var units = _squad.Units;
            var caught = new List<SquadUnit>();
            foreach (var u in units)
                if (!u.Dead && DangerShapeEval.InDanger(_cast.Shape, _cast.Cx, _cast.Cz, u.X, u.Z, _cast.Angle)) caught.Add(u);
            if (caught.Count == 0 && BossAttackSystem.NearMissMargin > 0f)
            {
                int near = 0;
                foreach (var u in units)
                    if (!u.Dead && DangerShapeEval.NearDanger(_cast.Shape, _cast.Cx, _cast.Cz, u.X, u.Z, _cast.Angle, BossAttackSystem.NearMissMargin)) near++;
                if (near > 0) OnNearMiss?.Invoke(near);
            }
            int before = 0; foreach (var u in units) if (!u.Dead) before++;
            if (_lethal)
            {
                if (!SquadDamage.Invincible)
                {
                    bool leaderCaught = false;
                    foreach (var u in caught) { if (u.IsLeader) leaderCaught = true; else Kill(u); }
                    if (leaderCaught && Tuning.LethalKillsLeader) foreach (var u in units) Kill(u);   // no squad without its leader
                }
            }
            else foreach (var u in caught) SquadDamage.Apply(units, u, Tuning.HitDamage);
            int after = 0; foreach (var u in units) if (!u.Dead) after++;
            OnHit?.Invoke(Attack, _cast, caught.Count, before - after);
        }

        static void Kill(SquadUnit u) { if (u.Dead) return; u.Hp = 0f; u.Dead = true; u.Flash = 1f; }
    }
}
