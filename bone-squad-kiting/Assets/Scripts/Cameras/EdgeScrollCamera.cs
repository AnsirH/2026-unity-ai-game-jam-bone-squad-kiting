using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Cameras
{
    /// <summary>
    /// RTS-style camera: fixed angle, never follows the squad. Pushing the mouse against a screen edge
    /// slides the view in that direction, parallel to the ground. The look-at point is kept inside
    /// <see cref="BoundsMin"/>..<see cref="BoundsMax"/> so the view cannot wander off the arena.
    /// </summary>
    public class EdgeScrollCamera : MonoBehaviour
    {
        [Tooltip("Shared numbers for every stage. When set, it overrides Scroll Speed, Edge Width and Distance below.")]
        public BoneSquadKiting.Data.GameTuning Tuning;
        public float ScrollSpeed = 14f;   // world units per second
        public float EdgeWidth = 18f;     // pixels from the screen edge that count as "at the edge"
        public float Distance = 34f;      // from the look-at point, along the view direction
        public Vector2 LookAt = new(0f, -0.1f);   // ground point (x, z) at the center of the view
        public Vector2 BoundsMin = new(GameConstants.LimitXMin, GameConstants.LimitZMin); // look-at limits (x, z)
        public Vector2 BoundsMax = new(GameConstants.LimitXMax, GameConstants.LimitZMax);

        /// <summary>Extra world-space offset added on top of the scrolled position (screen shake).</summary>
        public Vector3 ShakeOffset { get; private set; }

        internal float CombatShakeStrength { get; set; }
        readonly List<(float strength, float duration, float remaining)> _presentationShakes = new();

        /// <summary>Lines the view may not be scrolled past while their mini bosses live. Set by StageSetup.</summary>
        [System.NonSerialized] public BoneSquadKiting.Stage.CameraGate[] Gates;

        bool _mouseSeen;

        // Temporary look at a point (mini boss intro / death): go there, hold, come back.
        Vector2 _focusPoint, _focusReturn;
        float _focusDuration, _focusElapsed = -1f;

        public bool Focusing => _focusElapsed >= 0f;

        /// <summary>Glide to a point, hold, and return to where the view was, over <paramref name="seconds"/>.</summary>
        public void FocusOn(Vector2 point, float seconds)
        {
            if (seconds <= 0f) return;
            if (!Focusing) _focusReturn = LookAt;
            _focusPoint = point; _focusDuration = seconds; _focusElapsed = 0f;
        }

        /// <summary>Advance a focus move. Public so tests can drive time. Returns true while one is running.</summary>
        public bool TickFocus(float dt)
        {
            if (!Focusing) return false;
            _focusElapsed += dt;
            float t = Mathf.Clamp01(_focusElapsed / _focusDuration);
            const float edge = 0.3f;   // share of the time spent travelling each way
            float w = t < edge ? Mathf.SmoothStep(0f, 1f, t / edge)
                    : t > 1f - edge ? Mathf.SmoothStep(1f, 0f, (t - (1f - edge)) / edge) : 1f;
            LookAt = Vector2.Lerp(_focusReturn, _focusPoint, w);
            if (t >= 1f) { LookAt = _focusReturn; _focusElapsed = -1f; }
            Apply();
            return true;
        }

        /// <summary>
        /// Which way to scroll for a mouse position, as (right, up) in -1..1. Zero when the cursor is not
        /// at an edge or is outside the view (so dragging the mouse out of the Game window does nothing).
        /// </summary>
        public static Vector2 ScrollDirection(Vector2 mouse, Vector2 screenSize, float edgeWidth)
        {
            if (mouse.x < 0 || mouse.y < 0 || mouse.x > screenSize.x || mouse.y > screenSize.y) return Vector2.zero;
            float x = mouse.x <= edgeWidth ? -1f : mouse.x >= screenSize.x - edgeWidth ? 1f : 0f;
            float y = mouse.y <= edgeWidth ? -1f : mouse.y >= screenSize.y - edgeWidth ? 1f : 0f;
            return new Vector2(x, y);
        }

        void Start() => Apply();

        public void PlayPresentationShake(float strength, float seconds)
        {
            if (strength <= 0f || seconds <= 0f) return;
            for (int i = 0; i < _presentationShakes.Count; i++)
            {
                var shake = _presentationShakes[i];
                if (shake.strength != strength || shake.duration != seconds) continue;
                _presentationShakes[i] = (strength, seconds, seconds);
                return;
            }
            _presentationShakes.Add((strength, seconds, seconds));
        }

        public void ClearPresentationShake()
        {
            _presentationShakes.Clear();
            if (CombatShakeStrength <= 0f && ShakeOffset.sqrMagnitude > 0f)
            {
                ShakeOffset = Vector3.zero;
                Apply();
            }
        }

        public float TickShake(float dt)
        {
            float strongest = CombatShakeStrength;
            for (int i = _presentationShakes.Count - 1; i >= 0; i--)
            {
                var shake = _presentationShakes[i];
                shake.remaining -= dt;
                if (shake.remaining <= 0f)
                {
                    _presentationShakes.RemoveAt(i);
                    continue;
                }
                _presentationShakes[i] = shake;
                strongest = Mathf.Max(strongest, shake.strength * (shake.remaining / shake.duration));
            }
            ShakeOffset = strongest > 0f ? Random.insideUnitSphere * strongest : Vector3.zero;
            return strongest;
        }

        void OnDisable()
        {
            CombatShakeStrength = 0f;
            ClearPresentationShake();
        }

        void LateUpdate()
        {
            TickShake(Time.unscaledDeltaTime);
            if (TickFocus(Time.unscaledDeltaTime)) return;   // no edge scrolling while the camera is being directed
            // Until the mouse has reported a position it reads (0, 0), which would look like the corner.
            if (Mouse.current != null && Mouse.current.position.ReadValue() != Vector2.zero) _mouseSeen = true;
            var dir = _mouseSeen && Application.isFocused
                ? ScrollDirection(Mouse.current.position.ReadValue(), new Vector2(Screen.width, Screen.height), EdgeWidth)
                : Vector2.zero;
            Scroll(dir, Time.unscaledDeltaTime); // also runs when idle so the shake offset is applied
        }

        /// <summary>Slide the view by a screen-space direction. Public so tests can drive it.</summary>
        public void Scroll(Vector2 screenDir, float dt)
        {
            if (screenDir != Vector2.zero)
            {
                // Screen right / up mapped onto the ground plane, so the move stays parallel to the floor.
                Vector3 right = Flat(transform.right), up = Flat(transform.forward);
                Vector3 move = (right * screenDir.x + up * screenDir.y) * (ScrollSpeed * dt);
                LookAt += new Vector2(move.x, move.z);
            }
            Apply();
        }

        /// <summary>Clamp the look-at point and place the camera for it.</summary>
        public void Apply()
        {
            if (Tuning != null) { ScrollSpeed = Tuning.CameraScrollSpeed; EdgeWidth = Tuning.CameraEdgeWidth; Distance = Tuning.CameraDistance; }
            LookAt = new Vector2(Mathf.Clamp(LookAt.x, BoundsMin.x, BoundsMax.x), Mathf.Clamp(LookAt.y, BoundsMin.y, BoundsMax.y));
            if (Gates != null && !Focusing)   // a directed look (mini boss intro) may go past a closed gate
                foreach (var gate in Gates) if (gate != null) LookAt = gate.Clamp(LookAt);
            transform.position = new Vector3(LookAt.x, 0f, LookAt.y) - transform.forward * Distance + ShakeOffset;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }
}
