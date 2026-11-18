using System.Linq;
using UnityEngine;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Visuals
{
    /// <summary>What a character is doing, as far as its looks are concerned.</summary>
    public enum VisualState { Idle, Run, Attack, Die, Revive }

    /// <summary>
    /// Sits on the root of a character model and turns game states into Animator states, by name. No
    /// transitions or parameters are needed in the Animator: the named state is cross-faded to directly.
    /// A state the Animator does not have (or an empty name) is skipped without complaint, so a model with
    /// only some of its animations made still works.
    /// </summary>
    public class UnitVisual : MonoBehaviour
    {
        public AnimStates States = new();
        public Animator Animator;          // found in children when left empty

        /// <summary>Name an animation event must call for the attack to let go of its projectile.</summary>
        public const string AttackEventName = "AttackRelease";

        /// <summary>Seconds after an attack starts at which its AttackRelease event fires; 0 when it has none.</summary>
        public float AttackEventTime => _eventTime / (AttackSpeed * Scale);
        /// <summary>How long the attack animation that has the event takes to play; 0 when there is none.</summary>
        public float AttackLength => _clipLength / (AttackSpeed * Scale);
        /// <summary>How long the attack animation takes when it is not sped up (AttackPlaybackScale 1).</summary>
        public float NaturalAttackLength => _clipLength / AttackSpeed;
        /// <summary>
        /// Plays the attack animation this many times faster, so a unit meant to attack more often than
        /// its motion lasts still shows the whole motion. 1 = as authored.
        /// </summary>
        public float AttackPlaybackScale = 1f;
        float Scale => Mathf.Max(0.01f, AttackPlaybackScale);
        /// <summary>
        /// Playback speed of the attack state, as set in the Animator. It cannot be read until the state has
        /// played once, so it is 1 for the very first attack (the event still releases the shot on time).
        /// </summary>
        public float AttackSpeed { get; private set; } = 1f;
        float _eventTime, _clipLength;
        /// <summary>Raised when the attack animation reaches its AttackRelease event.</summary>
        public System.Action AttackReleased;

        VisualState _current = (VisualState)(-1);
        float _attackUntil;
        Renderer[] _flashRenderers;
        float _lastFlash;
        int _attackLayer = -1;
        bool _varyLoops;
        float _loopPhase;
        float _loopSpeed = 1f;

        /// <summary>Stable per-soldier variation; never changes attack playback speed.</summary>
        public void SetLoopVariation(int unitIndex)
        {
            _varyLoops = true;
            _loopPhase = Mathf.Repeat((unitIndex + 1) * 0.61803399f, 1f);
            _loopSpeed = Mathf.Lerp(0.94f, 1.06f, Mathf.Repeat((unitIndex + 1) * 0.75487767f, 1f));
            _current = (VisualState)(-1);
        }

        /// <summary>Drive every Mob PBR surface on this model with its own material's hit color and intensity.</summary>
        public void SetHitFlash(float amount, MaterialPropertyBlock block)
        {
            if (amount == _lastFlash) return;
            _lastFlash = amount;
            _flashRenderers ??= GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterials.Any(HitFlash.IsSupported)).ToArray();
            foreach (var renderer in _flashRenderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetFloat(HitFlash.AmountId, amount);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Called by the animation event in the attack clip (see <see cref="AttackEventName"/>).</summary>
        public void AttackRelease() => AttackReleased?.Invoke();

        // Look through the model's clips for the attack event, to learn when the projectile leaves.
        void ReadAttackTiming()
        {
            _eventTime = _clipLength = 0f;
            var controller = Animator != null ? Animator.runtimeAnimatorController : null;
            if (controller == null) return;
            foreach (var clip in controller.animationClips)
                foreach (var e in clip.events)
                    if (e.functionName == AttackEventName) { _eventTime = e.time; _clipLength = clip.length; return; }
        }

        void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
            ReadAttackTiming();
        }

        void Start()
        {
            // Prefab/controller defaults must not leave an upper-body attack permanently blended in.
            int layer = FindStateLayer(States.Attack);
            if (layer > 0) Animator.SetLayerWeight(layer, 0f);
        }

        void Update()
        {
            if (_varyLoops && Animator != null && (_current == VisualState.Idle || _current == VisualState.Run)
                && Has(_current))
                Animator.speed = Time.time < _attackUntil || _attackLayer > 0 ? 1f : _loopSpeed;
            // A sped-up attack (AttackPlaybackScale) runs the Animator faster only while the attack shows.
            if (Animator != null && AttackPlaybackScale != 1f)
            {
                if (Time.time < _attackUntil) Animator.speed = Scale;
                else if (Mathf.Approximately(Animator.speed, Scale))
                    Animator.speed = _varyLoops && (_current == VisualState.Idle || _current == VisualState.Run) ? _loopSpeed : 1f;
            }
            if (_attackLayer > 0)
            {
                float targetWeight = Time.time < _attackUntil ? 1f : 0f;
                float fadeSeconds = Mathf.Max(0.01f, States.CrossFade);
                float weight = Mathf.MoveTowards(
                    Animator.GetLayerWeight(_attackLayer), targetWeight, Time.deltaTime / fadeSeconds);
                Animator.SetLayerWeight(_attackLayer, weight);
                if (targetWeight == 0f && weight <= 0f) _attackLayer = -1;
            }

            // Learn the attack state's speed while it plays.
            if (_clipLength <= 0f || Animator == null || string.IsNullOrEmpty(States.Attack)) return;
            int layer = FindStateLayer(States.Attack);
            if (layer < 0) return;
            var info = Animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == Animator.StringToHash(States.Attack) && info.speed > 0.01f) AttackSpeed = info.speed;   // the state's own speed; the playback scale is applied on top
        }

        /// <summary>The state currently requested (Attack shows briefly, then falls back).</summary>
        public VisualState Current => _current;

        /// <summary>Show a one-off attack, then return to whatever SetState asks for.</summary>
        public void PlayAttack(float holdSeconds = -1f)
        {
            if (Animator != null) Animator.speed = Scale;        // a sped-up attack plays its whole motion in less time
            if (holdSeconds < 0f) holdSeconds = AttackLength > 0f ? AttackLength : 0.35f;
            _attackUntil = Time.time + holdSeconds;
            int layer = FindStateLayer(States.Attack);
            if (layer > 0)
            {
                _attackLayer = layer;
                Animator.CrossFadeInFixedTime(Animator.StringToHash(States.Attack), States.CrossFade, layer, 0f);
                return;
            }
            Apply(VisualState.Attack, restart: true);
        }

        /// <summary>Ask for a looping state. Ignored while a one-off attack is still showing.</summary>
        public void CancelAttack()
        {
            if (_attackUntil <= 0f && _attackLayer < 0) return;
            _attackUntil = 0f;
            if (_attackLayer > 0 && Animator != null) Animator.SetLayerWeight(_attackLayer, 0f);
            _attackLayer = -1;
            _current = (VisualState)(-1);
        }

        public void SetState(VisualState state)
        {
            // An upper-body attack is an overlay, so the base layer must still switch between Run and Idle.
            if (_attackLayer <= 0 && Time.time < _attackUntil && state != VisualState.Die) return;
            Apply(state, restart: false);
        }

        int FindStateLayer(string name)
        {
            if (Animator == null || string.IsNullOrEmpty(name)) return -1;
            int hash = Animator.StringToHash(name);
            for (int i = 0; i < Animator.layerCount; i++)
                if (Animator.HasState(i, hash)) return i;
            return -1;
        }

        void Apply(VisualState state, bool restart)
        {
            if (state == _current && !restart) return;
            _current = state;
            string name = NameOf(state);
            if (Animator == null) return;
            int hash = string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
            if (hash == 0 || !Animator.HasState(0, hash))
            {
                // This model has no such animation (yet): keep what is playing. A model with no idle would
                // go on running on the spot, so standing still holds its pose instead.
                if (state == VisualState.Idle) Animator.speed = 0f;
                return;
            }
            bool variedLoop = _varyLoops && (state == VisualState.Idle || state == VisualState.Run);
            Animator.speed = state == VisualState.Attack ? Scale
                : variedLoop && Time.time >= _attackUntil && _attackLayer < 0 ? _loopSpeed : 1f;
            if (restart) Animator.CrossFadeInFixedTime(hash, States.CrossFade, 0, 0f);
            else if (variedLoop)
                Animator.CrossFade(hash, States.CrossFade, 0, _loopPhase);
            else Animator.CrossFadeInFixedTime(hash, States.CrossFade);
        }

        string NameOf(VisualState state) => state switch
        {
            VisualState.Idle => States.Idle,
            VisualState.Run => States.Run,
            VisualState.Attack => States.Attack,
            VisualState.Die => States.Die,
            VisualState.Revive => States.Revive,
            _ => null,
        };

        /// <summary>True if the Animator has the state this visual would play for <paramref name="state"/>.</summary>
        public bool Has(VisualState state)
        {
            string name = NameOf(state);
            return FindStateLayer(name) >= 0;
        }
    }
}
