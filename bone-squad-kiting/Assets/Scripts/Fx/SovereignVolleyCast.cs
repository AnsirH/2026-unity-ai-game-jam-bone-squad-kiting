using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Fx
{
    /// <summary>Plays the full warning and three-lane strike once when placed in a scene.</summary>
    public sealed class SovereignVolleyCast : MonoBehaviour
    {
        public PatternDefinition Pattern;
        public MeshRenderer Warning;

        MaterialPropertyBlock block;
        GameObject strike;
        float elapsed;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            DrawWarning(Warning, Pattern, Pattern.Shape, 0f, Pattern.Time, block);
        }

        void Update()
        {
            if (!Warning.enabled)
            {
                if (strike == null) Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            float remaining = Pattern.Time - elapsed;
            if (remaining > 0f)
            {
                DrawWarning(Warning, Pattern, Pattern.Shape, elapsed, remaining, block);
                return;
            }

            Warning.enabled = false;
            strike = Instantiate(Pattern.BurstEffect, transform, false);
            strike.transform.localPosition = Warning.transform.localPosition;
        }

        internal static void DrawWarning(MeshRenderer warning, PatternDefinition pattern, DangerShape shape,
            float elapsed, float remaining, MaterialPropertyBlock block)
        {
            var material = pattern.LaneWarningMaterial;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, material.GetFloat("_Duration")));
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / Mathf.Max(.01f, material.GetFloat("_FadeDuration"))));
            block.Clear();
            block.SetFloat("_Loop", 0f);
            block.SetFloat("_Progress", progress);
            block.SetFloat("_LethalTelegraph", pattern.LethalTelegraph ? 1f : 0f);
            block.SetFloat("_Opacity", opacity);
            warning.transform.localScale = new Vector3(2f * shape.ThreeLaneHalfSpan, 2f * shape.HalfZ, 1f);
            warning.sharedMaterial = material;
            warning.SetPropertyBlock(block);
            warning.enabled = true;
            block.Clear();
        }
    }
}
