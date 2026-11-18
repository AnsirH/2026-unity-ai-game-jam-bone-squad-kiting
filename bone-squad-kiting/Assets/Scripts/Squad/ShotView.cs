using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.Squad
{
    /// <summary>
    /// Draws the projectiles in flight, pooled. With an ArtSet that has projectile prefabs each shot is that
    /// model, turned so its tip (+Z after the art set's rotation) points the way it flies; the leader's
    /// shots use the leader's projectile. Without one, a shot is a small glowing sphere.
    /// </summary>
    public class ShotView : MonoBehaviour
    {
        public GameDirector Director;
        public ArtSet Art;                 // optional; taken from the scene's SquadView when left empty
        public Material ShotMaterial;      // for the placeholder spheres
        public float Scale = 0.16f;
        public float Height = 0.45f;       // above the ground

        class Pool
        {
            public readonly List<Transform> Items = new();
            public int Used;
        }
        readonly Pool _spheres = new(), _shots = new(), _leaderShots = new();
        bool _artLooked;

        void LateUpdate()
        {
            if (Art == null && !_artLooked)
            {
                _artLooked = true;
                var squadView = FindAnyObjectByType<SquadView>();
                if (squadView != null) Art = squadView.Art;
            }
            _spheres.Used = _shots.Used = _leaderShots.Used = 0;

            var shots = Director != null && Director.Shots != null ? Director.Shots.Shots : null;
            int n = shots?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                var s = shots[i];
                var leaderPrefab = Art != null ? Art.LeaderShotPrefab : null;
                var shotPrefab = Art != null ? Art.ShotPrefab : null;
                Transform t;
                if (s.Heavy && leaderPrefab != null) t = Take(_leaderShots, leaderPrefab, Art.LeaderShotScale, Art.LeaderShotRotation);
                else if (shotPrefab != null) t = Take(_shots, shotPrefab, Art.ShotScale, Art.ShotRotation);
                else t = Take(_spheres, null, 1f, Vector3.zero);
                t.position = new Vector3(s.X, Height + GroundHeight.Y(s.X, s.Z), s.Z);
                if (s.Dir.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(new Vector3(s.Dir.x, 0f, s.Dir.y));
            }
            Hide(_spheres); Hide(_shots); Hide(_leaderShots);
        }

        static void Hide(Pool pool)
        {
            for (int i = pool.Used; i < pool.Items.Count; i++)
                if (pool.Items[i].gameObject.activeSelf) pool.Items[i].gameObject.SetActive(false);
        }

        // The next free object of a pool, switched on. Its model is kept at the art set's current scale and
        // rotation, so both can be tuned while playing.
        Transform Take(Pool pool, GameObject prefab, float scale, Vector3 rotation)
        {
            if (pool.Used == pool.Items.Count) pool.Items.Add(prefab != null ? CreateModel(prefab) : CreateSphere());
            var t = pool.Items[pool.Used++];
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                foreach (var trail in t.GetComponentsInChildren<TrailRenderer>()) trail.Clear();   // no streak from its last flight
            }
            if (prefab != null)
            {
                var model = t.GetChild(0);
                model.localRotation = Quaternion.Euler(rotation) * prefab.transform.localRotation;
                model.localScale = prefab.transform.localScale * scale;
            }
            return t;
        }

        // A holder that points along the flight, with the model inside it turned to match.
        Transform CreateModel(GameObject prefab)
        {
            var holder = new GameObject(prefab.name + " (shot)");
            holder.transform.SetParent(transform, false);
            var model = Instantiate(prefab, holder.transform);
            model.transform.localPosition = Vector3.zero;
            foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);   // shots are not physical
            return holder.transform;
        }

        Transform CreateSphere()
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
