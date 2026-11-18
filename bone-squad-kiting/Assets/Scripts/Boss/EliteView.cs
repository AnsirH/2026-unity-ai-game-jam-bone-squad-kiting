using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Temporary visuals for mini bosses: one pooled cylinder each, with a collider and EnemyRef on the
    /// Enemy layer so a right click can pick it.
    /// </summary>
    public class EliteView : MonoBehaviour
    {
        public GameDirector Director;
        public Material EliteMaterial;
        public int EnemyLayer;
        public float Height = 1.6f;
        public Color HitFlashColor = new(4f, 2.5f, 2f, 1f);

        readonly List<GameObject> _pool = new();
        MaterialPropertyBlock _block;
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (EliteMaterial != null && EliteMaterial.HasProperty(EmissionId)) _baseEmission = EliteMaterial.GetColor(EmissionId);
        }

        void LateUpdate()
        {
            var elites = Director != null && Director.Elites != null ? Director.Elites.Elites : null;
            int n = elites?.Count ?? 0;
            while (_pool.Count < n) _pool.Add(Create());
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < n && elites[i].Alive;
                if (_pool[i].activeSelf != on) _pool[i].SetActive(on);
                if (!on) continue;
                var e = elites[i];
                _pool[i].GetComponent<EnemyRef>().Enemy = e;
                _pool[i].transform.position = new Vector3(e.X, Height * 0.5f, e.Z);
                _pool[i].transform.localScale = new Vector3(e.Radius * 2f, Height * 0.5f, e.Radius * 2f);
                var r = _pool[i].GetComponent<Renderer>();
                if (e.Flash > 0f)
                {
                    _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, Mathf.Clamp01(e.Flash)));
                    r.SetPropertyBlock(_block);
                }
                else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
            }
        }

        GameObject Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // keeps its collider: it must be clickable
            go.name = "Mini Boss";
            go.layer = EnemyLayer;
            go.transform.SetParent(transform, false);
            go.AddComponent<EnemyRef>();
            if (EliteMaterial != null) go.GetComponent<Renderer>().sharedMaterial = EliteMaterial;
            return go;
        }
    }
}
