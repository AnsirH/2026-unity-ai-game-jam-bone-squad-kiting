using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.UI
{
    // The priest's defeat enables this object. A retry or stage change disables it.
    [DefaultExecutionOrder(150)]
    public sealed class PortalApproachGuide : MonoBehaviour
    {
        public StageRun Run;
        public ParticleSystemRenderer Warp;
        Image _icon;
        Camera _camera;
        ParticleSystem _particles;
        bool _seen;
        int _sceneryMask;
        Vector2 _restPosition;

        void OnEnable()
        {
            _seen = false;
            if (Run == null || Run.Kit == null) return;
            _camera = Run.Kit.Cam.GetComponent<Camera>();
            _particles = Warp != null ? Warp.GetComponent<ParticleSystem>() : null;
            _icon = Run.Kit.GetComponentInChildren<Hud>(true).PortalGuide;
            if (_icon != null) _restPosition = _icon.rectTransform.anchoredPosition;
            _sceneryMask = LayerMask.GetMask("Ground", "Obstacle");
        }

        void OnDisable()
        {
            if (_icon == null) return;
            _icon.enabled = false;
            _icon.rectTransform.anchoredPosition = _restPosition;
        }

        void LateUpdate()
        {
            if (_icon == null || _seen) return;
            var director = Run.Kit.Director;
            bool playing = director.Phase == GamePhase.Play && !director.Frozen && !director.Paused &&
                           _camera != null && _camera.isActiveAndEnabled;
            if (playing) _seen = WarpInView();
            _icon.enabled = playing && !_seen;
            if (!_icon.enabled) return;
            float bob = 5f * (1f - Mathf.Cos(Time.time * (2f * Mathf.PI / 1.15f)));
            _icon.rectTransform.anchoredPosition = _restPosition + new Vector2(-.5f, .866f) * bob;
        }

        bool WarpInView()
        {
            if (_particles == null || _particles.particleCount == 0 || Warp == null || !Warp.enabled || !Warp.gameObject.activeInHierarchy || Warp.forceRenderingOff ||
                Warp.mesh == null || (_camera.cullingMask & (1 << Warp.gameObject.layer)) == 0) return false;
            // The particle renderer has very large culling bounds. Sample the visible portal mesh itself.
            var bounds = Warp.mesh.bounds;
            var t = Warp.transform;
            if (GameplayVisibility.PointInView(_camera, t.TransformPoint(bounds.center), _sceneryMask)) return true;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var rim = bounds.center + new Vector3(Mathf.Cos(angle) * bounds.extents.x,
                    Mathf.Sin(angle) * bounds.extents.y, 0f);
                if (GameplayVisibility.PointInView(_camera, t.TransformPoint(rim), _sceneryMask)) return true;
            }
            return GameplayVisibility.PointInView(_camera,
                t.TransformPoint(bounds.center + Vector3.forward * bounds.extents.z), _sceneryMask);
        }
    }
}
