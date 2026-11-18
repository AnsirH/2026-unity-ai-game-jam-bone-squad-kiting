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

        VisualState _current = (VisualState)(-1);
        float _attackUntil;

        void Awake()
        {
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
        }

        /// <summary>The state currently requested (Attack shows briefly, then falls back).</summary>
        public VisualState Current => _current;

        /// <summary>Show a one-off attack, then return to whatever SetState asks for.</summary>
        public void PlayAttack(float holdSeconds = 0.35f)
        {
            _attackUntil = Time.time + holdSeconds;
            Apply(VisualState.Attack, restart: true);
        }

        /// <summary>Ask for a looping state. Ignored while a one-off attack is still showing.</summary>
        public void SetState(VisualState state)
        {
            if (Time.time < _attackUntil && state != VisualState.Die) return;
            Apply(state, restart: false);
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
            Animator.speed = 1f;
            if (restart) Animator.CrossFadeInFixedTime(hash, States.CrossFade, 0, 0f);
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
            return Animator != null && !string.IsNullOrEmpty(name) && Animator.HasState(0, Animator.StringToHash(name));
        }
    }
}
