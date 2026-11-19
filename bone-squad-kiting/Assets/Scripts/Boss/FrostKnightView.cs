using UnityEngine;
using BoneSquadKiting.Audio;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Game;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Shows the Frost Knight fight on the boss model: where it stands, which motion it plays, the attack
    /// effects, sounds and camera shake. The model's own scripts make it appear and fade out with its motions
    /// (it dissolves at the end of StandUp and ChargedSlash), so "out of sight" is just one of those states.
    /// Put on the FrostKnight model under the stage's BossArena.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class FrostKnightView : MonoBehaviour
    {
        public GameDirector Director;
        public BossArena Arena;
        public Animator Animator;
        [Tooltip("Seconds one motion takes to blend into the next.")]
        public float Blend = 0.25f;
        [Tooltip("Length of the StandUp motion: its end pose is where the model fades out.")]
        public float StandUpLength = 2.96f;
        public Color HitFlashColor = new(4f, 2.5f, 2f, 1f);

        [Header("Effects")]
        [Tooltip("Played on the circle attack's centre, this many seconds before the sword lands (the prefab's own time to impact).")]
        public GameObject CircleEffect;
        [Tooltip("One long-trail slash per warned lane, played together at impact.")]
        public GameObject ParallelEffect;
        public BoneSquadKiting.Fx.SovereignVolley ChargeEffect;
        [Tooltip("Extra height of slash marks above the combat floor, to clear the raised ritual medallion.")]
        [Min(0f)] public float SlashMarkLift = .12f;
        public float CircleEffectLead = 1.51f;
        public float CircleEffectScale = 1f;
        [Tooltip("Camera shake when an attack lands: ordinary attacks, and the lethal slam.")]
        public float HitShake = 0.3f;
        public float LethalShake = 0.6f;
        public float LandingShake = .42f;
        public float DeathShake = .85f;
        public float ShakeSeconds = 0.35f;

        [Header("Sounds (ids in the sound database; an empty or unknown id plays nothing)")]
        [Tooltip("The greatsword appears.")]
        public string CastSound = "boss.greatsword.summon";
        [Tooltip("The falling greatsword strikes the ground.")]
        public string HitSound = "boss.greatsword.impact";
        [Tooltip("The circle attack's sword starts to fall.")]
        public string CircleFallSound = "boss.greatsword.fall";
        [Tooltip("The charged slash is swung, and its wave flies across the room.")]
        public string ChargeSwingSound = "boss.spinningblade.swing";
        public string ChargeFlightSound = "boss.spinningblade.flight";
        [Tooltip("The squad walks in and the boss raises its head.")]
        public string WakeSound = "boss.frostknight.wake";
        public string StunSound = "boss.frostknight.stun";
        public string RiseSound = "boss.frostknight.rise";
        [Tooltip("Fading out of sight, and showing up for an attack.")]
        public string VanishSound = "boss.frostknight.vanish";
        public string AppearSound = "boss.frostknight.appear";
        public string GroggySound = "boss.frostknight.groggy";
        public string DeathSound = "boss.frostknight.death";
        [Tooltip("The lethal slam: its warning starts, and it lands.")]
        public string LethalWarnSound = "boss.frostknight.lethal.warn";
        public string LethalHitSound = "boss.frostknight.lethal.hit";

        FrostKnightFight _fight;
        int _serial = -1;
        bool _circleShown;
        float _previousRemaining;
        Renderer _body;
        FrostKnightPlantFlames _plantFire;
        MaterialPropertyBlock _block;
        static readonly int BodyHitTint = Shader.PropertyToID("_BodyHitTint");
        static readonly int RimStrength = Shader.PropertyToID("_FresnelIntensity");
        static readonly int DieState = UnityEngine.Animator.StringToHash("Die");
        const float GroggyDropSeconds = .38f;
        float _normalRim;
        FrostKnightDeathDissolve _dissolve;
        bool _landed, _deathPeak;
        EnemyRef _ref;
        Collider _collider;
        BoneSquadKiting.Cameras.EdgeScrollCamera _cam;
        CapsuleCollider _pickCapsule;
        Transform _chargeTorso;
        Vector3 _restPickCenter;
        float _seatY;

        void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
            if (Arena == null) Arena = GetComponentInParent<BossArena>();
            _body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            _block = new MaterialPropertyBlock();
            if (_body != null)
            {
                _normalRim = _body.sharedMaterial.GetFloat(RimStrength);
                _dissolve = _body.GetComponent<FrostKnightDeathDissolve>();
            }
            _plantFire = GetComponentInChildren<FrostKnightPlantFlames>(true);
            _ref = GetComponent<EnemyRef>();
            _collider = GetComponent<Collider>();
            _pickCapsule = _collider as CapsuleCollider;
            if (_pickCapsule != null)
            {
                _restPickCenter = _pickCapsule.center;
                if (Animator != null)
                    _chargeTorso = System.Array.Find(Animator.GetComponentsInChildren<Transform>(true),
                        bone => bone.name == "mixamorig:Spine1");
            }
            _seatY = transform.position.y;
        }

        void Start()
        {
            if (Director == null) Director = FindFirstObjectByType<GameDirector>();
            Place(null);
            if (Animator != null && Animator.isActiveAndEnabled) Animator.Play("HeadBowed", 0, 0f);
        }

        void Update()
        {
            var fight = Director != null ? Director.Fight : null;
            if (fight != _fight) Bind(fight);
            if (fight == null) return;

            Place(fight);
            if (_serial != fight.AnimSerial) PlayMotion(fight);
            else if (fight.State == FrostKnightState.Charging && Animator != null) Animator.speed = fight.AnimSpeed;
            if (_collider != null) _collider.enabled = fight.Targetable;
            UpdateArmor(fight);

            if (fight.Casting && fight.Telegraphs.Count > 0)
                PlayCastSounds(fight.Attack, fight.Telegraphs[0].Remaining);

            // The falling sword has its own time to impact: start it that long before the circle goes off.
            if (!_circleShown && CircleEffect != null && fight.Casting && fight.Attack == FrostKnightAttack.Circle
                && fight.Telegraphs.Count > 0 && fight.Telegraphs[0].Remaining <= CircleEffectLead)
            {
                _circleShown = true;
                foreach (var zone in fight.Telegraphs) PlayCircle(zone);
            }
        }

        void PlayCircle(BossAttackSystem.Active cast)
        {
            var at = new Vector3(cast.Cx, BoneSquadKiting.Stage.GroundHeight.Y(cast.Cx, cast.Cz), cast.Cz);
            var fx = Instantiate(CircleEffect, at, Quaternion.Euler(0f, cast.Angle, 0f));
            // The combat telegraph already draws the disc. Fit the ground wave to its radius.
            var disc = fx.transform.Find("TelegraphDisc/Disc");
            if (disc != null && cast.Shape.RMax > 0f)
            {
                float groundScale = cast.Shape.RMax / (disc.localScale.x * .5f);
                disc.gameObject.SetActive(false);
                fx.transform.localScale = Vector3.Scale(fx.transform.localScale, new Vector3(groundScale, 1f, groundScale));
                var sword = fx.transform.Find("Frost Greatsword");
                if (sword != null)
                    sword.localScale = Vector3.Scale(sword.localScale, new Vector3(1f / groundScale, 1f, 1f / groundScale)) * CircleEffectScale;
            }
            else fx.transform.localScale *= CircleEffectScale;
            Destroy(fx, 6f);
        }

        void UpdateArmor(FrostKnightFight fight)
        {
            if (_body == null) return;
            bool groggy = fight != null && fight.State == FrostKnightState.Groggy;
            float pulse = groggy ? .5f - .5f * Mathf.Cos(fight.StateTime * 2f * Mathf.PI / .8f) : 0f;
            var tint = HitFlashColor;
            tint.a = fight == null ? 0f : .30f * Mathf.Clamp01(fight.Boss.Flash / .8f);
            _body.GetPropertyBlock(_block);
            _block.SetFloat(RimStrength, _normalRim + pulse * 2.5f);
            _block.SetColor(BodyHitTint, tint);
            _body.SetPropertyBlock(_block);
        }

        void LateUpdate()
        {
            if (_fight != null) UpdateImpactShake();
            if (_pickCapsule == null || _chargeTorso == null) return;
            // Read the animated pose after the Animator has moved the airborne knight.
            _pickCapsule.center = _fight != null && _fight.State == FrostKnightState.Charging && _fight.Casting
                ? transform.InverseTransformPoint(_chargeTorso.position) : _restPickCenter;
        }

        // A new match (or none): forget the old fight, sit on the throne with the head bowed.
        void Bind(FrostKnightFight fight)
        {
            if (_fight != null)
            {
                _fight.OnCast -= OnCast; _fight.OnHit -= OnHit; _fight.OnState -= OnState;
                if (_fight.Attack == FrostKnightAttack.Circle) SoundManager.Current?.StopSfx(CircleFallSound);
            }
            if (_cam != null) _cam.BossDeathRumble = 0f;
            _cam = fight != null && Director != null && Director.Stage != null ? Director.Stage.Cam : null;
            _fight = fight;
            _landed = _deathPeak = false;
            if (fight == null) UpdateArmor(null);
            if (_plantFire != null) _plantFire.enabled = fight == null;
            _serial = -1;
            _circleShown = false;
            _previousRemaining = float.PositiveInfinity;
            if (_ref != null) _ref.Enemy = fight?.Boss;
            if (fight == null) { Place(null); if (Animator != null && Animator.isActiveAndEnabled) { Animator.speed = 1f; Animator.Play("HeadBowed", 0, 0f); } return; }
            fight.OnCast += OnCast; fight.OnHit += OnHit; fight.OnState += OnState;
            // The director ticks before this view, including on the first frame in the room.
            if (fight.State == FrostKnightState.Throne && fight.StateTime == 0f) Sfx(WakeSound);
        }

        void OnDisable() { if (_fight != null) Bind(null); }

        static void Sfx(string id) { if (!string.IsNullOrEmpty(id)) SoundManager.Current?.PlaySfx(id); }

        void OnState(FrostKnightState state)
        {
            _landed = _deathPeak = false;
            if (_cam != null) _cam.BossDeathRumble = 0f;
            switch (state)
            {
                case FrostKnightState.Throne: Sfx(WakeSound); break;
                case FrostKnightState.Stunned: Sfx(StunSound); break;
                case FrostKnightState.Rising: Sfx(RiseSound); break;
                case FrostKnightState.Charging: Sfx(AppearSound); break;
                case FrostKnightState.Groggy: Sfx(GroggySound); break;
                case FrostKnightState.Vanishing: Sfx(VanishSound); break;
                case FrostKnightState.Dying:
                    if (_fight.Attack == FrostKnightAttack.Circle) SoundManager.Current?.StopSfx(CircleFallSound);
                    Sfx(DeathSound);
                    break;
            }
        }

        // Seated states sit at the throne seat's height; on the floor it stands on the ground.
        void Place(FrostKnightFight fight)
        {
            if (fight == null)
            {
                if (Arena != null && Arena.Seat != null) transform.SetPositionAndRotation(Arena.Seat.position, Arena.Seat.rotation);
                return;
            }
            var p = fight.Boss.Position;
            bool seated = fight.State <= FrostKnightState.Rising;
            float y = seated ? (Arena != null && Arena.Seat != null ? Arena.Seat.position.y : _seatY) : BoneSquadKiting.Stage.GroundHeight.Y(p.x, p.y);
            if (fight.State == FrostKnightState.Groggy)
            {
                float t = Mathf.Clamp01(fight.StateTime / GroggyDropSeconds);
                y += 2.5f * (1f - t * t);
            }
            transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, fight.Boss.Facing, 0f));
        }

        void PlayMotion(FrostKnightFight fight)
        {
            _serial = fight.AnimSerial;
            if (fight.Anim == "SwordPlant") Sfx(AppearSound);            // the lethal slam: it shows up just before it strikes
            if (Animator == null) return;
            Animator.speed = Mathf.Max(0.05f, fight.AnimSpeed);
            // Blend times are in real seconds, so they do not stretch with the motion's speed.
            float blend = Blend * Animator.speed;
            if (fight.AnimAtEnd) Animator.CrossFadeInFixedTime(fight.Anim, blend, 0, StandUpLength);
            else Animator.CrossFadeInFixedTime(fight.Anim, blend, 0, 0f);
        }

        void OnCast(FrostKnightAttack attack, BossAttackSystem.Active cast)
        {
            _circleShown = false;
            _previousRemaining = float.PositiveInfinity;
            if (attack >= FrostKnightAttack.LethalLeft) Sfx(LethalWarnSound);
            else if (attack != FrostKnightAttack.Circle) Sfx(CastSound);
            PlayCastSounds(attack, cast.Remaining);
        }

        void PlayCastSounds(FrostKnightAttack attack, float remaining)
        {
            // The plunge reveals its blade at 1.15 s, drops at 1.39 s and lands at 1.51 s.
            if (attack == FrostKnightAttack.Circle)
            {
                if (Crossed(CircleEffectLead - 1.15f)) Sfx(CastSound);
                if (Crossed(CircleEffectLead - 1.39f)) Sfx(CircleFallSound);
            }
            else if (attack == FrostKnightAttack.Charge)
            {
                // The blade accelerates at 6.75 s in ChargedSlash; the wave leaves at ChargeHitAt.
                if (Crossed((FrostKnightFight.ChargeHitAt - 6.75f) / _fight.AnimSpeed)) Sfx(ChargeSwingSound);
            }
            _previousRemaining = remaining;

            bool Crossed(float lead) => _previousRemaining > lead && remaining <= lead;
        }

        void PlayParallel(BossAttackSystem.Active cast)
        {
            var shape = cast.Shape;
            var rotation = Quaternion.Euler(0f, cast.Angle, 0f);
            float laneOffset = (shape.Outer + shape.Inner) * .5f;
            // The prefab travels along local +Z over a 3 by 10 metre strip.
            var scale = new Vector3((shape.Outer - shape.Inner) / 3f, 1f, 2f * shape.HalfZ / 10f);
            for (int side = -1; side <= 1; side += 2)
            {
                var start = new Vector3(cast.Cx, 0f, cast.Cz) + rotation * new Vector3(side * laneOffset, 0f, shape.HalfZ);
                start.y = BoneSquadKiting.Stage.GroundHeight.Y(start.x, start.z) + .05f;
                var fx = Instantiate(ParallelEffect, start, rotation * Quaternion.Euler(0f, 180f, 0f));
                fx.transform.localScale = scale;
                fx.AddComponent<BoneSquadKiting.Fx.SovereignVolley>().LiftGroundMarks(SlashMarkLift);
            }
        }

        void OnHit(FrostKnightAttack attack, BossAttackSystem.Active cast, int hit, int lost)
        {
            if (attack == FrostKnightAttack.Parallel && ParallelEffect != null)
                foreach (var zone in _fight.Telegraphs) PlayParallel(zone);
            bool lethal = attack >= FrostKnightAttack.LethalLeft;
            if (attack == FrostKnightAttack.Circle)
            {
                SoundManager.Current?.StopSfx(CircleFallSound);
                Sfx(HitSound);
            }
            else if (lethal)
            {
                Sfx(LethalHitSound);
                if (_plantFire != null)
                {
                    float floor = BoneSquadKiting.Stage.GroundHeight.Y(cast.Cx, cast.Cz);
                    _plantFire.BurnArea(new Vector3(cast.Cx, floor, cast.Cz), cast.Angle,
                        new Vector2(2f * cast.Shape.Outer, 2f * cast.Shape.HalfZ), Arena.transform);
                }
            }
            else Sfx(ChargeFlightSound); // one release for the whole volley, even on a miss
            if (attack == FrostKnightAttack.Charge)
            {
                if (ChargeEffect != null)
                {
                    var at = new Vector3(cast.Cx, BoneSquadKiting.Stage.GroundHeight.Y(cast.Cx, cast.Cz) + .05f, cast.Cz);
                    var volley = Instantiate(ChargeEffect, at, Quaternion.Euler(0f, cast.Angle, 0f));
                    volley.FitLanes(cast.Shape, SlashMarkLift);
                }
            }
            Shake(lethal ? LethalShake : HitShake, ShakeSeconds);
        }

        void Shake(float strength, float seconds)
        {
            if (_cam == null) _cam = Director != null && Director.Stage != null ? Director.Stage.Cam : null;
            if (_cam == null) _cam = FindFirstObjectByType<BoneSquadKiting.Cameras.EdgeScrollCamera>();
            if (_cam != null) _cam.PlayPresentationShake(strength, seconds);
        }

        void UpdateImpactShake()
        {
            if (!_landed)
            {
                bool contact = _fight.State == FrostKnightState.Groggy && _fight.StateTime >= GroggyDropSeconds;
                // In ChargedSlash the foot touches down at 6.8 seconds, just before release at 7.
                if (_fight.State == FrostKnightState.Charging && (!_fight.Casting || _fight.StateTime * _fight.AnimSpeed >= 6.8f))
                    contact = true;
                if (contact)
                {
                    _landed = true;
                    Shake(LandingShake, .22f);
                }
            }
            if (_fight.State != FrostKnightState.Dying || Animator == null || _dissolve == null || _deathPeak) return;
            var pose = Animator.IsInTransition(0) ? Animator.GetNextAnimatorStateInfo(0) : Animator.GetCurrentAnimatorStateInfo(0);
            if (pose.shortNameHash != DieState) return;
            float untilDissolve = (_dissolve.StartNormalizedTime - pose.normalizedTime) * pose.length / Mathf.Max(.05f, Animator.speed);
            if (untilDissolve > 0f)
            {
                float progress = Mathf.Clamp01(pose.normalizedTime / Mathf.Max(.001f, _dissolve.StartNormalizedTime));
                float swell = Mathf.SmoothStep(0f, 1f, 1f - untilDissolve / .85f);
                if (_cam != null) _cam.BossDeathRumble = DeathShake * (.2f * progress + .45f * swell);
            }
            else
            {
                _deathPeak = true;
                if (_cam != null) _cam.BossDeathRumble = 0f;
                Shake(DeathShake, .16f);
            }
        }
    }
}
