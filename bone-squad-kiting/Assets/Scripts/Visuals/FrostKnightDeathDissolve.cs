using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class FrostKnightDeathDissolve : MonoBehaviour
    {
        static readonly int Death = Animator.StringToHash("Die");
        static readonly int Stand = Animator.StringToHash("StandUp");
        static readonly int Charge = Animator.StringToHash("ChargedSlash");
        static readonly int Dissolve = Shader.PropertyToID("_DissolveAmount");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int Tint = Shader.PropertyToID("_BaseColor");

        [SerializeField, Range(0f, .99f)] float roarStart = .885f;
        [SerializeField] ParticleSystemRenderer[] aura = System.Array.Empty<ParticleSystemRenderer>();

        Animator animator;
        SkinnedMeshRenderer body;
        MaterialPropertyBlock properties;
        [SerializeField, ColorUsage(false, true)] Color vanishEmission = new Color(2.4f, 23f, 13f);
        Color emission;
        float edgeWidth;
        static readonly int EdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
        Color[] auraTints;
        Vector2 applied;

        void Awake()
        {
            animator = GetComponentInParent<Animator>();
            body = GetComponent<SkinnedMeshRenderer>();
            properties = new MaterialPropertyBlock();
            emission = body.sharedMaterial.GetColor(Emission);
            edgeWidth = body.sharedMaterial.GetFloat(EdgeWidth);
            auraTints = new Color[aura.Length];
            for (int i = 0; i < aura.Length; i++)
                auraTints[i] = aura[i].sharedMaterial.GetColor(Tint);
        }

        // X is dissolve coverage. Y brightens both the armor markings and the aura.
        Vector2 Appearance(AnimatorStateInfo state)
        {
            float time = state.normalizedTime * state.length;
            if (state.shortNameHash == Stand)
            {
                float afterRise = time - state.length;
                float fade = Mathf.SmoothStep(0f, 1f, (afterRise - .16f) / .8f);
                return new Vector2(fade, VanishGlow(fade));
            }
            if (state.shortNameHash == Charge)
            {
                float reveal = Mathf.SmoothStep(0f, 1f, time / 1.35f);
                float remaining = state.length - time;
                float fade = Mathf.SmoothStep(0f, 1f, 1f - remaining / .62f);
                return new Vector2(Mathf.Max(1f - reveal, fade), VanishGlow(fade));
            }
            if (state.shortNameHash == Death)
            {
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roarStart, 1f, state.normalizedTime));
                return new Vector2(fade, VanishGlow(fade));
            }
            return Vector2.zero;
        }

        static float VanishGlow(float fade) => 4f * fade * (1f - fade);

        void LateUpdate()
        {
            if (!animator || !animator.isInitialized) return;
            var appearance = Appearance(animator.GetCurrentAnimatorStateInfo(0));
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                appearance = next.shortNameHash == Charge ? Appearance(next) :
                    Vector2.Lerp(appearance, Appearance(next), animator.GetAnimatorTransitionInfo(0).normalizedTime);
            }
            Apply(appearance);
        }

        void Apply(Vector2 appearance)
        {
            if (appearance.Equals(applied)) return;
            body.GetPropertyBlock(properties);
            properties.SetFloat(Dissolve, appearance.x);
            properties.SetColor(Emission, Color.LerpUnclamped(emission, vanishEmission, appearance.y));
            properties.SetFloat(EdgeWidth, edgeWidth * appearance.y);
            body.SetPropertyBlock(properties);
            for (int i = 0; i < aura.Length; i++)
            {
                Color color = auraTints[i] * (1f + 2.5f * appearance.y);
                color.a = auraTints[i].a * (1f - appearance.x);
                aura[i].GetPropertyBlock(properties);
                properties.SetColor(Tint, color);
                aura[i].SetPropertyBlock(properties);
            }
            applied = appearance;
        }

        void OnDisable()
        {
            if (body) Apply(Vector2.zero);
        }
    }
}
