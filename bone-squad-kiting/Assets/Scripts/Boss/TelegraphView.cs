using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Fx;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Temporary telegraphs: draws the danger area of every pattern being cast (the boss's and each mini
    /// boss's) on the ground. A zone brightens once it locks, i.e. stops following the squad.
    /// On uneven ground the zone is laid over the ground's shape (see GroundHeight), and its material
    /// draws it above the ground but behind anything standing on it.
    /// With the animated telegraph material (shader BoneSquad/Attack Telegraph) the zone grows in when it
    /// appears and keeps the material's own colours: dimmer while it still follows the squad, stronger while
    /// it is dealing damage, and the shader's lethal colours for a lethal pattern. Any other material is
    /// tinted with the three colours below.
    /// </summary>
    public class TelegraphView : MonoBehaviour
    {
        public GameDirector Director;
        public Material Material;
        public Color TrackingColor = new(1f, 0.25f, 0.2f, 0.28f);
        public Color LockedColor = new(1f, 0.15f, 0.1f, 0.6f);
        public Color LiveColor = new(1f, 0.55f, 0.1f, 0.8f);   // a sustained zone that is dealing damage right now
        public float Height = 0.03f;
        [Tooltip("On uneven ground the zone is cut into pieces about this long, each laid on the ground.")]
        public float GroundStep = 1f;

        class Zone
        {
            public MeshFilter Filter; public MeshRenderer Renderer;
            public PatternDefinition Pattern; public DangerShape Shape;
            public MeshRenderer VolleyWarning;
            public MeshFilter VolleyMesh;
            public Vector3[] Flat, Laid;          // the mesh's points: as built, and lifted onto the ground
            public Vector3 LaidAt; public float LaidAngle; public bool IsLaid, HasPlacement;
            public float Born;                    // when this zone appeared: it grows in from then
        }

        readonly List<Zone> _zones = new();
        MaterialPropertyBlock _block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FillId = Shader.PropertyToID("_FillColor"), EdgeId = Shader.PropertyToID("_EdgeColor");
        static readonly int LethalId = Shader.PropertyToID("_LethalTelegraph"), ShapeId = Shader.PropertyToID("_Shape");
        static readonly int InnerId = Shader.PropertyToID("_InnerRadius"), ProgressId = Shader.PropertyToID("_Progress");
        static readonly int LoopId = Shader.PropertyToID("_Loop"), DurationId = Shader.PropertyToID("_Duration");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        [Tooltip("Animated material only: how much of the material's opacity a zone has while it still follows the squad.")]
        [Range(0f, 1f)] public float TrackingOpacity = 0.55f;
        [Tooltip("Animated material only: how much stronger the fill gets while the zone is dealing damage.")]
        [Min(1f)] public float LiveFillBoost = 1.6f;

        /// <summary>True when <see cref="Material"/> is the animated telegraph material (it has fill and rim colours).</summary>
        public bool Animated => Material != null && Material.HasProperty(FillId);

        void Awake() { _block = new MaterialPropertyBlock(); }

        // What the animated material needs to draw one zone: its shape, how far it has grown, and its state.
        void SetAnimated(Zone z, bool locked, bool live, bool lethal)
        {
            var shape = z.Shape;
            float kind = shape.Type == DangerShapeType.Disc ? 1f : shape.Type == DangerShapeType.Annulus ? 2f : 0f;
            _block.SetFloat(ShapeId, kind);
            if (kind == 2f) _block.SetFloat(InnerId, Mathf.Clamp(shape.RMin / Mathf.Max(shape.ROut, 1e-4f), 0.05f, 0.95f));
            _block.SetFloat(LoopId, 0f);   // the material's loop is for previews: a real zone grows in once and stays
            float grow = Mathf.Max(Material.GetFloat(DurationId), 0.01f);
            _block.SetFloat(ProgressId, Mathf.Clamp01((Time.time - z.Born) / grow));
            _block.SetFloat(LethalId, lethal ? 1f : 0f);
            Color fill = Material.GetColor(FillId), edge = Material.GetColor(EdgeId);
            if (live) fill.a = Mathf.Min(1f, fill.a * LiveFillBoost);
            else if (!locked) { fill.a *= TrackingOpacity; edge.a *= TrackingOpacity; }
            _block.SetColor(FillId, fill);
            _block.SetColor(EdgeId, edge);
        }

        void LateUpdate()
        {
            var telegraphs = Director != null && Director.Phase == GamePhase.Play ? Director.Telegraphs : null;
            int n = telegraphs?.Count ?? 0;
            while (_zones.Count < n) _zones.Add(Create());
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (i >= n) { z.Filter.gameObject.SetActive(false); z.Pattern = null; continue; }
                z.Filter.gameObject.SetActive(true);
                var a = telegraphs[i];
                if (z.Pattern != a.Pattern) z.Born = Time.time;   // a hidden zone forgets its pattern, so this is also "it just appeared"
                if (z.Pattern != a.Pattern || !z.Shape.Equals(a.Shape))
                {
                    if (z.Filter.sharedMesh != null) Destroy(z.Filter.sharedMesh);
                    z.Filter.sharedMesh = DangerShapeMesh.Build(a.Shape, GroundStep);
                    z.Filter.sharedMesh.MarkDynamic();
                    z.Flat = z.Filter.sharedMesh.vertices; z.Laid = new Vector3[z.Flat.Length]; z.HasPlacement = false;
                    z.Pattern = a.Pattern; z.Shape = a.Shape;
                }
                var at = new Vector3(a.Cx, Height, a.Cz);
                var turn = Quaternion.Euler(0f, a.Angle, 0f);                      // +Y turns clockwise seen from above
                z.Filter.transform.SetPositionAndRotation(at, turn);
                bool volley = a.Shape.Type == DangerShapeType.ThreeLanes && a.Pattern != null && a.Pattern.LaneWarningMaterial != null;
                LayOnGround(z, at, turn, a.Angle, volley);
                if (volley)
                {
                    DrawVolleyWarning(z, a);
                    continue;
                }
                if (z.VolleyWarning != null) z.VolleyWarning.enabled = false;
                bool locked = !a.Tracks || a.Remaining <= GameConstants.CastTrackLockTime;
                bool lethal = a.Pattern != null && a.Pattern.LethalTelegraph;
                // A lane warning fades out just before it strikes.
                float fade = a.Shape.Type == DangerShapeType.ThreeLanes && !a.Sustaining
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a.Remaining / 0.4f)) : 1f;
                _block.Clear();
                if (Animated)
                {
                    SetAnimated(z, locked, a.Sustaining, lethal);
                    _block.SetFloat(OpacityId, fade);
                }
                else
                {
                    var color = a.Sustaining ? LiveColor : locked ? LockedColor : TrackingColor;
                    // Keep the warning readable as it moves from tracking to live damage.
                    if (lethal) color = new Color(0.48f, 0.025f, 0.08f, Mathf.Lerp(0.5f, 1f, color.a));
                    else color.a *= 0.7f;
                    color.a *= fade;
                    _block.SetColor(ColorId, color);
                }
                z.Renderer.SetPropertyBlock(_block);
                z.Renderer.enabled = true;
            }
        }

        void DrawVolleyWarning(Zone zone, BossAttackSystem.Active cast)
        {
            zone.Renderer.enabled = false;
            if (zone.VolleyWarning == null)
            {
                var go = new GameObject("Volley Warning", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(zone.Filter.transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                zone.VolleyMesh = go.GetComponent<MeshFilter>();
                zone.VolleyWarning = go.GetComponent<MeshRenderer>();
                zone.VolleyWarning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            zone.VolleyMesh.sharedMesh = zone.Filter.sharedMesh;
            SovereignVolleyCast.DrawWarning(zone.VolleyWarning, cast.Pattern, cast.Shape, cast.Elapsed, cast.Remaining, _block);
        }

        // Lift every point of the zone to the ground under it. Skipped on flat stages and while the zone stays put.
        void LayOnGround(Zone z, Vector3 at, Quaternion turn, float angle, bool volley)
        {
            bool uneven = BoneSquadKiting.Stage.GroundHeight.Mask.value != 0;
            if (z.HasPlacement && z.IsLaid == uneven && (!uneven || (z.LaidAt == at && z.LaidAngle == angle))) return;
            float width = Mathf.Max(.0001f, 2f * z.Shape.ThreeLaneHalfSpan);
            float depth = Mathf.Max(.0001f, 2f * z.Shape.HalfZ);
            for (int i = 0; i < z.Flat.Length; i++)
            {
                var p = z.Flat[i];
                if (uneven)
                {
                    var w = at + turn * p;
                    p.y = BoneSquadKiting.Stage.GroundHeight.Y(w.x, w.z);
                }
                // The volley shader reads a scaled unit XY plane rotated 90 degrees onto the ground.
                z.Laid[i] = volley ? new Vector3(p.x / width, p.z / depth, -p.y) : p;
            }
            var mesh = z.Filter.sharedMesh;
            mesh.vertices = z.Laid;
            mesh.RecalculateBounds();
            z.HasPlacement = true; z.IsLaid = uneven; z.LaidAt = at; z.LaidAngle = angle;
        }

        Zone Create()
        {
            var go = new GameObject("Zone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var z = new Zone { Filter = go.GetComponent<MeshFilter>(), Renderer = go.GetComponent<MeshRenderer>() };
            z.Renderer.sharedMaterial = Material;
            z.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            z.Renderer.enabled = false;
            return z;
        }
    }
}
