using UnityEngine;

namespace BoneSquadKiting.Cameras
{
    internal static class GameplayVisibility
    {
        public static bool PointInView(Camera camera, Vector3 point, int sceneryMask)
        {
            var p = camera.WorldToViewportPoint(point);
            if (p.z < camera.nearClipPlane || p.z > camera.farClipPlane ||
                p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f) return false;
            return !Physics.Linecast(camera.ViewportPointToRay(p).origin, point, sceneryMask, QueryTriggerInteraction.Ignore);
        }
    }
}
