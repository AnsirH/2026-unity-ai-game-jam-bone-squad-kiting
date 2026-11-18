using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Mobs
{
    /// <summary>Temporary visuals: one pooled sphere per living mob. No colliders, no rigidbodies.</summary>
    public class MobView : MonoBehaviour
    {
        public GameDirector Director;
        public Material MobMaterial;
        public Color HitFlashColor = new(3f, 3f, 3f, 1f);

        readonly List<Transform> _pool = new();
        readonly List<Renderer> _renderers = new();
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (MobMaterial != null && MobMaterial.HasProperty(EmissionId)) _baseEmission = MobMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            var mobs = Director != null && Director.Mobs != null ? Director.Mobs.Mobs : null;
            int n = mobs?.Count ?? 0;
            while (_pool.Count < n) Create();
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < n && mobs[i].Alive;
                if (_pool[i].gameObject.activeSelf != on) _pool[i].gameObject.SetActive(on);
                if (!on) continue;
                var m = mobs[i];
                float d = m.Radius * 2f;
                _pool[i].position = new Vector3(m.X, m.Radius, m.Z);
                _pool[i].localScale = new Vector3(d, d, d);
                if (m.Flash > 0f)
                {
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, m.Flash));
                    _renderers[i].SetPropertyBlock(_block);
                }
                else if (_renderers[i].HasPropertyBlock()) _renderers[i].SetPropertyBlock(null);
            }
        }

        void Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Mob";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            if (MobMaterial != null) r.sharedMaterial = MobMaterial;
            _pool.Add(go.transform); _renderers.Add(r);
        }
    }
}
