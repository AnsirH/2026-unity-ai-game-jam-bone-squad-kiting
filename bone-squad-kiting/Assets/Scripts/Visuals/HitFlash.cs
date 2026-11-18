using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    internal struct HitFlash
    {
        internal static readonly int AmountId = Shader.PropertyToID("_HitFlashAmount");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        internal static bool IsSupported(Material material) => material != null && material.HasProperty(AmountId);

        bool _wasActive;
        bool _overrodeEmission;
        Color _originalEmission;

        // Primitive views have one material. Restore only the properties this effect drives.
        internal void Apply(Renderer renderer, MaterialPropertyBlock block, float amount, Color color, bool reviving = false)
        {
            if (amount <= 0f && !_wasActive) return;
            _wasActive = amount > 0f;
            renderer.GetPropertyBlock(block);
            var material = renderer.sharedMaterial;
            bool supportsHitFlash = IsSupported(material);
            if (supportsHitFlash)
                block.SetFloat(AmountId, reviving ? 0f : amount);

            if (_wasActive && (!supportsHitFlash || reviving))
            {
                if (!_overrodeEmission)
                {
                    _originalEmission = block.HasColor(EmissionId) ? block.GetColor(EmissionId)
                        : material != null && material.HasProperty(EmissionId) ? material.GetColor(EmissionId) : Color.black;
                }
                block.SetColor(EmissionId, Color.Lerp(_originalEmission, color, amount));
                _overrodeEmission = true;
            }
            else if (_overrodeEmission)
            {
                block.SetColor(EmissionId, _originalEmission);
                _overrodeEmission = false;
            }
            renderer.SetPropertyBlock(block);
        }
    }
}
