using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Bosses
{
    /// <summary>
    /// Temporary telegraph: draws the current pattern's danger area on the ground and follows its center.
    /// Brightens once the zone locks (stops tracking the squad).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TelegraphView : MonoBehaviour
    {
        public GameDirector Director;
        public Color TrackingColor = new(1f, 0.25f, 0.2f, 0.28f);
        public Color LockedColor = new(1f, 0.15f, 0.1f, 0.6f);
        public float Height = 0.03f;

        MeshFilter _filter;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        PatternDefinition _shown;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        void LateUpdate()
        {
            var current = Director != null && Director.Attacks != null && Director.Phase == GamePhase.Play
                ? Director.Attacks.Current : null;
            if (current is not BossAttackSystem.Active a)
            {
                _renderer.enabled = false;
                _shown = null;
                return;
            }
            if (_shown != a.Pattern)
            {
                if (_filter.sharedMesh != null) Destroy(_filter.sharedMesh);
                _filter.sharedMesh = DangerShapeMesh.Build(a.Pattern.Shape);
                _shown = a.Pattern;
            }
            transform.position = new Vector3(a.Cx, Height, a.Cz);
            bool locked = a.Remaining <= GameConstants.CastTrackLockTime;
            _block.SetColor(ColorId, locked ? LockedColor : TrackingColor);
            _renderer.SetPropertyBlock(_block);
            _renderer.enabled = true;
        }
    }
}
