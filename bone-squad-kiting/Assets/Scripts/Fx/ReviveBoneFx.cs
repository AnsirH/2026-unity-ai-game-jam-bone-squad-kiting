using System.Collections.Generic;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;
using UnityEngine;

namespace BoneSquadKiting.Fx
{
    public sealed class ReviveBoneFx : MonoBehaviour
    {
        const int BoneCount = 3;
        const float LaunchInterval = 0.25f;
        const float FlightDuration = 0.13f;
        const float WindupStart = 0.75f;
        const float SettleTime = 0.99f;
        const float WideOrbitRadius = 3.2f;
        const float LineOrbitRadius = 1.8f;
        const float RadiusChangeSpeed = 5f;
        const float OrbitHeight = 0.95f;
        const float BoneScale = 0.72f;
        const float CancelSeconds = 0.18f;
        static readonly int FresnelIntensity = Shader.PropertyToID("_FresnelIntensity");
        static readonly int Opacity = Shader.PropertyToID("_Opacity");

        sealed class Orbit
        {
            public ReviveSystem.Revive Revive;
            public ReviveSystem System;
            public readonly Transform[] Bones = new Transform[BoneCount];
            public readonly Vector3[] Entry = new Vector3[BoneCount];
            public int Launched;
            public float Phase;
            public float Radius;
            public float LastPoseTime;
            public float CancelledAt = -1f;
        }

        readonly List<Orbit> _orbits = new();
        GameDirector _director;
        ArtSet _art;
        MaterialPropertyBlock _fade;
        int _sequence;

        public void Bind(GameDirector director, ArtSet art)
        {
            Disconnect();
            _director = director;
            _art = art;
            if (isActiveAndEnabled) Connect();
        }

        void OnEnable() => Connect();
        void OnDisable() => Disconnect();

        void Connect()
        {
            if (_director == null || _art == null) return;
            _director.ReviveStarted += OnStarted;
            _director.ReviveCompleted += OnCompleted;
            _director.ReviveCancelled += OnCancelled;
            if (_director.Revives == null) return;
            foreach (var revive in _director.Revives.Active) StartOrbit(revive);
        }

        void Disconnect()
        {
            if (_director != null)
            {
                _director.ReviveStarted -= OnStarted;
                _director.ReviveCompleted -= OnCompleted;
                _director.ReviveCancelled -= OnCancelled;
            }
            for (int i = _orbits.Count - 1; i >= 0; i--) RemoveOrbit(i);
        }

        void OnStarted(SquadUnit reviver, SquadUnit target)
        {
            foreach (var revive in _director.Revives.Active)
                if (revive.Reviver == reviver && revive.Target == target)
                {
                    StartOrbit(revive);
                    return;
                }
        }

        void StartOrbit(ReviveSystem.Revive revive)
        {
            if (_art.BrokenBone == null || _art.Vertebra == null || _art.Scapula == null) return;
            var orbit = new Orbit
            {
                Revive = revive,
                System = _director.Revives,
                Phase = _sequence++ * 2.39996f,
                Radius = FormationRadius,
                LastPoseTime = revive.Elapsed
            };
            var center = GroundPoint(_director.Squad.Anchor);
            _orbits.Add(orbit);
            Pose(orbit, center, _director.Revives.ProgressOf(revive.Reviver));
        }

        void LaunchDueBones(Orbit orbit, Vector3 center, float progress)
        {
            if (orbit.Launched == BoneCount || progress < orbit.Launched * LaunchInterval) return;
            var camera = Camera.main;
            if (camera == null) return;
            while (orbit.Launched < BoneCount && progress >= orbit.Launched * LaunchInterval)
            {
                int i = orbit.Launched++;
                var prefab = i switch { 0 => _art.BrokenBone, 1 => _art.Vertebra, _ => _art.Scapula };
                float angle = orbit.Phase + i * (2f * Mathf.PI / BoneCount);
                var start = ScreenEdge(camera, center + Vector3.up * OrbitHeight, angle);
                var bone = Instantiate(prefab, start, Quaternion.identity, transform).transform;
                bone.localScale *= BoneScale;
                orbit.Entry[i] = start;
                orbit.Bones[i] = bone;
            }
        }

        static Vector3 ScreenEdge(Camera camera, Vector3 orbitCenter, float angle)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var edge = Vector2.one * 0.5f + 0.60f * dir / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            var ray = camera.ViewportPointToRay(edge);
            var plane = new Plane(Vector3.up, orbitCenter);
            if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance);
            return camera.ViewportToWorldPoint(new Vector3(edge.x, edge.y, camera.WorldToViewportPoint(orbitCenter).z));
        }

        static Vector3 GroundPoint(Vector2 point) => new(point.x, GroundHeight.Y(point.x, point.y), point.y);

        float FormationRadius => _director.Squad.Formation == "line" ? LineOrbitRadius : WideOrbitRadius;

        void Pose(Orbit orbit, Vector3 center, float progress)
        {
            LaunchDueBones(orbit, center, progress);
            float windup = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WindupStart, ReviveSystem.ConvergenceStart, progress));
            float pull = Mathf.InverseLerp(ReviveSystem.ConvergenceStart, SettleTime, progress);
            float converge = Mathf.SmoothStep(0f, 1f, pull * pull);
            float radius = orbit.Radius * (1f + 0.13f * windup);
            float turn = Mathf.Min(progress, SettleTime) * 1.45f + 0.10f * converge;
            var offset = orbit.Revive.AppearOffset;
            var end = GroundPoint(new Vector2(center.x + offset.x, center.z + offset.y));
            for (int i = 0; i < orbit.Launched; i++)
            {
                float flight = Mathf.Clamp01((progress - i * LaunchInterval) / FlightDuration);
                float left = 1f - flight;
                // A small overshoot lets each bone catch the moving orbit and settle into it.
                float entry = 1f - left * left * (2.15f * left - 1.15f);
                float angle = orbit.Phase + (i / (float)BoneCount + turn) * (2f * Mathf.PI);
                var orbitPoint = center + new Vector3(Mathf.Cos(angle) * radius,
                    OrbitHeight + 0.22f * Mathf.Sin(angle + orbit.Phase) + windup * 0.28f, Mathf.Sin(angle) * radius);
                var arriving = Vector3.LerpUnclamped(orbit.Entry[i], orbitPoint, entry)
                    + Vector3.up * (0.85f * Mathf.Sin(Mathf.PI * flight));
                var position = Vector3.Lerp(arriving, end, converge);
                var rotation = Quaternion.Euler(22f + i * 11f, -angle * Mathf.Rad2Deg,
                    turn * 150f + i * 40f + 145f * (1f - left * left));
                orbit.Bones[i].SetPositionAndRotation(position, rotation);
            }
        }

        void LateUpdate()
        {
            if (_director == null || _orbits.Count == 0) return;
            var center = GroundPoint(_director.Squad.Anchor);
            for (int i = _orbits.Count - 1; i >= 0; i--)
            {
                var orbit = _orbits[i];
                if (orbit.System != _director.Revives)
                {
                    RemoveOrbit(i);
                    continue;
                }
                if (orbit.CancelledAt >= 0f)
                {
                    float fade = Mathf.Clamp01((_director.Elapsed - orbit.CancelledAt) / CancelSeconds);
                    if (fade >= 1f || _director.Phase != GamePhase.Play) RemoveOrbit(i);
                    else FadeCancelled(orbit, fade);
                    continue;
                }
                if (!orbit.System.Active.Contains(orbit.Revive))
                {
                    RemoveOrbit(i);
                    continue;
                }
                float elapsed = orbit.Revive.Elapsed;
                orbit.Radius = Mathf.MoveTowards(orbit.Radius, FormationRadius, RadiusChangeSpeed * (elapsed - orbit.LastPoseTime));
                orbit.LastPoseTime = elapsed;
                Pose(orbit, center, orbit.System.ProgressOf(orbit.Revive.Reviver));
            }
        }

        void OnCancelled(SquadUnit reviver, SquadUnit target)
        {
            int index = FindOrbit(reviver, target);
            if (index < 0) return;
            var orbit = _orbits[index];
            orbit.CancelledAt = _director.Elapsed;
            FadeCancelled(orbit, 0f);
        }

        void FadeCancelled(Orbit orbit, float fade)
        {
            _fade ??= new MaterialPropertyBlock();
            _fade.SetFloat(FresnelIntensity, 0f);
            _fade.SetFloat(Opacity, 1f - fade);
            for (int i = 0; i < orbit.Launched; i++) orbit.Bones[i].GetComponent<Renderer>().SetPropertyBlock(_fade);
        }

        void OnCompleted(SquadUnit reviver, SquadUnit target)
        {
            int index = FindOrbit(reviver, target);
            if (index < 0) return;
            var end = GroundPoint(new Vector2(target.X, target.Z));
            var orbit = _orbits[index];
            for (int i = 0; i < orbit.Launched; i++) orbit.Bones[i].position = end;
            RemoveOrbit(index);
        }

        int FindOrbit(SquadUnit reviver, SquadUnit target)
        {
            for (int i = _orbits.Count - 1; i >= 0; i--)
                if (_orbits[i].Revive.Reviver == reviver && _orbits[i].Revive.Target == target && _orbits[i].CancelledAt < 0f) return i;
            return -1;
        }

        void RemoveOrbit(int index)
        {
            var orbit = _orbits[index];
            for (int i = 0; i < orbit.Launched; i++)
            {
                var bone = orbit.Bones[i];
                bone.gameObject.SetActive(false);
                Destroy(bone.gameObject);
            }
            _orbits.RemoveAt(index);
        }
    }
}
