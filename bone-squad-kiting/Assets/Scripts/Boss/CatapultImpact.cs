using UnityEngine;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.Bosses
{
    public sealed class CatapultImpact : MonoBehaviour
    {
        public LineRenderer Shockwave;
        public ParticleSystem Chips;
        const int Points = 48;
        const float Seconds = 0.48f;
        readonly Vector3[] _edge = new Vector3[Points];
        float _elapsed;

        public void Show(float radius)
        {
            // Sample the outer edge once so the full-size ring meets the terrain.
            var center = transform.position;
            for (int i = 0; i < Points; i++)
            {
                float angle = i * (2f * Mathf.PI / Points);
                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.z + Mathf.Sin(angle) * radius;
                _edge[i] = new Vector3(x, GroundHeight.Y(x, z) + 0.06f, z);
            }
            Shockwave.positionCount = Points;
            DrawRing(0f);
            Chips.transform.localScale = Vector3.one * radius;
            Chips.Play();
            Destroy(gameObject, 0.85f);
        }

        void Update()
        {
            if (!Shockwave.enabled) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Seconds);
            DrawRing(t);
            if (t >= 1f) Shockwave.enabled = false;
        }

        void DrawRing(float t)
        {
            float expansion = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.75f), 2f);
            Vector3 center = transform.position + Vector3.up * 0.06f;
            for (int i = 0; i < Points; i++)
                Shockwave.SetPosition(i, Vector3.Lerp(center, _edge[i], expansion));
            Shockwave.widthMultiplier = Mathf.Lerp(0.20f, 0.035f, t);
            var color = Color.Lerp(new Color(1f, 0.86f, 0.6f), new Color(0.54f, 0.41f, 0.32f, 0f), t);
            Shockwave.startColor = Shockwave.endColor = color;
        }
    }
}
