using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// The squad as one commanded body: an anchor that walks to a destination, and units that
    /// follow anchor + formation slot. Driven by Tick(dt) so tests and the game director control time.
    /// </summary>
    public class FormationController : MonoBehaviour
    {
        public const string DefaultFormation = "dense";
        static readonly Vector2 DefaultStart = new(0, 3.4f);

        public List<FormationDefinition> Formations = new(); // assigned in the scene (or by tests)
        public Vector2 StartAnchor = DefaultStart;            // where Init() places the squad; a stage overrides it
        public Vector2 Anchor = DefaultStart;                 // Vector2(x, z)
        public Vector2 Destination = DefaultStart;

        /// <summary>
        /// The stage's walkable ground. When set, the anchor cannot leave it and slots that fall outside
        /// are pulled back toward the anchor. When null the prototype arena's fixed limits apply.
        /// </summary>
        [System.NonSerialized] public WalkGrid Grid;
        public bool HasLeader = true;   // unit 0 is the squad leader: fixed at the formation's center (on the rim in a ring)

        /// <summary>
        /// Direction from the anchor to the last point the player clicked. In a ring formation the leader
        /// stands on the rim in this direction.
        /// </summary>
        public Vector2 LeaderFacing = new(0f, -1f);

        public float SeatSize = 0.5f;   // spacing of the seats pressed units take along a wall (about one unit spacing)

        [Header("Movement feel (overridden by the GameTuning asset when the director has one)")]
        public float AnchorSpeed = GameConstants.AnchorSpeed;             // how fast the formation's center travels
        public float AnchorArrive = GameConstants.AnchorArrive;           // closer than this to the destination = arrived
        public float UnitBaseSpeed = GameConstants.UnitBaseSpeed;         // a unit's speed toward its slot when it is on it
        public float UnitSpeedDistGain = GameConstants.UnitSpeedDistGain; // extra speed per unit of distance from its slot...
        public float UnitSpeedDistCap = GameConstants.UnitSpeedDistCap;   // ...up to this much
        public float UnitArrive = GameConstants.UnitArrive;               // closer than this to its slot = standing (can fire)

        FlowField _route;               // path to the current destination, used when the straight line is blocked
        readonly List<Vector2> _slotBuffer = new();
        readonly List<SquadUnit> _aliveBuffer = new();
        public string Formation = DefaultFormation;
        public float RotationStepDegrees = 15f;              // one mouse-wheel notch (initial value, not final)
        public float RotationDegrees;                         // clockwise seen from above; 0 = as authored
        public float TargetRotationDegrees;                   // where the wheel has asked the formation to turn to

        [Tooltip("How fast the formation turns toward the angle set with the wheel, in degrees per second. 0 = instantly.")]
        public float RotationSpeed = 0f;
        public SquadOrder Order;
        public readonly List<SquadUnit> Units = new();

        /// <summary>True while the anchor has not yet reached its destination.</summary>
        public int AliveCount
        {
            get { int n = 0; foreach (var u in Units) if (!u.Dead) n++; return n; }
        }

        public bool IsMoving => (Destination - Anchor).magnitude > AnchorArrive;

        public FormationDefinition Find(string id) => Formations.Find(f => f != null && f.Id == id);

        /// <summary>Reset to the prototype's starting layout: dense formation at z = 3.4.</summary>
        public void Init()
        {
            var def = Find(DefaultFormation);
            if (def == null)
            {
                Debug.LogError($"FormationController: no '{DefaultFormation}' formation assigned.", this);
                return;
            }
            Units.Clear();
            Anchor = Destination = StartAnchor;
            Formation = DefaultFormation; _slotsFormation = DefaultFormation;
            RotationDegrees = 0; TargetRotationDegrees = 0;
            LeaderFacing = new Vector2(0f, -1f);
            Order = SquadOrder.Move(StartAnchor);
            var slots = FormationSlots.Generate(def, GameConstants.SquadSize);
            for (int i = 0; i < GameConstants.SquadSize; i++)
                Units.Add(new SquadUnit
                {
                    Id = i, Slot = slots[i], IsLeader = HasLeader && i == 0,   // slot 0 is the one at the center
                    X = SlotPosition(slots[i]).x, Z = SlotPosition(slots[i]).y,
                    Cd = Random.value * 0.65f, Anim = Random.value * 6.28f,
                });
        }

        /// <summary>Switch formation. Returns false when the id is unknown.</summary>
        public bool SetFormation(string id)
        {
            var def = Find(id);
            if (def == null) return false;
            Formation = id;
            ApplySlots(def);
            return true;
        }

        /// <summary>
        /// Turn the formation around the anchor by whole notches. Positive = clockwise seen from above.
        /// Units then walk to their new slots (and hold fire while they do).
        /// </summary>
        public void Rotate(int clockwiseNotches)
        {
            var def = Find(Formation);
            if (def == null || clockwiseNotches == 0) return;
            TargetRotationDegrees = Mathf.Repeat(TargetRotationDegrees + clockwiseNotches * RotationStepDegrees, 360f);
            if (RotationSpeed > 0f) return;   // Tick turns the formation toward the target at RotationSpeed
            RotationDegrees = TargetRotationDegrees;
            ApplySlots(def);
        }

        /// <summary>
        /// World position for a slot. A slot outside the playable area is pressed in: the unit stands at the
        /// last walkable point on the line from the anchor to the slot.
        /// </summary>
        public Vector2 SlotPosition(Vector2 slot)
        {
            var p = Anchor + slot;
            if (Grid != null) return Grid.LastWalkableToward(Anchor, p);
            return new Vector2(
                Mathf.Clamp(p.x, GameConstants.LimitXMin, GameConstants.LimitXMax),
                Mathf.Clamp(p.y, GameConstants.LimitZMin, GameConstants.LimitZMax));
        }

        // Which slot the leader takes: the one nearest the center — except in a ring, which has no center
        // slot, where it is the rim slot closest to the direction the player last clicked.
        int LeaderSlotIndex(FormationDefinition def, Vector2[] slots)
        {
            int best = 0; float bestScore = float.MaxValue;
            bool ring = def.Mode == FormationMode.Ring;
            for (int i = 0; i < slots.Length; i++)
            {
                float score = ring
                    ? -Vector2.Dot(slots[i].normalized, LeaderFacing)   // most aligned with the facing
                    : slots[i].sqrMagnitude;                            // closest to the center
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Tell the squad where the player last clicked. Only the direction matters, and only a ring
        /// formation shows it: the leader moves along the rim to face that way.
        /// </summary>
        public void FaceToward(Vector2 worldPoint)
        {
            var dir = worldPoint - Anchor;
            if (dir.sqrMagnitude < 1e-4f) return;   // clicked on the anchor itself: keep the last direction
            LeaderFacing = dir.normalized;
            var def = Find(Formation);
            if (HasLeader && def != null && def.Mode == FormationMode.Ring) ApplySlots(def);
        }

        /// <summary>Stop where the anchor is now.</summary>
        public void Halt() { Destination = Anchor; }

        /// <summary>
        /// Rebuild the formation for the units still alive. Call after units die: the shape closes up
        /// around the anchor instead of leaving gaps.
        /// </summary>
        public void Reform()
        {
            var def = Find(Formation);
            if (def != null) ApplySlots(def);
        }

        // Slots are generated for the living units only, and each unit takes the slot nearest to where it
        // stands, so a change moves the squad as little as possible (moving units cannot fire).
        void ApplySlots(FormationDefinition def)
        {
            var alive = Units.FindAll(u => !u.Dead);
            if (alive.Count == 0) return;
            var slots = FormationSlots.Rotate(FormationSlots.Generate(def, alive.Count), RotationDegrees);

            // The leader's slot is fixed and taken out before the others are matched to what is left.
            var leader = alive.Find(u => u.IsLeader);
            if (leader != null)
            {
                int index = LeaderSlotIndex(def, slots);
                leader.Slot = slots[index];
                alive.Remove(leader);
                var rest = new Vector2[slots.Length - 1];
                for (int i = 0, k = 0; i < slots.Length; i++) if (i != index) rest[k++] = slots[i];
                slots = rest;
            }

            // Already standing in this ring: keep everyone's order around it. Only the units between the
            // leader's old place and its new one step along by one; nobody swaps across the circle.
            bool keepOrder = def.Mode == FormationMode.Ring && _slotsFormation == def.Id;
            _slotsFormation = def.Id;
            if (keepOrder) { AssignAroundRing(alive, slots); return; }

            var positions = new Vector2[alive.Count];
            for (int i = 0; i < alive.Count; i++) positions[i] = new Vector2(alive[i].X, alive[i].Z) - Anchor;
            var assignment = FormationSlots.AssignNearest(positions, slots);
            for (int i = 0; i < alive.Count; i++) alive[i].Slot = slots[assignment[i]];
        }

        string _slotsFormation;   // the formation the units' current slots belong to

        // Units and free slots are both put in order around the circle; the units keep that order and take
        // the slots at whichever turn of the circle moves them least.
        static void AssignAroundRing(List<SquadUnit> units, Vector2[] slots)
        {
            int n = units.Count;
            if (n == 0) return;
            static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x);
            units.Sort((a, b) => Angle(a.Slot).CompareTo(Angle(b.Slot)));
            var ordered = (Vector2[])slots.Clone();
            System.Array.Sort(ordered, (a, b) => Angle(a).CompareTo(Angle(b)));
            int bestShift = 0; float bestCost = float.MaxValue;
            for (int shift = 0; shift < n; shift++)
            {
                float cost = 0f;
                for (int i = 0; i < n && cost < bestCost; i++) cost += (units[i].Slot - ordered[(i + shift) % n]).sqrMagnitude;
                if (cost < bestCost) { bestCost = cost; bestShift = shift; }
            }
            for (int i = 0; i < n; i++) units[i].Slot = ordered[(i + bestShift) % n];
        }

        /// <summary>Set where the anchor walks to, clamped to the commander's allowed area.</summary>
        public void SetDestination(Vector2 d)
        {
            if (Grid != null)
            {
                // A click on blocked ground goes to the nearest walkable point. If there is no walkable route
                // there at all, the squad stays where it is.
                var target = Grid.NearestWalkable(d);
                if (_route == null || _route.Grid != Grid) _route = new FlowField(Grid);
                _route.Build(Grid.Cell(target));
                Destination = _route.DistanceAt(Anchor) == FlowField.Unreachable ? Anchor : target;
                return;
            }
            Destination = new Vector2(
                Mathf.Clamp(d.x, -GameConstants.MoveClampX, GameConstants.MoveClampX),
                Mathf.Clamp(d.y, GameConstants.MoveClampZMin, GameConstants.MoveClampZMax));
        }

        public void Tick(float dt)
        {
            // With a turning speed set, the formation swings toward the wheel's target angle a little each frame.
            if (RotationSpeed > 0f && Mathf.Abs(Mathf.DeltaAngle(RotationDegrees, TargetRotationDegrees)) > 0.01f)
            {
                RotationDegrees = Mathf.Repeat(Mathf.MoveTowardsAngle(RotationDegrees, TargetRotationDegrees, RotationSpeed * dt), 360f);
                var turning = Find(Formation);
                if (turning != null) ApplySlots(turning);
            }

            Vector2 diff = Destination - Anchor;
            float dist = diff.magnitude;
            if (dist > AnchorArrive)
            {
                float step = Mathf.Min(dist, AnchorSpeed * dt);
                var dir = diff / dist;
                if (Grid != null && _route != null && !Grid.LineOpen(Anchor, Destination))
                {
                    // Something is in the way: follow the route around it until the destination is in sight.
                    var routed = _route.DirectionAt(Anchor);
                    if (routed != Vector2.zero) dir = routed;
                }
                var next = Anchor + dir * step;
                if (Grid != null)
                {
                    next = Grid.Sweep(Anchor, next);      // brush past wall corners without losing pace
                    if (next == Anchor) Halt();           // boxed in: give up rather than push forever
                }
                Anchor = next;
            }

            foreach (var u in Units) { u.BaseSpeed = UnitBaseSpeed; u.SpeedDistGain = UnitSpeedDistGain; u.SpeedDistCap = UnitSpeedDistCap; u.Arrive = UnitArrive; }

            if (Grid == null)
            {
                foreach (var u in Units) u.MoveToward(SlotPosition(u.Slot), dt);
                return;
            }
            // On a stage grid, units whose slot is outside the area take seats along the wall.
            _aliveBuffer.Clear(); _slotBuffer.Clear();
            foreach (var u in Units) if (!u.Dead) { _aliveBuffer.Add(u); _slotBuffer.Add(u.Slot); }
            var targets = SquadSeating.Resolve(Grid, Anchor, _slotBuffer, SeatSize);
            for (int i = 0; i < _aliveBuffer.Count; i++) _aliveBuffer[i].MoveToward(targets[i], dt);
        }
    }
}
