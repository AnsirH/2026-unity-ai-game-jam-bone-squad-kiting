using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FrostKnightPlantFlames : MonoBehaviour
    {
        static readonly int Plant = Animator.StringToHash("SwordPlant");
        static readonly int Phase = Shader.PropertyToID("_BurnPhase");
        const float ImpactSeconds = .25f;

        [SerializeField, Min(.1f)] float burnSeconds = 1.65f;

        Animator animator;
        MeshRenderer flames;
        MaterialPropertyBlock properties;
        bool enteredPlant;
        bool ignited;
        float lastTime;
        float elapsed;

        void Awake()
        {
            animator = GetComponentInParent<Animator>();
            flames = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            flames.enabled = false;
        }

        void LateUpdate()
        {
            if (!animator || !animator.isInitialized) return;
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            bool planting = state.shortNameHash == Plant;
            float time = state.normalizedTime * state.length;
            if (!planting || !enteredPlant || time < lastTime) ignited = false;
            if (planting && !ignited && time >= ImpactSeconds)
            {
                elapsed = time - ImpactSeconds;
                ignited = true;
                flames.enabled = elapsed < burnSeconds;
            }
            else if (flames.enabled)
                elapsed += animator.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (flames.enabled)
            {
                float phase = Mathf.Clamp01(elapsed / burnSeconds);
                flames.GetPropertyBlock(properties);
                properties.SetFloat(Phase, phase);
                flames.SetPropertyBlock(properties);
                flames.enabled = phase < 1f;
            }
            enteredPlant = planting;
            lastTime = time;
        }

        void OnDisable()
        {
            if (flames) flames.enabled = false;
            enteredPlant = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.15f, .65f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
