using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Core
{
    public static class FormationSlots
    {
        /// <summary>
        /// Slot offsets from the squad anchor as Vector2(x, z) for <paramref name="count"/> units.
        /// Slots are filled from the anchor outward, so a smaller squad makes a smaller version of the
        /// same shape instead of a full-size shape with holes. Slot 0 is the one closest to the anchor.
        /// </summary>
        public static Vector2[] Generate(FormationDefinition d, int count)
        {
            if (count <= 0) return new Vector2[0];
            switch (d.Mode)
            {
                case FormationMode.Grid: return Grid(d.Spacing, count);
                case FormationMode.Line: return Line(d.Spacing, d.Stagger, count);
                case FormationMode.Ring: return Ring(d.Radius, count);
                case FormationMode.Cross: return Cross(d.Spacing, count);
                default: return new Vector2[count];
            }
        }

        /// <summary>
        /// Ring radius for a squad of <paramref name="count"/>. <paramref name="fullRadius"/> is the radius
        /// of a full squad; fewer units keep the same spacing along the circle, so the ring shrinks.
        /// </summary>
        public static float RingRadius(float fullRadius, int count)
            => fullRadius * SquadFraction(count);

        /// <summary>How full the squad is, 0..1. Patterns that scale with the squad use the same factor.</summary>
        public static float SquadFraction(int count)
            => Mathf.Clamp01(count / (float)GameConstants.SquadSize);

        // Cells of the smallest odd-sided square grid that holds the squad, nearest to the center first.
        static Vector2[] Grid(float spacing, int count)
        {
            int side = 1;
            while (side * side < count) side += 2;
            int half = side / 2;
            var cells = new List<Vector2Int>(side * side);
            for (int z = -half; z <= half; z++)
                for (int x = -half; x <= half; x++)
                    cells.Add(new Vector2Int(x, z));
            cells.Sort((a, b) =>
            {
                int byDist = a.sqrMagnitude.CompareTo(b.sqrMagnitude);
                if (byDist != 0) return byDist;
                return Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x)); // fixed order within a ring
            });
            var o = new Vector2[count];
            for (int i = 0; i < count; i++) o[i] = new Vector2(cells[i].x * spacing, cells[i].y * spacing);
            return o;
        }

        // Staggered column along z, growing from the center in both directions: 0, +1, -1, +2, -2, ...
        static Vector2[] Line(float spacing, float stagger, int count)
        {
            var o = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int p = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1); // 0, 1, -1, 2, -2, ...
                int lane = ((p % 3) + 3) % 3 - 1;            // -1, 0, 1 repeating along the line
                o[i] = new Vector2(lane * stagger, p * spacing);
            }
            return o;
        }

        // Evenly spaced on a circle that shrinks with the squad. No unit stands on the anchor.
        static Vector2[] Ring(float fullRadius, int count)
        {
            float r = RingRadius(fullRadius, count);
            var o = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * 2f * i / count;
                o[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return o;
        }

        // Center first, then the four arms one step at a time: -x, +x, -z, +z.
        static Vector2[] Cross(float spacing, int count)
        {
            var o = new Vector2[count];
            for (int i = 1; i < count; i++)
            {
                float step = ((i - 1) / 4 + 1) * spacing;
                switch ((i - 1) % 4)
                {
                    case 0: o[i] = new Vector2(-step, 0); break;
                    case 1: o[i] = new Vector2(step, 0); break;
                    case 2: o[i] = new Vector2(0, -step); break;
                    default: o[i] = new Vector2(0, step); break;
                }
            }
            return o;
        }

        /// <summary>
        /// Rotate slot offsets around the anchor. Positive degrees turn clockwise as seen from above
        /// (a slot at +z moves toward +x). Returns a new array.
        /// </summary>
        public static Vector2[] Rotate(Vector2[] slots, float clockwiseDegrees)
        {
            float rad = clockwiseDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            var o = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                o[i] = new Vector2(slots[i].x * cos + slots[i].y * sin, -slots[i].x * sin + slots[i].y * cos);
            return o;
        }

        /// <summary>
        /// Pair each unit with a slot so that units move as little as possible: the closest unit/slot pair
        /// is matched first, then the next closest among what is left. Both arrays must be the same length.
        /// Returns, for each unit, the index of its slot.
        /// </summary>
        public static int[] AssignNearest(Vector2[] unitPositions, Vector2[] slotPositions)
        {
            int n = unitPositions.Length;
            var pairs = new List<(float d, int unit, int slot)>(n * n);
            for (int u = 0; u < n; u++)
                for (int s = 0; s < n; s++)
                    pairs.Add(((unitPositions[u] - slotPositions[s]).sqrMagnitude, u, s));
            pairs.Sort((a, b) =>
            {
                int byDist = a.d.CompareTo(b.d);
                if (byDist != 0) return byDist;
                int byUnit = a.unit.CompareTo(b.unit);
                return byUnit != 0 ? byUnit : a.slot.CompareTo(b.slot);
            });
            var result = new int[n];
            var unitDone = new bool[n];
            var slotTaken = new bool[n];
            foreach (var p in pairs)
            {
                if (unitDone[p.unit] || slotTaken[p.slot]) continue;
                result[p.unit] = p.slot;
                unitDone[p.unit] = true; slotTaken[p.slot] = true;
            }
            return result;
        }
    }
}
