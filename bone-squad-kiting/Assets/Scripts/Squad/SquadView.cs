using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Squad
{
    /// <summary>Temporary visuals: one capsule per unit, positioned from the unit data each frame.</summary>
    public class SquadView : MonoBehaviour
    {
        public FormationController Squad;
        public Material UnitMaterial;
        public float UnitScale = 0.34f;

        public Color HitFlashColor = new(3f, 3f, 3f, 1f); // emission while a unit's hit flash is at full
        public Color ReviveColor = new(0.4f, 3f, 0.9f, 1f); // pulsing emission on a unit that is reviving a comrade

        readonly List<Transform> _views = new();
        readonly List<Renderer> _renderers = new();
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            // Units already placed under this object (so they show in the editor) are reused in order.
            foreach (Transform child in transform) _views.Add(child);
            _block = new MaterialPropertyBlock();
            if (UnitMaterial != null && UnitMaterial.HasProperty(EmissionId)) _baseEmission = UnitMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            if (Squad == null) return;
            while (_views.Count < Squad.Units.Count) _views.Add(CreateView(_views.Count));
            for (int i = 0; i < _views.Count; i++)
            {
                bool alive = i < Squad.Units.Count && !Squad.Units[i].Dead;
                if (_views[i].gameObject.activeSelf != alive) _views[i].gameObject.SetActive(alive);
                if (!alive) continue;
                var u = Squad.Units[i];
                _views[i].position = new Vector3(u.X, UnitScale, u.Z);

                // Brighten units that were just hit; the flash value decays in SquadUnit.MoveToward.
                while (_renderers.Count <= i) _renderers.Add(_views[_renderers.Count].GetComponent<Renderer>());
                var r = _renderers[i];
                if (r == null) continue;
                if (u.Flash > 0f)
                {
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, u.Flash));
                    r.SetPropertyBlock(_block);
                }
                else if (u.Reviving)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, ReviveColor, 0.35f + 0.65f * pulse));
                    r.SetPropertyBlock(_block);
                }
                else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
            }
        }

        Transform CreateView(int index)
        {
            var go = BuildUnit(index, UnitScale, UnitMaterial);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>One placeholder unit. Also used by the scene builder to pre-place the squad.</summary>
        public static GameObject BuildUnit(int index, float scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Unit " + index;
            var col = go.GetComponent<Collider>(); // units are not pickable or physical
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            go.transform.localScale = Vector3.one * scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
