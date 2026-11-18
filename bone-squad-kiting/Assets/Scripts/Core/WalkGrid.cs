using UnityEngine;

namespace BoneSquadKiting.Core
{
    /// <summary>
    /// The walkable ground of a stage as a uniform grid on the XZ plane. One grid serves both the squad's
    /// movement boundary and enemy pathfinding (FlowField). Points are Vector2(x, z).
    /// </summary>
    public class WalkGrid
    {
        public readonly Vector2 Origin;   // world position of the grid's minimum corner
        public readonly float CellSize;
        public readonly int Width, Height; // cells along x, along z
        public readonly bool[] Walkable;   // index = z * Width + x

        public WalkGrid(Vector2 origin, float cellSize, int width, int height, bool[] walkable = null)
        {
            Origin = origin; CellSize = cellSize; Width = width; Height = height;
            Walkable = walkable ?? new bool[width * height];
        }

        /// <summary>A fully walkable rectangle, e.g. an open arena.</summary>
        public static WalkGrid Open(Vector2 min, Vector2 max, float cellSize)
        {
            int w = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / cellSize));
            int h = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / cellSize));
            var cells = new bool[w * h];
            for (int i = 0; i < cells.Length; i++) cells[i] = true;
            return new WalkGrid(min, cellSize, w, h, cells);
        }

        public int Index(int cx, int cz) => cz * Width + cx;
        public bool InBounds(int cx, int cz) => cx >= 0 && cz >= 0 && cx < Width && cz < Height;
        public bool IsWalkable(int cx, int cz) => InBounds(cx, cz) && Walkable[Index(cx, cz)];
        public bool IsWalkable(Vector2 p) { var c = Cell(p); return IsWalkable(c.x, c.y); }

        public Vector2Int Cell(Vector2 p)
            => new(Mathf.FloorToInt((p.x - Origin.x) / CellSize), Mathf.FloorToInt((p.y - Origin.y) / CellSize));

        public Vector2 CellCenter(int cx, int cz)
            => new(Origin.x + (cx + 0.5f) * CellSize, Origin.y + (cz + 0.5f) * CellSize);

        /// <summary>
        /// Move from <paramref name="from"/> toward <paramref name="to"/> without entering a blocked cell.
        /// If the full move is blocked it tries each axis alone, so things slide along walls.
        /// </summary>
        public Vector2 Slide(Vector2 from, Vector2 to)
        {
            if (IsWalkable(to)) return to;
            var xOnly = new Vector2(to.x, from.y);
            if (IsWalkable(xOnly)) return xOnly;
            var zOnly = new Vector2(from.x, to.y);
            if (IsWalkable(zOnly)) return zOnly;
            return from;
        }

        /// <summary>
        /// Walk the straight line from <paramref name="from"/> (assumed walkable) toward <paramref name="to"/>
        /// and return the last walkable point on it: <paramref name="to"/> itself when the whole way is open.
        /// Used to pull a formation slot that falls outside the playable area back toward the squad anchor.
        /// </summary>
        public Vector2 LastWalkableToward(Vector2 from, Vector2 to)
        {
            float dist = (to - from).magnitude;
            if (dist < 1e-5f) return from;
            int steps = Mathf.CeilToInt(dist / (CellSize * 0.25f));
            Vector2 last = from;
            for (int i = 1; i <= steps; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)steps);
                if (!IsWalkable(p)) break;
                last = p;
            }
            return last;
        }

        /// <summary>
        /// The walkable point nearest to <paramref name="p"/>: p itself when it is walkable, otherwise the
        /// center of the closest walkable cell. Used to turn a click on blocked ground into a legal destination.
        /// </summary>
        public Vector2 NearestWalkable(Vector2 p)
        {
            if (IsWalkable(p)) return p;
            float best = float.MaxValue; Vector2 result = p;
            for (int z = 0; z < Height; z++)
                for (int x = 0; x < Width; x++)
                {
                    if (!Walkable[Index(x, z)]) continue;
                    var c = CellCenter(x, z);
                    float d = (c - p).sqrMagnitude;
                    if (d < best) { best = d; result = c; }
                }
            return result;
        }
    }
}
