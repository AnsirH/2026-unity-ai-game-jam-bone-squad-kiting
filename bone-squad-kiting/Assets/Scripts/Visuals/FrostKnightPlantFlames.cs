using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FrostKnightPlantFlames : MonoBehaviour
    {
        static readonly int Plant = Animator.StringToHash("SwordPlant");
        static readonly int Phase = Shader.PropertyToID("_BurnPhase");
        static readonly int Tiling = Shader.PropertyToID("_FireTiling");
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

        // In combat the hit event supplies the world footprint. The model's own fire still
        // follows the animation when the prefab is previewed on its own.
        public void BurnArea(Vector3 floorCenter, float yaw, Vector2 size, Transform arena)
        {
            float height = Mathf.Abs(transform.lossyScale.y);
            var fire = Instantiate(this, floorCenter + Vector3.up * (height * .5f),
                Quaternion.Euler(0f, yaw, 0f));
            fire.transform.localScale = new Vector3(size.x, height, size.y);
            fire.transform.SetParent(arena, true);
            fire.enabled = true;
            fire.areaBurn = true;
            fire.elapsed = 0f;
            fire.DrawPhase(0f);
            fire.properties.SetVector(Tiling, new Vector4(size.x * 1.25f, size.y * 1.25f, 0f, 0f));
            fire.flames.SetPropertyBlock(fire.properties);
        }

        bool areaBurn;

        void DrawPhase(float phase)
        {
            if (flames == null) flames = GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
            flames.GetPropertyBlock(properties);
            properties.SetFloat(Phase, phase);
            flames.SetPropertyBlock(properties);
            flames.enabled = phase < 1f;
        }

        void LateUpdate()
        {
            if (areaBurn)
            {
                elapsed += Time.deltaTime;
                DrawPhase(Mathf.Clamp01(elapsed / burnSeconds));
                if (!flames.enabled) Destroy(gameObject);
                return;
            }
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
                DrawPhase(Mathf.Clamp01(elapsed / burnSeconds));
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
