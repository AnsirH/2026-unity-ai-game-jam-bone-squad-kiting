using UnityEngine;

namespace BoneSquadKiting.Fx
{
    /// <summary>One cast clock for all three blades, tip trails and fading ground marks.</summary>
    public sealed class SovereignVolley : MonoBehaviour
    {
        static readonly int Elapsed = Shader.PropertyToID("_Elapsed");
        const float EffectLifetime = 2.4f;
        Renderer[] parts;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            parts = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            SetAge(0f);
        }

        void Update()
        {
            age += Time.deltaTime;
            SetAge(age);
            if (age >= EffectLifetime) Destroy(gameObject);
        }

        void SetAge(float seconds)
        {
            foreach (var part in parts)
            {
                part.GetPropertyBlock(block);
                block.SetFloat(Elapsed, seconds);
                part.SetPropertyBlock(block);
            }
        }
    }
}
