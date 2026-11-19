using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;
using UnityEngine;

namespace BoneSquadKiting.Fx
{
    // Each rescued unit reuses its own three pieces across revives.
    internal sealed class RescueBoneOrbit
    {
        readonly Transform _root;
        readonly Vector3[] _entry;
        ReviveSystem.Revive _rescue;
        float _radius, _lastElapsed;

        public RescueBoneOrbit(Transform parent, ArtSet art)
        {
            _root = new GameObject("Rescue bone orbit").transform;
            _root.SetParent(parent, false);
            foreach (var prefab in new[] { art.BrokenBone, art.Vertebra, art.Scapula })
                if (prefab != null) Object.Instantiate(prefab, _root, false);
            _entry = new Vector3[_root.childCount];
        }

        public void Pose(ReviveSystem.Revive rescue, Vector3 squadCenter, Vector3 outlet,
            string formation, float poseTime, float depth)
        {
            _root.gameObject.SetActive(true);
            float radius = ReviveBoneFx.RadiusFor(formation);
            if (_rescue != rescue)
            {
                _rescue = rescue;
                _radius = radius;
                _lastElapsed = rescue.Elapsed;
                foreach (Transform bone in _root) bone.gameObject.SetActive(false);
            }
            _radius = Mathf.MoveTowards(_radius, radius, 5f * (rescue.Elapsed - _lastElapsed));
            _lastElapsed = rescue.Elapsed;
            _root.position = squadCenter;
            // The last stretch spirals out of the squad orbit into the actual breach point.
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0f, depth));
            float turn = poseTime * 4.8f + 0.10f * pull;
            float entryTime = poseTime / 0.24f;
            for (int i = 0; i < _root.childCount; i++)
            {
                var bone = _root.GetChild(i);
                float launch = i * ReviveBoneFx.LaunchInterval;
                if (entryTime < launch && pull < 1f) continue;
                float phase = i * (2f * Mathf.PI / _root.childCount) + rescue.Target.Id * 2.39996f;
                float angle = phase + turn * (2f * Mathf.PI);
                var ring = squadCenter + new Vector3(_radius * Mathf.Cos(angle),
                    ReviveBoneFx.OrbitHeight + 0.22f * Mathf.Sin(angle), _radius * Mathf.Sin(angle));
                if (!bone.gameObject.activeSelf)
                {
                    var camera = Camera.main;
                    _entry[i] = camera == null ? ring : ReviveBoneFx.ScreenEdge(camera,
                        squadCenter + Vector3.up * ReviveBoneFx.OrbitHeight, phase);
                    bone.gameObject.SetActive(true);
                }
                float flight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(launch, launch + ReviveBoneFx.FlightDuration, entryTime));
                var position = Vector3.Lerp(Vector3.Lerp(_entry[i], ring, flight), outlet, pull);
                bone.SetPositionAndRotation(position, Quaternion.Euler(20f + i * 10f, -angle * Mathf.Rad2Deg, 45f + turn * 150f));
                bone.localScale = Vector3.one * Mathf.Lerp(ReviveBoneFx.BoneScale, 0.16f, pull);
            }
        }

        public void Hide() => _root.gameObject.SetActive(false);
    }
}
