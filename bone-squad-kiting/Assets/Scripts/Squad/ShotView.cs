using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Squad
{
    /// <summary>Temporary visuals: one small sphere per live projectile, pooled.</summary>
    public class ShotView : MonoBehaviour
    {
        public GameDirector Director;
        public Material ShotMaterial;
        public float Scale = 0.16f;
        public float Height = 0.45f;

        readonly List<Transform> _pool = new();

        void LateUpdate()
        {
            var shots = Director != null && Director.Shots != null ? Director.Shots.Shots : null;
            int n = shots?.Count ?? 0;
            while (_pool.Count < n) _pool.Add(Create());
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < n;
                if (_pool[i].gameObject.activeSelf != on) _pool[i].gameObject.SetActive(on);
                if (on) _pool[i].position = new Vector3(shots[i].X, Height, shots[i].Z);
            }
        }

        Transform Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shot";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Scale;
            if (ShotMaterial != null) go.GetComponent<Renderer>().sharedMaterial = ShotMaterial;
            return go.transform;
        }
    }
}
