using UnityEngine;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.Bosses
{
    public sealed class CatapultThrow : MonoBehaviour
    {
        public Animator Arm;
        public AnimationClip FireClip;
        public Transform Socket;
        public MeshRenderer Stone;
        public TrailRenderer Trail;
        public CatapultImpact ImpactPrefab;
        [Min(0f)] public float ReleaseSeconds = 1.25f;
        float _releaseClipTime;

        static readonly int Fire = Animator.StringToHash("Base Layer.Fire");
        static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        BossAttackSystem _attacks;
        Vector3 _launch, _control, _target;
        Quaternion _releaseRotation;
        bool _flying, _casting;
        Vector3 _loadedPosition;
        Quaternion _loadedRotation;

        void Awake()
        {
            foreach (var marker in FireClip.events)
                if (marker.functionName == nameof(Audio.AnimationSound.PlayClipSound)
                    && marker.stringParameter == "enemy.catapult.launch")
                {
                    _releaseClipTime = marker.time;
                    break;
                }
            _loadedPosition = Stone.transform.localPosition;
            _loadedRotation = Stone.transform.localRotation;
        }

        void Bind(BossAttackSystem attacks)
        {
            if (_attacks != null) _attacks.OnImpact -= Land;
            _attacks = attacks;
            attacks.OnImpact += Land;
            Arm.speed = 0f;
            Reload();
        }

        void OnDisable()
        {
            if (_attacks != null) _attacks.OnImpact -= Land;
            _attacks = null;
        }

        public void Draw(BossAttackSystem attacks)
        {
            if (_attacks != attacks) Bind(attacks);
            if (attacks.Current is not BossAttackSystem.Active cast)
            {
                if (_casting) Reload();
                return;
            }

            // Drive the arm and the stone from the same simulation clock, including pauses and cutscenes.
            _casting = true;
            float release = Mathf.Min(ReleaseSeconds, cast.Pattern.Time * 0.6f);
            float clipTime = release > 0f && cast.Elapsed < release
                ? _releaseClipTime * cast.Elapsed / release
                : Mathf.Lerp(_releaseClipTime, FireClip.length, Mathf.InverseLerp(release, cast.Pattern.Time, cast.Elapsed));
            if (!_flying && cast.Elapsed >= release) Release(cast);
            PoseArm(clipTime);
            if (!_flying) return;

            float t = Mathf.InverseLerp(release, cast.Pattern.Time, cast.Elapsed);
            Stone.transform.position = Vector3.Lerp(Vector3.Lerp(_launch, _control, t), Vector3.Lerp(_control, _target, t), t);
            Stone.transform.rotation = Quaternion.AngleAxis(t * 300f, transform.forward) * _releaseRotation;
        }

        void PoseArm(float clipTime)
        {
            // Seeking for a pose must not replay a marker on every frame of a paused cast.
            bool events = Arm.fireEvents;
            Arm.fireEvents = false;
            Arm.Play(Fire, 0, Mathf.Clamp(clipTime / FireClip.length, 0f, 0.999f));
            Arm.Update(0f);
            Arm.fireEvents = events;
        }

        void Release(BossAttackSystem.Active cast)
        {
            // Sample the authored swing at release even when the current frame has stepped past it.
            float beforeRelease = Mathf.Max(0f, _releaseClipTime - 0.01f);
            PoseArm(beforeRelease);
            Vector3 before = Stone.transform.position;
            // Advance across the actual Animation Event exactly once. Unity dispatches
            // the timber snap on the arm Animator as the stone leaves its socket.
            Arm.speed = 1f;
            Arm.Update(_releaseClipTime - beforeRelease + 0.0001f);
            Arm.speed = 0f;
            _launch = Stone.transform.position;
            _releaseRotation = Stone.transform.rotation;
            _target = new Vector3(cast.Cx, GroundHeight.Y(cast.Cx, cast.Cz), cast.Cz);
            Vector3 swing = _launch - before;
            Vector3 direction = swing.sqrMagnitude > 1e-10f ? swing.normalized : -transform.right;
            // The first segment of this quadratic sets its departure direction. The last point fixes impact.
            _control = _launch + direction * (0.5f * Vector3.Distance(_launch, _target));
            Stone.transform.SetParent(transform, true);
            Trail.Clear();
            Trail.enabled = true;
            Trail.emitting = true;
            _flying = true;
            Audio.SoundManager.Current?.PlaySfx("enemy.catapult.flight");
        }

        void Land(BossAttackSystem.Active cast)
        {
            var ground = new Vector3(cast.Cx, GroundHeight.Y(cast.Cx, cast.Cz), cast.Cz);
            Stone.transform.position = ground;
            Trail.emitting = false;
            Instantiate(ImpactPrefab, ground, Quaternion.identity).Show(cast.Shape.RMax);
            Audio.SoundManager.Current?.PlaySfx("enemy.catapult.impact");
        }

        void Reload()
        {
            Trail.emitting = false;
            Trail.Clear();
            Trail.enabled = false;
            Stone.transform.SetParent(Socket, true);
            Stone.transform.SetLocalPositionAndRotation(_loadedPosition, _loadedRotation);
            Arm.Play(Idle, 0, 0f);
            Arm.Update(0f);
            _casting = _flying = false;
        }
    }
}
