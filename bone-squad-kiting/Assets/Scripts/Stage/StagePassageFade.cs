using BoneSquadKiting.Squad;
using UnityEngine;

namespace BoneSquadKiting.Stage
{
    // The transform sits on the threshold with local +Z pointing into the next room.
    public sealed class StagePassageFade : MonoBehaviour
    {
        public Renderer[] Surfaces = System.Array.Empty<Renderer>();
        public Light[] Lamps = System.Array.Empty<Light>();
        [Min(0.01f)] public float FadeSeconds = 0.45f;
        [Min(0f)] public float StageHalfWidth = 22f;

        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        MaterialPropertyBlock block;
        FormationController squad;
        float[] fullIntensity;
        float visibility = 1f;
        float nextLookup;

        void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            nextLookup = 0f;
            fullIntensity = new float[Lamps.Length];
            for (int i = 0; i < Lamps.Length; i++)
                if (Lamps[i]) fullIntensity[i] = Lamps[i].intensity;
        }

        void LateUpdate()
        {
            if (!squad || !squad.isActiveAndEnabled)
            {
                if (Time.unscaledTime < nextLookup) return;
                nextLookup = Time.unscaledTime + 0.5f;
                squad = null;
                foreach (var candidate in FindObjectsByType<FormationController>(FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled && candidate.gameObject.scene == gameObject.scene)
                    {
                        squad = candidate;
                        break;
                    }
                if (!squad) return;
            }

            var anchor = squad.Anchor;
            var local = transform.InverseTransformPoint(new Vector3(anchor.x, transform.position.y, anchor.y));
            float target = local.z >= 0f && Mathf.Abs(local.x) <= StageHalfWidth ? 0f : 1f;
            if (visibility != target)
                Apply(Mathf.MoveTowards(visibility, target, Time.deltaTime / Mathf.Max(0.01f, FadeSeconds)));
        }

        void Apply(float opacity)
        {
            visibility = opacity;
            foreach (var surface in Surfaces)
            {
                if (!surface) continue;
                surface.GetPropertyBlock(block);
                block.SetFloat(Opacity, opacity);
                surface.SetPropertyBlock(block);
            }

            for (int i = 0; i < Lamps.Length; i++)
                if (Lamps[i]) Lamps[i].intensity = fullIntensity[i] * opacity;
        }

        void OnDisable()
        {
            if (visibility != 1f) Apply(1f);
        }
    }
}
