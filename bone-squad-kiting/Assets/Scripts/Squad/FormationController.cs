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
        static readonly Vector2 StartAnchor = new(0, 3.4f);

        public List<FormationDefinition> Formations = new(); // assigned in the scene (or by tests)
        public Vector2 Anchor = StartAnchor;                  // Vector2(x, z)
        public Vector2 Destination = StartAnchor;
        public string Formation = DefaultFormation;
        public float RotationStepDegrees = 15f;              // one mouse-wheel notch (initial value, not final)
        public float RotationDegrees;                         // clockwise seen from above; 0 = as authored
        public SquadOrder Order;
        public readonly List<SquadUnit> Units = new();

        /// <summary>True while the anchor has not yet reached its destination.</summary>
        public bool IsMoving => (Destination - Anchor).magnitude > GameConstants.AnchorArrive;

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
            Formation = DefaultFormation;
            RotationDegrees = 0;
            Order = SquadOrder.Move(StartAnchor);
            var slots = FormationSlots.Generate(def);
            for (int i = 0; i < GameConstants.SquadSize; i++)
                Units.Add(new SquadUnit
                {
                    Id = i, Slot = slots[i],
                    X = StartAnchor.x + slots[i].x, Z = StartAnchor.y + slots[i].y,
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
            RotationDegrees = Mathf.Repeat(RotationDegrees + clockwiseNotches * RotationStepDegrees, 360f);
            ApplySlots(def);
        }

        /// <summary>Stop where the anchor is now.</summary>
        public void Halt() { Destination = Anchor; }

        void ApplySlots(FormationDefinition def)
        {
            var slots = FormationSlots.Rotate(FormationSlots.Generate(def), RotationDegrees);
            for (int i = 0; i < Units.Count; i++) Units[i].Slot = slots[i];
        }

        /// <summary>Set where the anchor walks to, clamped to the commander's allowed area.</summary>
        public void SetDestination(Vector2 d)
        {
            Destination = new Vector2(
                Mathf.Clamp(d.x, -GameConstants.MoveClampX, GameConstants.MoveClampX),
                Mathf.Clamp(d.y, GameConstants.MoveClampZMin, GameConstants.MoveClampZMax));
        }

        public void Tick(float dt)
        {
            Vector2 diff = Destination - Anchor;
            float dist = diff.magnitude;
            if (dist > GameConstants.AnchorArrive)
                Anchor += diff / dist * Mathf.Min(dist, GameConstants.AnchorSpeed * dt);
            foreach (var u in Units) u.MoveToward(Anchor, dt);
        }
    }
}
