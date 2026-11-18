using UnityEngine;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Holds the camera back until certain mini bosses are dead. The gate is a line through this object,
    /// square to its forward (blue) arrow: while it is closed the camera's view cannot be scrolled past
    /// the line in the arrow's direction. The squad itself is not blocked — it is moved by clicking, so it
    /// cannot be sent where the camera cannot look.
    /// </summary>
    public class CameraGate : MonoBehaviour
    {
        [Tooltip("The gate stays closed while any of these is alive (or has not appeared yet).")]
        public MiniBossSpawn[] Until;

        public bool Closed
        {
            get
            {
                if (Until == null) return false;
                foreach (var m in Until) if (m != null && !m.Defeated) return true;
                return false;
            }
        }

        /// <summary>Pull a camera look-at point back to the line if the gate is closed and the point is past it.</summary>
        public Vector2 Clamp(Vector2 lookAt)
        {
            if (!Closed) return lookAt;
            var f3 = transform.forward; var forward = new Vector2(f3.x, f3.z);
            if (forward.sqrMagnitude < 1e-6f) return lookAt;
            forward.Normalize();
            var origin = new Vector2(transform.position.x, transform.position.z);
            float past = Vector2.Dot(lookAt - origin, forward);
            return past > 0f ? lookAt - forward * past : lookAt;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Application.isPlaying && !Closed ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.3f, 0.9f);
            var p = transform.position + Vector3.up * 0.2f;
            var right = transform.right; right.y = 0; right.Normalize();
            var fwd = transform.forward; fwd.y = 0; fwd.Normalize();
            Gizmos.DrawLine(p - right * 15f, p + right * 15f);          // the line the camera may not cross
            Gizmos.DrawLine(p, p + fwd * 3f);                            // the side that is closed off
            Gizmos.DrawLine(p + fwd * 3f, p + fwd * 2.3f + right * 0.5f);
            Gizmos.DrawLine(p + fwd * 3f, p + fwd * 2.3f - right * 0.5f);
            if (Until == null) return;
            foreach (var m in Until) if (m != null) Gizmos.DrawLine(p, m.transform.position + Vector3.up);
        }
    }
}
