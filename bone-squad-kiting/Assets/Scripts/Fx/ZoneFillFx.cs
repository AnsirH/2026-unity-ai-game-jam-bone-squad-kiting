using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Fx
{
    /// <summary>
    /// Fills a pattern's danger area with copies of its fill effect when the area goes off, and takes them
    /// away again after the pattern's FillSeconds. Copies are reused.
    /// </summary>
    public class ZoneFillFx : MonoBehaviour
    {
        const float GrowSeconds = 0.1f, ShrinkSeconds = 0.2f;

        class Item { public GameObject Prefab, Go; public ParticleSystem[] Particles; public Vector3 Scale; public float Age, Life; }

        static ZoneFillFx _host;
        static readonly List<Vector2> Points = new();

        readonly List<Item> _live = new();
        readonly Dictionary<GameObject, Stack<Item>> _free = new();

        /// <summary>How many copies are showing right now.</summary>
        public static int LiveCount => _host != null ? _host._live.Count : 0;

        /// <summary>Fill the shape (centre, clockwise turn in degrees) with the pattern's fill effect. Does nothing outside play.</summary>
        public static void Play(PatternDefinition pattern, DangerShape shape, float cx, float cz, float clockwiseDegrees)
        {
            if (pattern == null || pattern.FillEffect == null || !Application.isPlaying) return;
            if (_host == null) _host = new GameObject("Zone Fill Fx").AddComponent<ZoneFillFx>();
            Points.Clear();
            DangerShapeFill.Points(shape, pattern.FillSpacing, Points);
            var turn = Quaternion.Euler(0f, clockwiseDegrees, 0f);          // +Y turns clockwise seen from above
            foreach (var p in Points)
            {
                var at = new Vector3(cx, 0f, cz) + turn * new Vector3(p.x, 0f, p.y);
                at.y = BoneSquadKiting.Stage.GroundHeight.Y(at.x, at.z);
                _host.Show(pattern, at);
            }
        }

        void Show(PatternDefinition pattern, Vector3 at)
        {
            var prefab = pattern.FillEffect;
            Item it;
            if (_free.TryGetValue(prefab, out var stack) && stack.Count > 0) it = stack.Pop();
            else
            {
                var go = Instantiate(prefab, transform);
                it = new Item { Prefab = prefab, Go = go, Particles = go.GetComponentsInChildren<ParticleSystem>(true) };
            }
            // The prefab's own rotation and size are kept; each copy gets a random turn so they do not look stamped.
            it.Go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.value * 360f, 0f) * prefab.transform.rotation);
            it.Scale = prefab.transform.localScale * Mathf.Max(0.01f, pattern.FillScale);
            it.Go.transform.localScale = it.Scale * 0.01f;
            it.Age = 0f; it.Life = Mathf.Max(GrowSeconds + ShrinkSeconds, pattern.FillSeconds);
            it.Go.SetActive(true);
            foreach (var ps in it.Particles) { ps.Clear(true); ps.Play(true); }
            _live.Add(it);
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var it = _live[i];
                it.Age += Time.deltaTime;
                if (it.Age >= it.Life)
                {
                    it.Go.SetActive(false);
                    if (!_free.TryGetValue(it.Prefab, out var stack)) _free[it.Prefab] = stack = new Stack<Item>();
                    stack.Push(it);
                    _live.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Min(Mathf.Clamp01(it.Age / GrowSeconds), Mathf.Clamp01((it.Life - it.Age) / ShrinkSeconds));
                it.Go.transform.localScale = it.Scale * Mathf.Max(0.01f, k);
            }
        }
    }
}
