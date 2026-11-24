using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Fx
{
    /// <summary>
    /// Temporary game-feel effects: screen shake, a red screen flash, hit particles on struck units and
    /// an expanding ring where a pattern went off. Every piece is optional; unassigned ones are skipped.
    /// </summary>
    public class JuiceFx : MonoBehaviour
    {
        const int RingPoints = 64;

        public GameDirector Director;
        public EdgeScrollCamera Cam;
        public Image FlashImage;            // full-screen UI image, alpha driven here
        public ParticleSystem HitParticles;
        public LineRenderer Ring;

        [Header("Tuning")]
        public float ShakeOnHit = 0.35f;    // world units of camera jitter when a pattern hits units
        public float ShakeOnPhaseTwo = 0.6f;
        public float ShakeDecay = 7f;       // higher = settles faster
        public float FlashOnHit = 0.35f;    // screen flash alpha
        public float FlashDecay = 2.7f;     // alpha per second
        public int ParticlesPerHit = 10;
        public float RingSeconds = 0.5f;
        public float RingMaxRadius = 8f;

        public float Shake { get; private set; }
        public float Flash { get; private set; }

        Vector3 _lastCenter;
        float _ringT = -1f;

        void OnEnable()
        {
            if (Director == null) return;
            Director.DefaultRingRequested += StartRing;
            Director.PatternResolved += OnResolved;
            Director.PhaseTwoStarted += OnPhaseTwo;
            Director.ReviveCompleted += OnRevived;
            Director.CutsceneStarted += OnCutscene;
        }

        void OnDisable()
        {
            if (Cam != null) Cam.CombatShakeStrength = 0f;
            if (Director == null) return;
            Director.DefaultRingRequested -= StartRing;
            Director.PatternResolved -= OnResolved;
            Director.PhaseTwoStarted -= OnPhaseTwo;
            Director.ReviveCompleted -= OnRevived;
            Director.CutsceneStarted -= OnCutscene;
        }

        // Mini boss intro / death: send the camera over and drop the effect prefab there, if one is set.
        void OnCutscene(Vector2 focus, float seconds, string title, GameObject fxPrefab)
        {
            if (Cam != null) Cam.FocusOn(focus, seconds);
            if (fxPrefab == null) return;
            var fx = Instantiate(fxPrefab, new Vector3(focus.x, 0f, focus.y), Quaternion.identity);
            Destroy(fx, seconds + 3f);
        }

        // A burst where the unit comes back, so a finished revive can be seen without a gauge.
        void OnRevived(BoneSquadKiting.Squad.SquadUnit reviver, BoneSquadKiting.Squad.SquadUnit revived)
        {
            if (HitParticles == null) return;
            HitParticles.Emit(new ParticleSystem.EmitParams { position = new Vector3(revived.X, 0.4f, revived.Z), startColor = new Color(0.4f, 1f, 0.7f) }, ParticlesPerHit * 2);
        }

        void StartRing() => _ringT = 0f;

        void OnResolved(int hit, int lost)
        {
            if (hit == 0) return;
            Shake = Mathf.Max(Shake, ShakeOnHit);
            Flash = Mathf.Max(Flash, FlashOnHit + (lost > 0 ? 0.15f : 0f));
            if (HitParticles == null) return;
            foreach (var u in Director.Squad.Units)
            {
                if (u.Flash < 0.99f) continue; // Flash is set to 1 on the units this pattern just hit
                HitParticles.Emit(new ParticleSystem.EmitParams { position = new Vector3(u.X, 0.4f, u.Z) }, ParticlesPerHit);
            }
        }

        void OnPhaseTwo()
        {
            Shake = Mathf.Max(Shake, ShakeOnPhaseTwo);
            Flash = Mathf.Max(Flash, 0.25f);
        }

        void Update() { if (Time.deltaTime > 0f) Tick(Time.deltaTime); }

        /// <summary>Advance the effects. Public so tests can drive time.</summary>
        public void Tick(float dt)
        {
            if (Director != null && Director.NextTelegraph is BossAttackSystem.Active a)
                _lastCenter = new Vector3(a.Cx, 0.05f, a.Cz);

            Shake *= Mathf.Exp(-ShakeDecay * dt);
            if (Shake < 0.002f) Shake = 0f;
            if (Cam != null) Cam.CombatShakeStrength = Shake;

            Flash = Mathf.Max(0f, Flash - FlashDecay * dt);
            if (FlashImage != null)
            {
                var c = FlashImage.color; c.a = Flash; FlashImage.color = c;
            }

            if (Ring == null) return;
            if (_ringT < 0f) { Ring.enabled = false; return; }
            _ringT += dt;
            float k = _ringT / RingSeconds;
            if (k >= 1f) { _ringT = -1f; Ring.enabled = false; return; }
            Ring.enabled = true;
            if (Ring.positionCount != RingPoints) Ring.positionCount = RingPoints;
            float radius = RingMaxRadius * k;
            for (int i = 0; i < RingPoints; i++)
            {
                float ang = Mathf.PI * 2f * i / RingPoints;
                Ring.SetPosition(i, _lastCenter + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
            }
            var color = new Color(1f, 0.5f, 0.3f, 1f - k);
            Ring.startColor = color; Ring.endColor = color;
        }
    }
}
