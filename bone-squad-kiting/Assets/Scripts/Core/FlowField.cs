using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>
    /// For every walkable cell, the direction that leads to a target cell by the shortest walkable route.
    /// Built with a breadth-first search over the grid; diagonal steps are allowed only when both
    /// neighbouring orthogonal cells are open, so nothing cuts through a wall corner.
    /// </summary>
    public class FlowField
    {
        public const int Unreachable = -1;

        public readonly WalkGrid Grid;
        public readonly int[] Distance;       // steps to the target, or Unreachable
        public readonly Vector2[] Direction;  // unit vector toward the target, zero at the target / unreachable
        public Vector2Int Target { get; private set; }

        static readonly Vector2Int[] Neighbours =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        public FlowField(WalkGrid grid)
        {
            Grid = grid;
            Distance = new int[grid.Width * grid.Height];
            Direction = new Vector2[grid.Width * grid.Height];
            Target = new Vector2Int(int.MinValue, int.MinValue);
        }

        /// <summary>Rebuild only when the target has moved to a different cell. Returns true if it rebuilt.</summary>
        public bool Retarget(Vector2 worldTarget)
        {
            var cell = Grid.Cell(worldTarget);
            if (cell == Target) return false;
            Build(cell);
            return true;
        }

        public void Build(Vector2Int target)
        {
            Target = target;
            for (int i = 0; i < Distance.Length; i++) { Distance[i] = Unreachable; Direction[i] = Vector2.zero; }
            if (!Grid.IsWalkable(target.x, target.y)) return;

            var queue = new Queue<Vector2Int>();
            Distance[Grid.Index(target.x, target.y)] = 0;
            queue.Enqueue(target);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int next = Distance[Grid.Index(c.x, c.y)] + 1;
                foreach (var n in Neighbours)
                {
                    int nx = c.x + n.x, nz = c.y + n.y;
                    if (!CanStep(c.x, c.y, n)) continue;
                    int ni = Grid.Index(nx, nz);
                    if (Distance[ni] != Unreachable) continue;
                    Distance[ni] = next;
                    queue.Enqueue(new Vector2Int(nx, nz));
                }
            }

            for (int z = 0; z < Grid.Height; z++)
                for (int x = 0; x < Grid.Width; x++)
                {
                    int i = Grid.Index(x, z);
                    if (Distance[i] <= 0) continue; // target cell or unreachable
                    int best = Distance[i]; Vector2Int step = default;
                    foreach (var n in Neighbours)
                    {
                        if (!CanStep(x, z, n)) continue;
                        int d = Distance[Grid.Index(x + n.x, z + n.y)];
                        if (d == Unreachable || d >= best) continue;
                        best = d; step = n;
                    }
                    Direction[i] = ((Vector2)step).normalized;
                }
        }

        // A step is legal when the destination is open and, for diagonals, both cells it squeezes between are open.
        bool CanStep(int x, int z, Vector2Int n)
        {
            if (!Grid.IsWalkable(x + n.x, z + n.y)) return false;
            if (n.x != 0 && n.y != 0) return Grid.IsWalkable(x + n.x, z) && Grid.IsWalkable(x, z + n.y);
            return true;
        }

        /// <summary>Direction to walk from a world position. Zero when off the grid, blocked or unreachable.</summary>
        public Vector2 DirectionAt(Vector2 world)
        {
            var c = Grid.Cell(world);
            return Grid.InBounds(c.x, c.y) ? Direction[Grid.Index(c.x, c.y)] : Vector2.zero;
        }

        public int DistanceAt(Vector2 world)
        {
            var c = Grid.Cell(world);
            return Grid.InBounds(c.x, c.y) ? Distance[Grid.Index(c.x, c.y)] : Unreachable;
        }
    }
}
