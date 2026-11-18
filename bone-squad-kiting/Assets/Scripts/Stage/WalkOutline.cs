using UnityEngine;

namespace BoneSquadKiting.Stage
{
    public enum WalkOutlineMode { Include, Exclude }

    /// <summary>
    /// A hand-drawn boundary for the walkable area. The child objects of this object are the corner
    /// points, in hierarchy order; the shape closes itself (last point back to first). Only x and z matter.
    ///
    /// Include: walkable ground must lie inside at least one Include outline (when any exist).
    /// Exclude: ground inside this outline is never walkable — a hole cut out of the Include area.
    /// With no outlines in the scene the walkable area is decided by colliders alone.
    /// </summary>
    public class WalkOutline : MonoBehaviour
    {
        public WalkOutlineMode Mode = WalkOutlineMode.Include;

        /// <summary>Corner points as Vector2(x, z), one per active child, in hierarchy order.</summary>
        public Vector2[] Points()
        {
            int n = 0;
            foreach (Transform child in transform) if (child.gameObject.activeSelf) n++;
            var points = new Vector2[n];
            int i = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                points[i++] = new Vector2(child.position.x, child.position.z);
            }
            return points;
        }

        void OnDrawGizmos()
        {
            var points = Points();
            Gizmos.color = points.Length < 3 ? Color.gray            // not a shape yet: ignored
                : Mode == WalkOutlineMode.Include ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.25f, 0.2f);
            float y = transform.position.y + 0.1f;
            for (int i = 0; i < points.Length; i++)
            {
                var a = new Vector3(points[i].x, y, points[i].y);
                var b = points[(i + 1) % points.Length];
                Gizmos.DrawSphere(a, 0.2f);
                if (points.Length >= 2) Gizmos.DrawLine(a, new Vector3(b.x, y, b.y));
            }
        }
    }
}
