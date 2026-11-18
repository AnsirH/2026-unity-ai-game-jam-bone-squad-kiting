using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Game
{
    /// <summary>
    /// Draws the two ranges in the Scene view so they can be judged while tuning them on GameDirector:
    /// the squad range as one circle around the formation anchor, and each unit's fire range as a circle
    /// around that unit. Follows inspector changes immediately, in edit mode and while playing.
    /// </summary>
    public class RangeGizmos : MonoBehaviour
    {
        const int Segments = 72;

        public GameDirector Director;
        public bool ShowSquadRange = true;
        public bool ShowUnitRanges = true;
        public Color SquadRangeColor = new(1f, 0.85f, 0.2f, 1f);     // yellow
        public Color UnitRangeColor = new(0.2f, 0.9f, 1f, 0.35f);    // faint cyan: there are up to 25 of these
        public float Height = 0.1f;

        void OnDrawGizmos()
        {
            if (Director == null || Director.Squad == null) return;
            var squad = Director.Squad;
            var anchor = squad.Anchor;

            if (ShowSquadRange)
            {
                Gizmos.color = SquadRangeColor;
                Circle(anchor, Director.SquadEngageRange);
                Circle(anchor, 0.15f); // marks the anchor itself
            }

            if (!ShowUnitRanges) return;
            Gizmos.color = UnitRangeColor;
            if (squad.Units.Count > 0)
            {
                foreach (var u in squad.Units)
                    if (!u.Dead) Circle(new Vector2(u.X, u.Z), Director.UnitFireRange);
            }
            else
            {
                // Not playing yet: show where the units will stand in the squad's starting formation.
                var def = squad.Find(squad.Formation);
                if (def == null) return;
                foreach (var slot in FormationSlots.Generate(def, GameConstants.SquadSize))
                    Circle(anchor + slot, Director.UnitFireRange);
            }
        }

        void Circle(Vector2 center, float radius)
        {
            var prev = new Vector3(center.x + radius, Height, center.y);
            for (int i = 1; i <= Segments; i++)
            {
                float a = Mathf.PI * 2f * i / Segments;
                var next = new Vector3(center.x + Mathf.Cos(a) * radius, Height, center.y + Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
