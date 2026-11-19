using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Fx
{
    /// <summary>
    /// A pattern's strike effect (the spear thrower's spears): one copy per part of the danger area comes in
    /// from the caster's side, grows to the size of that part on the way, and sticks in the ground almost
    /// lying down along the part the moment the area goes off: the camera shakes and debris flies up where it
    /// went in. Only the caster's direction matters, not how far away it is. It stays for the pattern's
    /// StrikeStaySeconds and then fades out. Copies are reused.
    /// </summary>
    public class ZoneStrikeFx : MonoBehaviour
    {
        const float Buried = 0.08f, ImpactLifeSeconds = 3f;

        class Item
        {
            public GameObject Prefab, Go; public ParticleSystem[] Particles; public TrailRenderer[] Trails;
            public BoneSquadKiting.Visuals.ProjectileFade Fader; public PatternDefinition Pattern; public bool Landed;
            public Vector3 From, To, Dir, Tip, BaseScale; public float Grow, Length, Age, Flight, Stay, Fade;
        }

        static ZoneStrikeFx _host;
        BoneSquadKiting.Cameras.EdgeScrollCamera _cam;

        /// <summary>How many impacts (shake + debris) have been played since the game started. For tests.</summary>
        public static int Impacts { get; private set; }
        static readonly List<Vector3> Parts = new();      // x, z of each part's centre (unturned), y = its long side

        readonly List<Item> _live = new();
        readonly Dictionary<GameObject, Stack<Item>> _free = new();

        /// <summary>How many copies are flying or standing right now.</summary>
        public static int LiveCount => _host != null ? _host._live.Count : 0;

        /// <summary>
        /// Throw the pattern's strike effect at every part of the shape, to land <paramref name="flightSeconds"/>
        /// from now. Does nothing outside play.
        /// </summary>
        public static void Play(PatternDefinition pattern, DangerShape shape, float cx, float cz, float clockwiseDegrees,
            Vector2 caster, float flightSeconds)
        {
            if (pattern == null || pattern.StrikeEffect == null || !Application.isPlaying) return;
            if (_host == null) _host = new GameObject("Zone Strike Fx").AddComponent<ZoneStrikeFx>();
            Parts.Clear();
            PartsOf(shape, Parts);
            var turn = Quaternion.Euler(0f, clockwiseDegrees, 0f);          // +Y turns clockwise seen from above
            var centre = new Vector3(cx, 0f, cz);
            var fromCaster = centre - new Vector3(caster.x, 0f, caster.y);
            foreach (var part in Parts)
            {
                var at = centre + turn * new Vector3(part.x, 0f, part.z);
                at.y = BoneSquadKiting.Stage.GroundHeight.Y(at.x, at.z);
                Vector3 heading;
                if (shape.Type == DangerShapeType.CrossBandsX || shape.Type == DangerShapeType.ThreeLanes)
                {
                    // Along the band, away from the side the caster is on (caster above = top to bottom).
                    var axis = turn * Vector3.forward;
                    heading = Vector3.Dot(fromCaster, axis) >= 0f ? axis : -axis;
                }
                else heading = fromCaster.sqrMagnitude > 1e-4f ? fromCaster.normalized : turn * Vector3.back;
                _host.Throw(pattern, at, heading, part.y, flightSeconds);
            }
        }

        /// <summary>The centre (x, z) and long side (y) of every separate part of a shape.</summary>
        public static void PartsOf(DangerShape s, List<Vector3> into)
        {
            float mid = (s.Inner + s.Outer) * 0.5f, side = s.Outer - s.Inner;
            switch (s.Type)
            {
                case DangerShapeType.Rectangle: into.Add(new Vector3(0f, 2f * Mathf.Max(s.Outer, s.HalfZ), 0f)); break;
                case DangerShapeType.CrossBandsX:
                    float longSide = Mathf.Max(side, 2f * s.HalfZ);
                    into.Add(new Vector3(mid, longSide, 0f)); into.Add(new Vector3(-mid, longSide, 0f));
                    break;
                case DangerShapeType.QuadrantBoxes:
                    into.Add(new Vector3(mid, side, mid)); into.Add(new Vector3(-mid, side, mid));
                    into.Add(new Vector3(mid, side, -mid)); into.Add(new Vector3(-mid, side, -mid));
                    break;
                case DangerShapeType.Disc: into.Add(new Vector3(0f, 2f * s.RMax, 0f)); break;
                case DangerShapeType.Annulus: into.Add(new Vector3(0f, 2f * s.ROut, 0f)); break;
                case DangerShapeType.ThreeLanes: into.Add(new Vector3(0f, 2f * s.HalfZ, 0f)); break;
            }
        }

        void Throw(PatternDefinition pattern, Vector3 at, Vector3 heading, float partSize, float flightSeconds)
        {
            var prefab = pattern.StrikeEffect;
            Item it;
            if (_free.TryGetValue(prefab, out var stack) && stack.Count > 0) it = stack.Pop();
            else
            {
                var go = Instantiate(prefab, transform);
                it = new Item { Prefab = prefab, Go = go, Fader = go.GetComponent<BoneSquadKiting.Visuals.ProjectileFade>() ?? go.AddComponent<BoneSquadKiting.Visuals.ProjectileFade>(), Particles = go.GetComponentsInChildren<ParticleSystem>(true), Trails = go.GetComponentsInChildren<TrailRenderer>(true) };
                // The model's own length along the way it flies (+Z), at the prefab's size.
                var mesh = go.GetComponentInChildren<MeshFilter>();
                it.Length = mesh != null && mesh.sharedMesh != null
                    ? mesh.sharedMesh.bounds.size.z * mesh.transform.lossyScale.z / Mathf.Max(1e-5f, go.transform.lossyScale.z) * prefab.transform.localScale.z
                    : 2f;
                if (it.Length < 0.01f) it.Length = 2f;
            }
            it.BaseScale = prefab.transform.localScale;
            it.Grow = Mathf.Max(0.05f, partSize * pattern.StrikeSize / it.Length);      // full size = the part's long side
            // Almost lying down: the heading tipped a little into the ground. It flies straight along itself,
            // ends centred on the part with its tip just under the ground.
            float pitch = Mathf.Clamp(pattern.StrikePitch, 0f, 89f) * Mathf.Deg2Rad;
            it.Dir = (heading.normalized * Mathf.Cos(pitch) + Vector3.down * Mathf.Sin(pitch)).normalized;
            float full = it.Length * it.Grow;
            it.To = at + Vector3.up * (Mathf.Sin(pitch) * full * (0.5f - Buried));
            it.From = it.To - it.Dir * (full * Mathf.Max(0f, pattern.StrikeTravel));
            it.Tip = at + heading.normalized * (Mathf.Cos(pitch) * full * (0.5f - Buried));      // where it goes into the ground
            it.Tip.y = BoneSquadKiting.Stage.GroundHeight.Y(it.Tip.x, it.Tip.z);
            it.Pattern = pattern; it.Landed = false; it.Fade = Mathf.Max(0.01f, pattern.StrikeFadeSeconds);
            it.Fader.SetFade(1f);
            it.Age = 0f; it.Flight = Mathf.Max(0.05f, flightSeconds); it.Stay = Mathf.Max(0f, pattern.StrikeStaySeconds);
            Place(it, 0f);
            it.Go.SetActive(true);
            foreach (var t in it.Trails) t.Clear();
            foreach (var ps in it.Particles) { ps.Clear(true); ps.Play(true); }
            _live.Add(it);
        }

        // t: 0 where it appears, 1 stuck in the ground.
        static void Place(Item it, float t)
        {
            float size = Mathf.Lerp(Mathf.Min(1f, it.Grow), it.Grow, Mathf.SmoothStep(0f, 1f, t));
            it.Go.transform.SetPositionAndRotation(Vector3.Lerp(it.From, it.To, t), Quaternion.LookRotation(it.Dir, Vector3.up));
            it.Go.transform.localScale = it.BaseScale * size;
        }

        // The moment it sticks in: shake the camera and throw debris up, sized to the spear.
        void Impact(Item it)
        {
            Impacts++;
            var p = it.Pattern;
            if (p == null) return;
            if (p.StrikeShake > 0f)
            {
                if (_cam == null) _cam = FindFirstObjectByType<BoneSquadKiting.Cameras.EdgeScrollCamera>();
                if (_cam != null) _cam.PlayPresentationShake(p.StrikeShake, p.StrikeShakeSeconds);
            }
            if (p.StrikeImpactEffect == null) return;
            // Thrown up and onward, the way the spear was going.
            var onward = it.Dir; onward.y = 0f;
            var debris = Instantiate(p.StrikeImpactEffect, it.Tip, Quaternion.LookRotation((Vector3.up + onward.normalized * 0.6f).normalized), transform);
            debris.transform.localScale = p.StrikeImpactEffect.transform.localScale * Mathf.Max(0.01f, it.Length * it.Grow * p.StrikeImpactScale);
            Destroy(debris, ImpactLifeSeconds);
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var it = _live[i];
                it.Age += Time.deltaTime;
                float end = it.Flight + it.Stay + it.Fade;
                if (it.Age >= end)
                {
                    it.Go.SetActive(false);
                    if (!_free.TryGetValue(it.Prefab, out var stack)) _free[it.Prefab] = stack = new Stack<Item>();
                    stack.Push(it);
                    _live.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01(it.Age / it.Flight);
                Place(it, t * t);                                                    // speeds up as it comes in
                if (!it.Landed && it.Age >= it.Flight) { it.Landed = true; Impact(it); }
                if (it.Age > it.Flight + it.Stay) it.Fader.SetFade((end - it.Age) / it.Fade);
            }
        }
    }
}
