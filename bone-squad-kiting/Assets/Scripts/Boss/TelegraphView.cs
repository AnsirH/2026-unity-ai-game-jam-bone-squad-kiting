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
        }

        readonly List<Zone> _zones = new();
        MaterialPropertyBlock _block;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake() { _block = new MaterialPropertyBlock(); }

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
                var color = a.Sustaining ? LiveColor : locked ? LockedColor : TrackingColor;
                if (a.Pattern != null && a.Pattern.LethalTelegraph)
                {
                    // Keep the warning readable as it moves from tracking to live damage.
                    color = new Color(0.48f, 0.025f, 0.08f, Mathf.Lerp(0.5f, 1f, color.a));
                }
                else
                {
                    color.a *= 0.7f;
                }
                if (a.Shape.Type == DangerShapeType.ThreeLanes && !a.Sustaining)
                    color.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a.Remaining / 0.4f));
                _block.SetColor(ColorId, color);
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
