using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Visuals
{
    /// <summary>
    /// The way an enemy model leaves: it plays the shared death clip over whatever its Animator was doing
    /// (no state is needed in its controller), and from the moment it hits the ground it turns to ash and
    /// dissolves. When it is gone the model is put back as it was, so the view can go back to its pool.
    /// A material without the dissolve property is swapped for a dissolving copy while this runs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathFade : MonoBehaviour
    {
        static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");
        static readonly int EdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
        static readonly int EdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int UseNormalId = Shader.PropertyToID("_UseNormalMap");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        const string DissolveShaderName = "Bone Squad/Mob PBR";

        // One dissolving copy per material that cannot dissolve by itself, shared by every model that uses it.
        static readonly Dictionary<Material, Material> Copies = new();

        /// <summary>How many models are leaving right now, over the whole game.</summary>
        public static int ActiveCount { get; private set; }

        public bool Playing { get; private set; }
        /// <summary>Seconds since the death began.</summary>
        public float Elapsed { get; private set; }
        /// <summary>0 = whole, 1 = gone.</summary>
        public float Amount { get; private set; }

        readonly List<Renderer> _renderers = new();
        readonly List<Material[]> _originals = new();
        readonly List<Color> _tints = new();
        MaterialPropertyBlock _block;
        PlayableGraph _graph;
        ArtSet.EnemyDeathLook _look;
        System.Action _done;
        float _start;

        /// <summary>Total seconds a death takes with this look, for a model that can play the clip.</summary>
        public static float Seconds(ArtSet.EnemyDeathLook look) => look.DissolveStart + look.DissolveSeconds;

        public void Play(Animator animator, ArtSet.EnemyDeathLook look, System.Action done)
        {
            if (Playing) Stop();
            _look = look; _done = done; Elapsed = 0f; Amount = 0f;
            Playing = true; ActiveCount++;
            _block ??= new MaterialPropertyBlock();

            // The clip is humanoid: a model that is not (a catapult) just dissolves where it stands.
            bool animated = look.Clip != null && animator != null && animator.isActiveAndEnabled && animator.avatar != null && animator.avatar.isHuman;
            _start = animated ? look.DissolveStart : 0f;
            if (animated)
            {
                _graph = PlayableGraph.Create("Enemy death");
                var output = AnimationPlayableOutput.Create(_graph, "death", animator);
                var clip = AnimationClipPlayable.Create(_graph, look.Clip);
                clip.SetApplyFootIK(false);
                output.SetSourcePlayable(clip);
                _graph.Play();
            }

            var shader = look.DissolveShader != null ? look.DissolveShader : Shader.Find(DissolveShaderName);
            _renderers.Clear(); _originals.Clear(); _tints.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is not SkinnedMeshRenderer && r is not MeshRenderer) continue;
                var mats = r.sharedMaterials;
                var swapped = new Material[mats.Length];
                bool any = false;
                for (int i = 0; i < mats.Length; i++) { swapped[i] = Dissolving(mats[i], shader); any |= swapped[i] != null && swapped[i].HasProperty(DissolveId); }
                if (!any) continue;
                _renderers.Add(r); _originals.Add(mats);
                _tints.Add(swapped[0] != null && swapped[0].HasProperty(BaseColorId) ? swapped[0].GetColor(BaseColorId) : Color.white);
                r.sharedMaterials = swapped;
            }
            Apply();
        }

        static Material Dissolving(Material m, Shader shader)
        {
            if (m == null || m.HasProperty(DissolveId) || shader == null || !m.HasProperty(BaseMapId)) return m;
            if (Copies.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = new Material(shader) { name = m.name + " (dissolving)", hideFlags = HideFlags.HideAndDontSave };
            copy.SetTexture(BaseMapId, m.GetTexture(BaseMapId));
            copy.SetTextureScale(BaseMapId, m.GetTextureScale(BaseMapId));
            copy.SetTextureOffset(BaseMapId, m.GetTextureOffset(BaseMapId));
            if (m.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, m.GetColor(BaseColorId));
            if (m.HasProperty(MetallicId)) copy.SetFloat(MetallicId, m.GetFloat(MetallicId));
            if (m.HasProperty(SmoothnessId)) copy.SetFloat(SmoothnessId, m.GetFloat(SmoothnessId));
            if (m.HasProperty(CullId)) copy.SetFloat(CullId, m.GetFloat(CullId));
            if (m.HasProperty(BumpMapId) && m.GetTexture(BumpMapId) != null)
            {
                copy.SetTexture(BumpMapId, m.GetTexture(BumpMapId));
                copy.SetFloat(UseNormalId, 1f); copy.EnableKeyword("_NORMALMAP");
            }
            Copies[m] = copy;
            return copy;
        }

        void Update()
        {
            if (!Playing) return;
            Elapsed += Time.deltaTime;
            Amount = _look.DissolveSeconds > 0f ? Mathf.Clamp01((Elapsed - _start) / _look.DissolveSeconds) : (Elapsed >= _start ? 1f : 0f);
            if (Amount >= 1f) { var done = _done; Stop(); done?.Invoke(); return; }
            Apply();
        }

        void Apply()
        {
            float ash = Mathf.SmoothStep(0f, 1f, Amount * 2.5f);   // it greys first, then it goes
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(DissolveId, Amount);
                _block.SetFloat(HitFlash.AmountId, 0f);   // the killing hit's flash must not stay lit on the body
                _block.SetColor(EdgeColorId, _look.EdgeColor);
                _block.SetFloat(EdgeWidthId, _look.EdgeWidth);
                _block.SetColor(BaseColorId, Color.Lerp(_tints[i], _look.AshColor, ash));
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>Ends it now and puts the model back as it was. The callback is not called.</summary>
        public void Stop()
        {
            if (!Playing) return;
            Playing = false; ActiveCount--; _done = null; Amount = 0f;
            if (_graph.IsValid()) _graph.Destroy();
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sharedMaterials = _originals[i];
                _renderers[i].SetPropertyBlock(null);
            }
            _renderers.Clear(); _originals.Clear(); _tints.Clear();
        }

        void OnDisable() => Stop();
    }
}
