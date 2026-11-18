using UnityEngine;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Cameras
{
    /// <summary>
    /// Keeps a fixed-angle camera looking at the squad's formation anchor, easing toward it so small
    /// moves do not jerk the view. The camera's rotation is whatever it was given in the scene.
    /// </summary>
    public class QuarterViewCamera : MonoBehaviour
    {
        public FormationController Squad;
        public float Distance = 34f;      // from the anchor, along the view direction
        public float SmoothTime = 0.35f;  // seconds to roughly catch up; 0 = locked to the anchor

        Vector3 _velocity;

        /// <summary>Where the camera wants to be for the anchor's current position.</summary>
        public Vector3 DesiredPosition
        {
            get
            {
                var anchor = new Vector3(Squad.Anchor.x, 0f, Squad.Anchor.y);
                return anchor - transform.forward * Distance;
            }
        }

        /// <summary>Jump straight to the desired position (scene start, restart).</summary>
        public void Snap()
        {
            if (Squad == null) return;
            transform.position = DesiredPosition;
            _velocity = Vector3.zero;
        }

        void Start() => Snap();

        void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>Ease toward the desired position. Public so tests can drive time.</summary>
        public void Tick(float dt)
        {
            if (Squad == null || dt <= 0f) return;
            transform.position = SmoothTime <= 0f
                ? DesiredPosition
                : Vector3.SmoothDamp(transform.position, DesiredPosition, ref _velocity, SmoothTime, Mathf.Infinity, dt);
        }
    }
}
