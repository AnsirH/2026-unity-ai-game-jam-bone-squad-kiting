using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.UI
{
    // Read the gameplay camera after it follows the squad and the knight's animation has been posed.
    [DefaultExecutionOrder(150), RequireComponent(typeof(Image))]
    public sealed class BossApproachGuide : MonoBehaviour
    {
        Hud _hud;
        Image _icon;
        Camera _camera;
        FrostKnightFight _fight;
        bool _seen;
        int _sceneryMask;

        void Awake()
        {
            _hud = GetComponentInParent<Hud>();
            _icon = GetComponent<Image>();
            _icon.enabled = false;
            _sceneryMask = LayerMask.GetMask("Ground", "Obstacle");
        }

        void LateUpdate()
        {
            var director = _hud.Director;
            var fight = director != null ? director.Fight : null;
            if (_fight != fight)
            {
                _fight = fight;
                _seen = false;
                _camera = fight != null && director.Stage != null && director.Stage.Cam != null
                    ? director.Stage.Cam.GetComponent<Camera>() : null;
            }

            bool guiding = fight != null && !_seen && director.Phase == GamePhase.Play && !director.Paused &&
                !director.Frozen && _camera != null && _camera.isActiveAndEnabled && director.KnightModel != null;
            if (guiding && fight.State != FrostKnightState.Hidden)
                _seen = director.KnightModel.BodyInView(_camera, _sceneryMask);
            _icon.enabled = guiding && !_seen;
            if (!_icon.enabled) return;

            // Keep both the cue and an incoming pattern readable if the player waits at the door.
            float top = 0f;
            if (_hud.WarningPanel.activeInHierarchy) top = PanelBottom(_hud.WarningPanel);
            else if (_hud.BossBar.activeInHierarchy) top = PanelBottom(_hud.BossBar);
            float bob = 5f * (1f - Mathf.Cos(director.Elapsed * (2f * Mathf.PI / 1.15f)));
            _icon.rectTransform.anchoredPosition = new Vector2(0f, top - 19f + bob);
        }

        static float PanelBottom(GameObject panel)
        {
            var rect = (RectTransform)panel.transform;
            return rect.anchoredPosition.y + rect.rect.yMin;
        }
    }
}
