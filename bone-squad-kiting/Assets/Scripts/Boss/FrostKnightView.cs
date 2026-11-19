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
        public float CircleEffectLead = 1.51f;
        public float CircleEffectScale = 1f;
        [Tooltip("Camera shake when an attack lands: ordinary attacks, and the lethal slam.")]
        public float HitShake = 0.3f;
        public float LethalShake = 0.6f;
        public float ShakeSeconds = 0.35f;

        [Header("Sounds")]
        public string CastSound = "boss.greatsword.summon";
        public string HitSound = "boss.greatsword.impact";
        public string ChargeSound = "boss.spinningblade.swing";

        FrostKnightFight _fight;
        int _serial = -1;
        bool _circleShown;
        Renderer _body;
        MaterialPropertyBlock _block;
        HitFlash _flash;
        EnemyRef _ref;
        Collider _collider;
        BoneSquadKiting.Cameras.EdgeScrollCamera _cam;
        float _seatY;

        void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
            if (Arena == null) Arena = GetComponentInParent<BossArena>();
            _body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            _block = new MaterialPropertyBlock();
            _ref = GetComponent<EnemyRef>();
            _collider = GetComponent<Collider>();
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
            if (_collider != null) _collider.enabled = fight.Targetable;
            if (_body != null) _flash.Apply(_body, _block, fight.Boss.Flash, HitFlashColor);

            // The falling sword has its own time to impact: start it that long before the circle goes off.
            if (!_circleShown && CircleEffect != null && fight.Casting && fight.Attack == FrostKnightAttack.Circle
                && fight.Telegraphs.Count > 0 && fight.Telegraphs[0].Remaining <= CircleEffectLead)
            {
                _circleShown = true;
                var cast = fight.Telegraphs[0];
                var at = new Vector3(cast.Cx, BoneSquadKiting.Stage.GroundHeight.Y(cast.Cx, cast.Cz), cast.Cz);
                var fx = Instantiate(CircleEffect, at, Quaternion.identity);
                fx.transform.localScale *= CircleEffectScale;
                Destroy(fx, 6f);
            }
        }

        // A new match (or none): forget the old fight, sit on the throne with the head bowed.
        void Bind(FrostKnightFight fight)
        {
            if (_fight != null) { _fight.OnCast -= OnCast; _fight.OnHit -= OnHit; }
            _fight = fight;
            _serial = -1;
            if (_ref != null) _ref.Enemy = fight?.Boss;
            if (fight == null) { Place(null); if (Animator != null && Animator.isActiveAndEnabled) { Animator.speed = 1f; Animator.Play("HeadBowed", 0, 0f); } return; }
            fight.OnCast += OnCast; fight.OnHit += OnHit;
        }

        void OnDestroy() { if (_fight != null) { _fight.OnCast -= OnCast; _fight.OnHit -= OnHit; } }

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
            transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, fight.Boss.Facing, 0f));
        }

        void PlayMotion(FrostKnightFight fight)
        {
            _serial = fight.AnimSerial;
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
            SoundManager.Current?.PlaySfx(attack == FrostKnightAttack.Charge ? ChargeSound : CastSound);
        }

        void OnHit(FrostKnightAttack attack, BossAttackSystem.Active cast, int hit, int lost)
        {
            SoundManager.Current?.PlaySfx(HitSound);
            bool lethal = attack >= FrostKnightAttack.LethalLeft;
            float shake = lethal ? LethalShake : HitShake;
            if (shake <= 0f) return;
            if (_cam == null) _cam = FindFirstObjectByType<BoneSquadKiting.Cameras.EdgeScrollCamera>();
            if (_cam != null) _cam.PlayPresentationShake(shake, ShakeSeconds);
        }
    }
}
