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
    /// </summary>
    public class TelegraphView : MonoBehaviour
    {
        public GameDirector Director;
        public Material Material;
        public Color TrackingColor = new(1f, 0.25f, 0.2f, 0.28f);
        public Color LockedColor = new(1f, 0.15f, 0.1f, 0.6f);
        public Color LiveColor = new(1f, 0.55f, 0.1f, 0.8f);   // a sustained zone that is dealing damage right now
        public float Height = 0.03f;

        class Zone
        {
            public MeshFilter Filter; public MeshRenderer Renderer;
            public PatternDefinition Pattern; public DangerShape Shape;
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
                    z.Filter.sharedMesh = DangerShapeMesh.Build(a.Shape);
                    z.Pattern = a.Pattern; z.Shape = a.Shape;
                }
                z.Filter.transform.position = new Vector3(a.Cx, Height, a.Cz);
                z.Filter.transform.rotation = Quaternion.Euler(0f, a.Angle, 0f);   // +Y turns clockwise seen from above
                bool locked = !a.Tracks || a.Remaining <= GameConstants.CastTrackLockTime;
                _block.SetColor(ColorId, a.Sustaining ? LiveColor : locked ? LockedColor : TrackingColor);
                z.Renderer.SetPropertyBlock(_block);
                z.Renderer.enabled = true;
            }
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
