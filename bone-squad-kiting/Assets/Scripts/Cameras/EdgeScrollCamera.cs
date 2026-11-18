using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Cameras
{
    /// <summary>
    /// Fixed-angle camera. With <see cref="FollowSquad"/> it stays on the squad and leans toward the mouse:
    /// the further the cursor is from the screen center, the further the view shifts that way, but never so
    /// far that the squad's center (the formation's origin) comes closer to a screen edge than
    /// <see cref="KeepMargin"/>. Without it, it is an RTS camera that only slides when the mouse pushes a
    /// screen edge. Either way the look-at point is kept inside <see cref="BoundsMin"/>..<see cref="BoundsMax"/>
    /// and behind closed camera gates.
    /// </summary>
    public class EdgeScrollCamera : MonoBehaviour
    {
        [Tooltip("Shared numbers for every stage. When set, it overrides Scroll Speed, Edge Width and Distance below.")]
        public BoneSquadKiting.Data.GameTuning Tuning;
        public float ScrollSpeed = 14f;   // world units per second
        public float EdgeWidth = 18f;     // pixels from the screen edge that count as "at the edge"
        public float Distance = 34f;      // from the look-at point, along the view direction
        public Vector2 LookAt = new(0f, -0.1f);   // ground point (x, z) at the center of the view

        [Header("Follow the squad, leaning toward the mouse")]
        public bool FollowSquad = true;
        public BoneSquadKiting.Squad.FormationController Squad;   // found in the scene when left empty
        [Range(0f, 1f)] public float LookAhead = 1f;        // 1 = lean until the squad's center reaches the keep margin
        [Range(0f, 0.45f)] public float KeepMargin = 0.14f; // share of the screen the squad's center keeps from every edge
        [Range(0f, 0.9f)] public float DeadZone = 0.12f;    // share of the half screen around the center where the mouse does not lean the view
        public float SmoothTime = 0.22f;                    // seconds to catch up with the mouse

        public Vector2 BoundsMin = new(GameConstants.LimitXMin, GameConstants.LimitZMin); // look-at limits (x, z)
        public Vector2 BoundsMax = new(GameConstants.LimitXMax, GameConstants.LimitZMax);

        /// <summary>Extra world-space offset added on top of the scrolled position (screen shake).</summary>
        public Vector3 ShakeOffset { get; private set; }

        internal float CombatShakeStrength { get; set; }
        readonly List<(float strength, float duration, float remaining)> _presentationShakes = new();

        /// <summary>Lines the view may not be scrolled past while their mini bosses live. Set by StageSetup.</summary>
        [System.NonSerialized] public BoneSquadKiting.Stage.CameraGate[] Gates;

        bool _mouseSeen;
        Camera _camera;
        Vector2 _lean, _leanVelocity, _lastAim;

        /// <summary>How far the view is shifted off the squad's center right now (x, z).</summary>
        public Vector2 Lean => _lean;

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

        void Start()
        {
            // The squad that came with this camera (the same kit or scene object tree) first; any squad otherwise.
            if (Squad == null) Squad = transform.root.GetComponentInChildren<BoneSquadKiting.Squad.FormationController>();
            if (Squad == null) Squad = FindAnyObjectByType<BoneSquadKiting.Squad.FormationController>();
            Apply();
        }

        /// <summary>
        /// Where the mouse is pulling, as (right, up) in -1..1, from a mouse position in viewport units
        /// (0..1 across the screen). Zero inside the dead zone around the center, 1 at the screen edge.
        /// </summary>
        public static Vector2 AimFromViewport(Vector2 viewport, float deadZone)
        {
            var v = new Vector2(Mathf.Clamp(viewport.x * 2f - 1f, -1f, 1f), Mathf.Clamp(viewport.y * 2f - 1f, -1f, 1f));
            return new Vector2(Ease(v.x, deadZone), Ease(v.y, deadZone));
        }

        static float Ease(float v, float deadZone)
        {
            float a = Mathf.Abs(v);
            if (a <= deadZone) return 0f;
            return Mathf.Sign(v) * (a - deadZone) / Mathf.Max(1e-4f, 1f - deadZone);
        }

        /// <summary>
        /// The shift off the squad's center that an aim asks for, on the ground (x, z). It is the shift that
        /// puts the squad's center on the opposite side of the screen from the mouse, at most the keep
        /// margin away from the edge — so the center can never leave the view.
        /// </summary>
        public Vector2 LeanFor(Vector2 aim)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return Vector2.zero;
            float reach = (0.5f - KeepMargin) * LookAhead;
            var centerOnScreen = new Vector2(0.5f - aim.x * reach, 0.5f - aim.y * reach);   // where the squad's center should appear
            if (!Ground(centerOnScreen, out var there) || !Ground(new Vector2(0.5f, 0.5f), out var middle)) return Vector2.zero;
            return middle - there;
        }

        // The ground point (x, z) under a viewport position, for the camera as it stands.
        bool Ground(Vector2 viewport, out Vector2 point)
        {
            var ray = _camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            point = default;
            if (ray.direction.y > -1e-4f) return false;   // looking at the sky: no ground there
            float t = -ray.origin.y / ray.direction.y;
            var hit = ray.origin + ray.direction * t;
            point = new Vector2(hit.x, hit.z);
            return true;
        }

        /// <summary>Stay on a point and lean toward an aim. Public so tests can drive it.</summary>
        public void Follow(Vector2 center, Vector2 aim, float dt)
        {
            var want = LeanFor(aim);
            _lean = SmoothTime > 0f && dt > 0f
                ? Vector2.SmoothDamp(_lean, want, ref _leanVelocity, SmoothTime, Mathf.Infinity, dt)
                : want;
            LookAt = center + _lean;
            Apply();
        }

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
            if (FollowSquad && Squad != null)
            {
                // The aim holds its last value while the cursor is outside the window, so the view does not jump.
                if (_mouseSeen && Application.isFocused && Screen.width > 0 && Screen.height > 0)
                {
                    var m = Mouse.current.position.ReadValue();
                    if (m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
                        _lastAim = AimFromViewport(new Vector2(m.x / Screen.width, m.y / Screen.height), DeadZone);
                }
                Follow(Squad.Anchor, _lastAim, Time.unscaledDeltaTime);
                return;
            }
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
            if (Tuning != null)
            {
                ScrollSpeed = Tuning.CameraScrollSpeed; EdgeWidth = Tuning.CameraEdgeWidth; Distance = Tuning.CameraDistance;
                FollowSquad = Tuning.CameraFollow; LookAhead = Tuning.CameraLookAhead; KeepMargin = Tuning.CameraKeepMargin;
                DeadZone = Tuning.CameraDeadZone; SmoothTime = Tuning.CameraSmoothTime;
            }
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
