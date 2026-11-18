using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FrostKnightSwordReveal : MonoBehaviour
    {
        static readonly int Dissolve = Shader.PropertyToID("_DissolveAmount");
        static readonly int Raise = Animator.StringToHash("RaiseHands");
        static readonly int Slam = Animator.StringToHash("ThroneSlam");
        static readonly int Charge = Animator.StringToHash("ChargedSlash");
        static readonly int Plant = Animator.StringToHash("SwordPlant");

        [SerializeField, Min(0.01f)] float seatedSeconds = 1.5f;
        [SerializeField, Min(0.01f)] float chargeSeconds = 2.4f;
        [SerializeField, Min(0.01f)] float landingSeconds = 0.29f;

        [SerializeField, Min(0.01f)] float seatedDismissSeconds = 1.1f;
        [SerializeField, Min(0.01f)] float chargedDismissSeconds = 0.62f;
        static readonly int Groggy = Animator.StringToHash("Groggy");

        Animator animator;
        MeshRenderer blade;
        MaterialPropertyBlock properties;
        int previousState;
        float previousTime;
        float phaseOffset;
        float progress;
        float applied;
        float appliedGlow;
        Color emission;
        float edgeWidth;
        static readonly int EdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        bool landingFromCharge;
        bool dismissing;
        float landingLimit = 1f;

        void Awake()
        {
            animator = GetComponentInParent<Animator>();
            blade = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            emission = blade.sharedMaterial.GetColor(Emission);
            edgeWidth = blade.sharedMaterial.GetFloat(EdgeWidth);
        }

        void LateUpdate()
        {
            if (!animator || !animator.isInitialized) return;
            var state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            int id = state.shortNameHash;
            float duration = id == Charge ? chargeSeconds : id == Plant ? landingSeconds
                : (id == Raise || id == Slam) ? seatedSeconds : 0f;
            float seconds = state.normalizedTime * state.length;
            float phase = duration > 0f ? seconds / duration : 1f;
            if (id != previousState || state.normalizedTime < previousTime)
            {
                // A completed disappearance makes the next plant a fresh summon.
                landingFromCharge = previousState == Charge && id == Plant && progress > .001f;
                landingLimit = landingFromCharge && dismissing ? progress : 1f;
                bool continues = landingFromCharge
                    || (previousState == Raise && id == Slam);
                // The seated follow-through enters partway into its clip.
                phaseOffset = continues ? progress - phase : 0f;
            }
            previousState = id;
            previousTime = state.normalizedTime;
            progress = Mathf.Clamp01(phase + phaseOffset);
            float fade = id == Slam ? seatedDismissSeconds : id == Charge ? chargedDismissSeconds
                : landingFromCharge ? .35f : 0f;
            float remaining = Mathf.Max(0f, state.length - seconds);
            dismissing = fade > 0f && remaining <= fade;
            if (dismissing) progress = Mathf.Min(progress, remaining / fade);
            if (landingFromCharge) progress = Mathf.Min(progress, landingLimit);
            if (duration == 0f && id != Groggy) progress = 0f;
            float amount = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float glow = dismissing ? 4f * progress * (1f - progress) : 0f;
            SetAmount(amount, glow);
        }

        void SetAmount(float amount, float glow)
        {
            if (amount == applied && glow == appliedGlow) return;
            blade.GetPropertyBlock(properties);
            properties.SetFloat(Dissolve, amount);
            properties.SetFloat(EdgeWidth, edgeWidth * glow);
            properties.SetColor(Emission, emission * (1f + 3.2f * glow));
            blade.SetPropertyBlock(properties);
            applied = amount;
            appliedGlow = glow;
        }

        void OnDisable()
        {
            if (blade) SetAmount(0f, 0f);
            previousState = 0;
            previousTime = 0f;
        }
    }
}
