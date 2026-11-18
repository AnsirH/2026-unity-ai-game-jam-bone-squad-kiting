using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Turns the level art into a WalkGrid when the stage loads. No bake step: for each cell it casts a
    /// ray straight down, and the cell is walkable when the ray lands on a Ground collider and no
    /// Obstacle collider is within <see cref="Clearance"/> of that point.
    ///
    /// Level rules: floors need a collider on the Ground layer; anything that blocks movement needs a
    /// collider on the Obstacle layer; this object's box (Size) must cover the whole playable area.
    /// WalkOutline objects in the same scene narrow it further: a cell must also be inside an Include
    /// outline (when any exist) and outside every Exclude outline. A flat terrain is one big Ground
    /// collider, so outlines are how its playable area gets its shape.
    /// Select the object to see the result drawn in the Scene view.
    /// </summary>
    public class WalkGridBaker : MonoBehaviour
    {
        public Vector2 Size = new(24.6f, 20.4f);   // x and z extent of the area, centered on this object
        [Tooltip("Ignore this object's position and Size: cover the Include outlines drawn in the scene instead (or, with none, every collider on the ground layers). For maps whose size is not known in advance.")]
        public bool FitToScene;
        public float FitPadding = 1.5f;
        public float CellSize = 0.75f;
        public float Clearance = 0.3f;             // how close an obstacle may be to a cell center
        public float RayHeight = 50f;              // ray starts this far above the object
        public LayerMask GroundMask;
        public LayerMask ObstacleMask;
        public bool DrawGizmo = true;

        public WalkGrid Grid { get; private set; }

        /// <summary>Build (or rebuild) the grid from the colliders currently in the scene.</summary>
        public WalkGrid Bake()
        {
            Physics.SyncTransforms();
            Area(out var center, out var area);
            var min = center - area / 2f;
            int w = Mathf.Max(1, Mathf.CeilToInt(area.x / CellSize));
            int h = Mathf.Max(1, Mathf.CeilToInt(area.y / CellSize));
            var grid = new WalkGrid(min, CellSize, w, h);
            float top = transform.position.y + RayHeight;

            // Outlines drawn in this scene. Shapes with fewer than three points are ignored.
            var includes = new List<Vector2[]>(); var excludes = new List<Vector2[]>();
            foreach (var outline in FindObjectsByType<WalkOutline>(FindObjectsSortMode.None))
            {
                if (outline.gameObject.scene != gameObject.scene) continue;
                var points = outline.Points();
                if (points.Length < 3) continue;
                (outline.Mode == WalkOutlineMode.Include ? includes : excludes).Add(points);
            }
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var c = grid.CellCenter(x, z);
                    if (!InsideOutlines(c, includes, excludes)) continue; // stays blocked
                    var from = new Vector3(c.x, top, c.y);
                    bool ground = Physics.Raycast(from, Vector3.down, out var hit, RayHeight * 2f, GroundMask, QueryTriggerInteraction.Ignore);
                    grid.Walkable[grid.Index(x, z)] = ground
                        && !Physics.CheckSphere(hit.point + Vector3.up * Clearance, Clearance, ObstacleMask, QueryTriggerInteraction.Ignore);
                }
            Grid = grid;
            return grid;
        }

        /// <summary>The rectangle (center and size on x/z) the grid covers.</summary>
        public void Area(out Vector2 center, out Vector2 size)
        {
            center = new Vector2(transform.position.x, transform.position.z); size = Size;
            if (!FitToScene) return;
            bool any = false; Vector2 lo = default, hi = default;
            void Grow(Vector2 p) { if (!any) { lo = hi = p; any = true; } else { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); } }
            foreach (var outline in FindObjectsByType<WalkOutline>(FindObjectsSortMode.None))
            {
                if (outline.gameObject.scene != gameObject.scene || outline.Mode != WalkOutlineMode.Include) continue;
                var points = outline.Points();
                if (points.Length < 3) continue;
                foreach (var p in points) Grow(p);
            }
            if (!any)
                foreach (var col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (col.gameObject.scene != gameObject.scene || (GroundMask.value & (1 << col.gameObject.layer)) == 0) continue;
                    Grow(new Vector2(col.bounds.min.x, col.bounds.min.z)); Grow(new Vector2(col.bounds.max.x, col.bounds.max.z));
                }
            if (!any) return;
            center = (lo + hi) / 2f;
            size = hi - lo + Vector2.one * (FitPadding * 2f);
        }

        static bool InsideOutlines(Vector2 p, List<Vector2[]> includes, List<Vector2[]> excludes)
        {
            foreach (var hole in excludes) if (Polygon.Contains(hole, p)) return false;
            if (includes.Count == 0) return true;
            foreach (var area in includes) if (Polygon.Contains(area, p)) return true;
            return false;
        }

        void Awake() { if (Grid == null) Bake(); }

        void OnDrawGizmosSelected()
        {
            if (!DrawGizmo) return;
            var grid = Application.isPlaying && Grid != null ? Grid : Bake();
            float y = transform.position.y + 0.05f;
            var size = new Vector3(grid.CellSize * 0.9f, 0.01f, grid.CellSize * 0.9f);
            for (int z = 0; z < grid.Height; z++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var c = grid.CellCenter(x, z);
                    Gizmos.color = grid.Walkable[grid.Index(x, z)] ? new Color(0.2f, 1f, 0.4f, 0.35f) : new Color(1f, 0.2f, 0.2f, 0.35f);
                    Gizmos.DrawCube(new Vector3(c.x, y, c.y), size);
                }
            Gizmos.color = Color.white;
            Area(out var gc, out var gs);
            Gizmos.DrawWireCube(new Vector3(gc.x, transform.position.y, gc.y), new Vector3(gs.x, 0.1f, gs.y));
        }
    }
}
