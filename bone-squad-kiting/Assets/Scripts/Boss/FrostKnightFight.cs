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
        const float ThroneMotionHitAt = 2.9f;
        const float ChargeWindupScale = .35f;
        const float PlantLead = 0.8f;          // extra anticipation before the clip's normal ground contact
        const float PlantRaisedPose = 0.05f;   // clip seconds reserved for the raised sword
        const float PlantDropSeconds = PlantHitAt - PlantRaisedPose;

        public readonly Boss Boss;
        public readonly FrostKnightTuning Tuning;
        public string BossName = "마력의 주인";

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

        // Only the first warning resolves damage. Extra throne strikes share its timing.
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
            _patterns[(int)FrostKnightAttack.Circle] = Pattern("대검 낙하", "E · 원형", "대검이 떨어집니다. 원형으로 벌리세요!", "ring", false);
            _patterns[(int)FrostKnightAttack.Charge] = Pattern("가로 차징", "세 줄 사이로", "검기가 방을 가로지릅니다!", "line", false);
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
                    if (_casting) break;
                    SetTargetable(false);
                    AnimSpeed = Mathf.Max(0.1f, Tuning.ChargeAnimSpeed);
                    if (StateTime >= _cast.Pattern.Time + (ChargeLength - ChargeHitAt) / AnimSpeed) Hide();
                    break;

                case FrostKnightState.Slamming:
                    if (_casting)
                    {
                        if (!_plantStarted && _cast.Remaining <= PlantHitAt + PlantLead)
                        {
                            _plantStarted = true;
                            float windup = Mathf.Max(PlantRaisedPose, _cast.Remaining - PlantDropSeconds);
                            Play("SwordPlant", PlantRaisedPose / windup);
                        }
                        if (_plantStarted && _cast.Remaining <= PlantDropSeconds) AnimSpeed = 1f;
                    }
                    else
                    {
                        AnimSpeed = 1f;
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
            if (_random() < 0.5f)
            {
                Play("RaiseHands");
                float maxAngle = Mathf.Clamp(Tuning.ParallelMaxAngle, 0f, 45f);
                float angle = _yaw + Mathf.Lerp(-maxAngle, maxAngle, _random());
                Cast(FrostKnightAttack.Parallel, DangerShape.BandsX(Tuning.ParallelInner, Tuning.ParallelOuter, Tuning.ParallelHalfLength),
                    _squad.Anchor, angle, Tuning.SlamHitSeconds, false);
            }
            else
            {
                float warn = Mathf.Max(.1f, Tuning.CircleHitSeconds);
                Play("RaiseHands", ThroneMotionHitAt / warn);
                // Dodged by the ring formation, so it shrinks with the squad the way the ring does.
                var ring = _squad.Formations.Find(f => f != null && f.Mode == FormationMode.Ring);
                float radius = Tuning.CircleRadius;
                if (Tuning.CircleScalesWithSquad)
                    radius = ring != null ? FormationSlots.RingRadius(ring.Radius, _squad.AliveCount) * Tuning.CircleRingRatio
                        : Tuning.CircleRadius * FormationSlots.SquadFraction(_squad.AliveCount);
                Cast(FrostKnightAttack.Circle, DangerShape.Disc(radius), _squad.Anchor, _yaw, warn, false);
            }
            _wait = Tuning.SlamRecoverSeconds + Tuning.ThroneAttackGap;
        }

        void AddThroneStrikes()
        {
            float limit = Mathf.Clamp(Tuning.ParallelMaxAngle, 0f, 45f);
            float offset = Mathf.DeltaAngle(_yaw, _cast.Angle);
            // Drop the closest of left diagonal, straight and right diagonal. At the authored
            // 45 degree limit the remaining headings differ from the main strike by at least 22.5.
            float firstAngle = offset < -.5f * limit ? _yaw : _yaw - limit;
            float secondAngle = offset > .5f * limit ? _yaw : _yaw + limit;
            var first = EmptyStrikeCandidates(firstAngle);
            var second = EmptyStrikeCandidates(secondAngle);
            BossAttackSystem.Active? bestFirst = null, bestSecond = null;
            float bestClearance = float.NegativeInfinity;
            foreach (var a in first)
            {
                float clearance = StrikeSeparation(_cast, a);
                foreach (var b in second)
                {
                    float score = Mathf.Min(clearance, Mathf.Min(StrikeSeparation(_cast, b), StrikeSeparation(a, b)));
                    if (score <= bestClearance || (a.Cx == b.Cx && a.Cz == b.Cz)) continue;
                    bestClearance = score;
                    bestFirst = a;
                    bestSecond = b;
                }
            }
            if (bestFirst.HasValue)
            {
                Telegraphs.Add(bestFirst.Value);
                Telegraphs.Add(bestSecond.Value);
                return;
            }
            // A smaller room or scattered soldiers may leave room for only one copy.
            foreach (var candidate in first) KeepFurthest(candidate);
            foreach (var candidate in second) KeepFurthest(candidate);
            if (bestFirst.HasValue) Telegraphs.Add(bestFirst.Value);

            void KeepFurthest(BossAttackSystem.Active candidate)
            {
                float score = StrikeSeparation(_cast, candidate);
                if (score <= bestClearance) return;
                bestClearance = score;
                bestFirst = candidate;
            }
        }

        List<BossAttackSystem.Active> EmptyStrikeCandidates(float angle)
        {
            var shape = _cast.Shape;
            bool circle = shape.Type == DangerShapeType.Disc;
            float radians = (angle - _yaw) * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(radians)), sin = Mathf.Abs(Mathf.Sin(radians));
            float insetX = circle ? shape.RMax : shape.Outer * cos + shape.HalfZ * sin;
            float insetZ = circle ? shape.RMax : shape.Outer * sin + shape.HalfZ * cos;
            var range = _size * .5f - new Vector2(insetX, insetZ);
            var candidates = new List<BossAttackSystem.Active>(9);
            if (range.x < 0f || range.y < 0f) return candidates;
            var footprint = circle ? DangerShape.Disc(shape.RMax + .5f) : DangerShape.Rectangle(shape.Outer + .5f, shape.HalfZ + .5f);
            int start = Mathf.FloorToInt(_random() * 9f);
            for (int i = 0; i < 9; i++)
            {
                int cell = (start + i) % 9;
                var at = Point((cell % 3 - 1) * range.x, (cell / 3 - 1) * range.y);
                if (DangerShapeEval.InDanger(footprint, at.x, at.y, _cast.Cx, _cast.Cz, angle)) continue;
                if (_squad.Units.Exists(u => !u.Dead && DangerShapeEval.InDanger(footprint, at.x, at.y, u.X, u.Z, angle))) continue;
                var copy = _cast;
                copy.Cx = at.x;
                copy.Cz = at.y;
                copy.Angle = Mathf.Repeat(angle, 360f);
                candidates.Add(copy);
            }
            return candidates;
        }

        // Positive when the two footprints are apart; negative when distant tips must cross.
        static float StrikeSeparation(BossAttackSystem.Active a, BossAttackSystem.Active b)
        {
            var delta = new Vector2(b.Cx - a.Cx, b.Cz - a.Cz);
            if (a.Shape.Type == DangerShapeType.Disc) return delta.magnitude - 2f * a.Shape.RMax;
            float radians = (a.Angle - b.Angle) * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(radians)), sin = Mathf.Abs(Mathf.Sin(radians));
            float xReach = a.Shape.Outer * (1f + cos) + a.Shape.HalfZ * sin;
            float zReach = a.Shape.HalfZ * (1f + cos) + a.Shape.Outer * sin;
            return Mathf.Max(GapOnAxes(a.Angle), GapOnAxes(b.Angle));

            float GapOnAxes(float angle)
            {
                var p = Quaternion.Euler(0f, -angle, 0f) * new Vector3(delta.x, 0f, delta.y);
                return Mathf.Max(Mathf.Abs(p.x) - xReach, Mathf.Abs(p.z) - zReach);
            }
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
            float speed = Mathf.Max(0.1f, Tuning.ChargeAnimSpeed) / ChargeWindupScale;
            SetTargetable(Tuning.ChargeCanBeHit);
            Play("ChargedSlash", speed);
            var line = _squad.Find("line");
            float soldierSpacing = line != null ? line.Spacing : .49f;
            float gap = 2f * (soldierSpacing + Tuning.ChargeGapPadding);
            var shape = new DangerShape
            {
                Type = DangerShapeType.ThreeLanes, LaneHalfWidth = Tuning.ChargeRowWidth * .5f,
                LaneSpacing = Tuning.ChargeRowWidth + gap, HalfZ = _size.x * .5f - 1f
            };
            Cast(FrostKnightAttack.Charge, shape, Point(0f, shift), Boss.Facing, ChargeHitAt / speed, false);
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
            Cast(attack, DangerShape.Rectangle(halfWidth, _size.y * 0.5f), Point(x, 0f), _yaw, Tuning.LethalWarnSeconds, true);
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
                GrowLanesThroughCast = attack == FrostKnightAttack.Charge,
                WarningFadeSeconds = attack == FrostKnightAttack.Circle || attack == FrostKnightAttack.Parallel ? .4f : 0f,
                WarningEndLead = attack == FrostKnightAttack.Circle ? .36f : 0f, // blade descent after the disc fades
            };
            Telegraphs.Clear();
            Telegraphs.Add(_cast);
            if (attack == FrostKnightAttack.Parallel || attack == FrostKnightAttack.Circle) AddThroneStrikes();
            OnPatternStarted?.Invoke(p);
            OnCast?.Invoke(attack, _cast);
        }

        void TickCast(float dt)
        {
            if (!_casting) return;
            _cast.Elapsed += dt;
            _cast.Remaining = Mathf.Max(0f, _cast.Pattern.Time - _cast.Elapsed);
            Telegraphs[0] = _cast;
            for (int i = 1; i < Telegraphs.Count; i++)
            {
                var copy = Telegraphs[i];
                copy.Elapsed = _cast.Elapsed;
                copy.Remaining = _cast.Remaining;
                Telegraphs[i] = copy;
            }
            if (_cast.Remaining > 0f) return;
            _casting = false;
            if (Attack == FrostKnightAttack.Circle) AnimSpeed = 1f;
            Resolve(); // The view uses the exact warned poses at impact.
            Telegraphs.Clear();
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
