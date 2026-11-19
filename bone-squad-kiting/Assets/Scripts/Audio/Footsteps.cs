using UnityEngine;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.Audio
{
    /// <summary>What the squad is walking on. Each has its own set of step sounds.</summary>
    public enum FootstepSurface { Dirt, Grass, Brick }

    /// <summary>
    /// The squad's footsteps: while the formation is travelling, one single boot step plays at a walking
    /// beat, picked at random from the steps recorded for the ground under the squad. It is one sound for
    /// the whole squad, not one per soldier.
    /// </summary>
    public sealed class Footsteps
    {
        public const int StepsPerSurface = 3;
        public float Interval = 0.3f;         // seconds between steps while moving
        public float MinSpeed = 0.5f;         // slower than this (metres per second) is standing still

        float _untilNext;
        int _last = -1;

        /// <summary>Sound ID of one step: squad.footstep.dirt.1 … squad.footstep.brick.3.</summary>
        public static string Id(FootstepSurface surface, int index) =>
            "squad.footstep." + (surface == FootstepSurface.Grass ? "grass" : surface == FootstepSurface.Brick ? "brick" : "dirt") + "." + (index + 1);

        /// <summary>
        /// Call once per game step with how far the formation's center moved in it and where it is now.
        /// The stage's own surface is used wherever the ground itself does not say what it is.
        /// </summary>
        public void Tick(float moved, float dt, Vector2 at, FootstepSurface stageSurface)
        {
            if (dt <= 0f || moved / dt < MinSpeed) { _untilNext = 0f; return; }   // the first step sounds as soon as it sets off
            _untilNext -= dt;
            if (_untilNext > 0f) return;
            _untilNext = Interval;
            int pick = Random.Range(0, StepsPerSurface);
            if (pick == _last) pick = (pick + 1) % StepsPerSurface;               // never the same step twice in a row
            _last = pick;
            SoundManager.Current?.PlaySfx(Id(SurfaceAt(at, stageSurface), pick));
        }

        /// <summary>
        /// The ground at a point: on a Terrain, the strongest painted layer there (by its name); on a mesh,
        /// its material's name, or else its object's name; anything that says none of these is the stage's own surface.
        /// </summary>
        public static FootstepSurface SurfaceAt(Vector2 at, FootstepSurface fallback)
        {
            if (GroundHeight.Mask.value == 0) return fallback;
            var from = new Vector3(at.x, GroundHeight.RayTop, at.y);
            if (!Physics.Raycast(from, Vector3.down, out var hit, GroundHeight.RayTop * 2f, GroundHeight.Mask, QueryTriggerInteraction.Ignore)) return fallback;

            if (hit.collider is TerrainCollider && hit.collider.TryGetComponent<Terrain>(out var terrain) && terrain.terrainData != null)
            {
                var data = terrain.terrainData;
                var layers = data.terrainLayers;
                if (layers == null || layers.Length == 0) return fallback;
                var local = hit.point - terrain.transform.position;
                int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                var weights = data.GetAlphamaps(x, z, 1, 1);
                int best = 0;
                for (int i = 1; i < layers.Length && i < weights.GetLength(2); i++)
                    if (weights[0, 0, i] > weights[0, 0, best]) best = i;
                return layers[best] != null ? FromName(layers[best].name, fallback) : fallback;
            }

            var renderer = hit.collider.GetComponent<Renderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;
            // A ground collider without a look of its own is told by its object's name ("Flagstone Pavement").
            return material != null ? FromName(material.name, FromName(hit.collider.name, fallback)) : FromName(hit.collider.name, fallback);
        }

        /// <summary>Guesses the surface from an asset name ("MeadowGrass", "Road Soil", "Gorge Stone" …).</summary>
        public static FootstepSurface FromName(string name, FootstepSurface fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            var n = name.ToLowerInvariant();
            if (n.Contains("grass") || n.Contains("meadow") || n.Contains("moss") || n.Contains("lawn")) return FootstepSurface.Grass;
            if (n.Contains("brick") || n.Contains("stone") || n.Contains("rock") || n.Contains("tile") || n.Contains("floor") || n.Contains("cobble") || n.Contains("pave") || n.Contains("flagstone")) return FootstepSurface.Brick;
            if (n.Contains("soil") || n.Contains("dirt") || n.Contains("earth") || n.Contains("mud") || n.Contains("sand") || n.Contains("road")) return FootstepSurface.Dirt;
            return fallback;
        }
    }
}
