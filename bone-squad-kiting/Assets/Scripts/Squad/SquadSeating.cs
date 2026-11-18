using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// Decides where each unit actually stands when part of the formation falls outside the walkable area.
    /// Units whose slot is on walkable ground stand on it. The others are "pressed": each takes the nearest
    /// free seat on a world-aligned lattice of unit-sized cells, so pressed units line up along the wall
    /// instead of stacking on one point.
    /// </summary>
    public static class SquadSeating
    {
        const int MaxRings = 12;

        /// <param name="anchor">Formation anchor (assumed walkable).</param>
        /// <param name="slots">Slot offsets from the anchor, one per unit.</param>
        /// <param name="seatSize">Lattice spacing; about one unit spacing.</param>
        /// <returns>World position for each unit, in the same order as <paramref name="slots"/>.</returns>
        public static Vector2[] Resolve(WalkGrid grid, Vector2 anchor, IReadOnlyList<Vector2> slots, float seatSize)
        {
            int n = slots.Count;
            var result = new Vector2[n];
            var taken = new HashSet<Vector2Int>();
            var pressed = new List<int>();

            // 1. Units whose slot is reachable stand on it and claim the seat under it.
            for (int i = 0; i < n; i++)
            {
                var p = anchor + slots[i];
                if (grid.IsWalkable(p)) { result[i] = p; taken.Add(Seat(p, seatSize)); }
                else pressed.Add(i);
            }
            if (pressed.Count == 0) return result;

            // 2. Pressed units, innermost slot first, so the formation's inside-out order mostly survives.
            pressed.Sort((a, b) =>
            {
                int byDist = slots[a].sqrMagnitude.CompareTo(slots[b].sqrMagnitude);
                return byDist != 0 ? byDist : a.CompareTo(b);
            });
            foreach (int i in pressed)
            {
                var slot = anchor + slots[i];
                var start = Seat(slot, seatSize);
                // Fallback when no free seat turns up within MaxRings: the last walkable point toward the slot.
                result[i] = grid.LastWalkableToward(anchor, slot);
                float best = float.MaxValue; bool found = false; Vector2Int bestSeat = default;
                // Stop at the first ring that has a candidate, plus one more: a seat on the next ring can still
                // be closer to the slot than a corner seat of this one.
                int lastRing = MaxRings;
                for (int ring = 0; ring <= lastRing; ring++)
                {
                    for (int dz = -ring; dz <= ring; dz++)
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;   // only the ring's edge
                            var seat = new Vector2Int(start.x + dx, start.y + dz);
                            if (taken.Contains(seat)) continue;
                            var pos = SeatPosition(seat, seatSize);
                            if (!grid.IsWalkable(pos)) continue;
                            float d = (pos - slot).sqrMagnitude;   // the free seat closest to where the unit wanted to be
                            if (d < best) { best = d; bestSeat = seat; }
                            if (!found) { found = true; lastRing = Mathf.Min(MaxRings, ring + 1); }
                        }
                }
                if (!found) continue;
                taken.Add(bestSeat);
                result[i] = SeatPosition(bestSeat, seatSize);
            }
            return result;
        }

        // Seats sit at the centers of the lattice cells (offset by half a seat), which keeps them off the
        // exact lines where walkable-grid cells usually begin.
        static Vector2Int Seat(Vector2 p, float seatSize)
            => new(Mathf.FloorToInt(p.x / seatSize), Mathf.FloorToInt(p.y / seatSize));

        static Vector2 SeatPosition(Vector2Int seat, float seatSize)
            => new((seat.x + 0.5f) * seatSize, (seat.y + 0.5f) * seatSize);
    }
}
