using System.Collections.Generic;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;

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
            public Vector3[] Flat, Laid;          // the mesh's points: as built, and lifted onto the ground
            public Vector3 LaidAt; public float LaidAngle; public bool IsLaid;
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
                if (i >= n) { z.Renderer.enabled = false; z.Pattern = null; continue; }
                var a = telegraphs[i];
                if (z.Pattern != a.Pattern || !z.Shape.Equals(a.Shape))
                {
                    if (z.Filter.sharedMesh != null) Destroy(z.Filter.sharedMesh);
                    z.Filter.sharedMesh = DangerShapeMesh.Build(a.Shape, GroundStep);
                    z.Filter.sharedMesh.MarkDynamic();
                    z.Flat = z.Filter.sharedMesh.vertices; z.Laid = new Vector3[z.Flat.Length]; z.IsLaid = false;
                    z.Pattern = a.Pattern; z.Shape = a.Shape;
                }
                var at = new Vector3(a.Cx, Height, a.Cz);
                var turn = Quaternion.Euler(0f, a.Angle, 0f);                      // +Y turns clockwise seen from above
                z.Filter.transform.SetPositionAndRotation(at, turn);
                LayOnGround(z, at, turn, a.Angle);
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
                _block.SetColor(ColorId, color);
                z.Renderer.SetPropertyBlock(_block);
                z.Renderer.enabled = true;
            }
        }

        // Lift every point of the zone to the ground under it. Skipped on flat stages and while the zone stays put.
        void LayOnGround(Zone z, Vector3 at, Quaternion turn, float angle)
        {
            bool uneven = BoneSquadKiting.Stage.GroundHeight.Mask.value != 0;
            if (!uneven && !z.IsLaid) return;
            if (uneven && z.IsLaid && z.LaidAt == at && z.LaidAngle == angle) return;
            for (int i = 0; i < z.Flat.Length; i++)
            {
                var p = z.Flat[i];
                if (uneven)
                {
                    var w = at + turn * p;
                    p.y = BoneSquadKiting.Stage.GroundHeight.Y(w.x, w.z);
                }
                z.Laid[i] = p;
            }
            var mesh = z.Filter.sharedMesh;
            mesh.vertices = z.Laid;
            mesh.RecalculateBounds();
            z.IsLaid = uneven; z.LaidAt = at; z.LaidAngle = angle;
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
