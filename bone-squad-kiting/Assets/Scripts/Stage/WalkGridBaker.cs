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
    /// Select the object to see the result drawn in the Scene view.
    /// </summary>
    public class WalkGridBaker : MonoBehaviour
    {
        public Vector2 Size = new(24.6f, 20.4f);   // x and z extent of the area, centered on this object
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
            var center = new Vector2(transform.position.x, transform.position.z);
            var min = center - Size / 2f;
            int w = Mathf.Max(1, Mathf.CeilToInt(Size.x / CellSize));
            int h = Mathf.Max(1, Mathf.CeilToInt(Size.y / CellSize));
            var grid = new WalkGrid(min, CellSize, w, h);
            float top = transform.position.y + RayHeight;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    var c = grid.CellCenter(x, z);
                    var from = new Vector3(c.x, top, c.y);
                    bool ground = Physics.Raycast(from, Vector3.down, out var hit, RayHeight * 2f, GroundMask, QueryTriggerInteraction.Ignore);
                    grid.Walkable[grid.Index(x, z)] = ground
                        && !Physics.CheckSphere(hit.point + Vector3.up * Clearance, Clearance, ObstacleMask, QueryTriggerInteraction.Ignore);
                }
            Grid = grid;
            return grid;
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
            Gizmos.DrawWireCube(transform.position, new Vector3(Size.x, 0.1f, Size.y));
        }
    }
}
