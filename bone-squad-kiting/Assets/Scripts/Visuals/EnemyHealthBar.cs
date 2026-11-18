using UnityEngine;

namespace BoneSquadKiting.Visuals
{
    /// <summary>A camera-facing health bar with a delayed damage trail.</summary>
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] Transform fill;
        [SerializeField] SpriteRenderer damageTrail;
        [SerializeField] SpriteRenderer impact;

        const float HoldSeconds = 0.12f;
        const float DrainSeconds = 0.32f;
        const float FlashSeconds = 0.14f;

        float previous = -1f;
        float trailFrom;
        float hitTime;

        void OnDisable() => previous = -1f;

        public void Draw(Vector3 anchor, Camera camera, float current, float maximum)
        {
            if (camera == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(anchor, camera.transform.rotation);
            float fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            UpdateFill(fraction, Time.time);
        }

        void UpdateFill(float fraction, float now)
        {
            if (previous < 0f || fraction > previous)
            {
                trailFrom = fraction;
                hitTime = float.NegativeInfinity;
            }
            else if (fraction < previous)
            {
                // A second hit starts at the visible trail, even if the first is still draining.
                trailFrom = TrailFraction(previous, now);
                hitTime = now;
            }
            previous = fraction;

            fill.localScale = new Vector3(fraction, 1f, 1f);
            float trail = TrailFraction(fraction, now);
            damageTrail.enabled = trail > fraction;
            if (damageTrail.enabled) damageTrail.transform.localScale = new Vector3(trail, 1f, 1f);

            float flash = Mathf.Clamp01(1f - (now - hitTime) / FlashSeconds);
            impact.enabled = flash > 0f;
            if (!impact.enabled) return;
            float width = damageTrail.sprite.bounds.size.x;
            impact.transform.localPosition = fill.localPosition + Vector3.right * (width * fraction);
            var color = impact.color;
            color.a = flash;
            impact.color = color;
        }

        float TrailFraction(float fraction, float now)
        {
            float t = (now - hitTime - HoldSeconds) / DrainSeconds;
            return Mathf.SmoothStep(trailFrom, fraction, Mathf.Clamp01(t));
        }
    }
}
