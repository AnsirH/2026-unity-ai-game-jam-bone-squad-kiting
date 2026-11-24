using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoneSquadKiting.Visuals
{
    /// <summary>Owns runtime material copies, so pooling never alters shared prefab materials.</summary>
    public sealed class ProjectileFade : MonoBehaviour
    {
        readonly List<Material> _materials = new();
        readonly List<Dictionary<string, Color>> _colors = new();

        void Awake()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var copies = renderer.materials;
                foreach (var mat in copies)
                {
                    // Built-in URP Lit opaque surfaces need transparent rendering for alpha fades.
                    if (mat.shader.name == "Universal Render Pipeline/Lit" && mat.GetFloat("_Surface") == 0f)
                    {
                        mat.SetFloat("_Surface", 1f);
                        mat.SetFloat("_Blend", 0f);
                        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                        mat.SetFloat("_ZWrite", 0f);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                    }
                    var colors = new Dictionary<string, Color>();
                    for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                        if (mat.shader.GetPropertyType(i) == ShaderPropertyType.Color)
                        {
                            string property = mat.shader.GetPropertyName(i);
                            colors[property] = mat.GetColor(property);
                        }
                    _materials.Add(mat);
                    _colors.Add(colors);
                }
            }
        }

        public void SetFade(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            for (int i = 0; i < _materials.Count; i++)
            {
                var material = _materials[i];
                bool additive = material.shader.name != "Universal Render Pipeline/Lit";
                foreach (var pair in _colors[i])
                {
                    var color = pair.Value;
                    color.a *= opacity;
                    // Covers additive graphs that use RGB as their opacity mask.
                    if (additive || pair.Key.ToLowerInvariant().Contains("emission"))
                    { color.r *= opacity; color.g *= opacity; color.b *= opacity; }
                    material.SetColor(pair.Key, color);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var material in _materials) Destroy(material);
        }
    }
}
