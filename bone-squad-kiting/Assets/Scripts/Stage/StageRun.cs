using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Runs several stages in one scene. Every stage's map sits in the scene; only the current one is
    /// switched on. Each map carries its own places (a StageStart with its end point, walk outlines, mini
    /// boss markers, camera gates); this list carries the game side of each stage (its waves and how it is
    /// cleared). The game kit — squad, camera, HUD — is brought in once and reused for every stage.
    ///
    /// Clearing a stage fades the screen out, swaps the maps, rebuilds the walk area, seats a fresh full
    /// squad on the next start point, and only then fades back in. After the last map, NextScene is loaded.
    /// </summary>
    [DefaultExecutionOrder(-1100)]   // before the maps' StageStart markers, which then leave the kit to this
    public class StageRun : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("The stage's map object in this scene. It needs a StageStart somewhere inside.")]
            public GameObject Map;
            [Tooltip("Shown when the stage begins and on its clear screen.")]
            public string Title = "Stage";
            [Tooltip("This stage's waves. Empty = no waves.")]
            public WavePlan WavePlan;
            public StageClearRule ClearRule = StageClearRule.ReachEnd;
            [Tooltip("The game camera's turn around Y (its Rotation Y) while this stage is played. 0 = looking toward +Z.")]
            public float CameraYaw = 0f;
            [Tooltip("Post-processing look of this stage (a Volume Profile). Empty = the project's default look.")]
            public VolumeProfile Volume;
        }

        public List<Entry> Stages = new();
        [Tooltip("Which stage to begin on (0 = the first). For testing a later stage directly.")]
        public int StartStage;
        [Tooltip("Scene loaded after the last stage is cleared. Empty = the last stage ends the game.")]
        public string NextScene = "";
        [Tooltip("Seconds the screen takes to go dark, and again to come back.")]
        public float FadeSeconds = 0.35f;
        [Tooltip("Show the main menu (new game / continue / quit) on this scene's title screen.")]
        public bool ShowMainMenu = true;

        /// <summary>Index of the stage being played.</summary>
        public int Current { get; private set; }
        public Entry CurrentEntry => Current >= 0 && Current < Stages.Count ? Stages[Current] : null;
        public bool IsLast => Current >= Stages.Count - 1;
        /// <summary>True from the moment the screen starts going dark until it is fully back.</summary>
        public bool Switching { get; private set; }
        /// <summary>The game kit, while playing.</summary>
        public StageKit Kit { get; private set; }
        /// <summary>Raised when a stage has been set up (index), before the screen comes back.</summary>
        public event System.Action<int> StageEntered;

        /// <summary>The global volume that carries the current stage's post-processing; null until a stage has one.</summary>
        public Volume StageVolume { get; private set; }

        WaveSpawner _waves;
        CanvasGroup _fade;

        void Awake()
        {
            if (Stages.Count == 0) { Debug.LogError("StageRun: the stage list is empty.", this); return; }
            Current = Mathf.Clamp(StartStage, 0, Stages.Count - 1);
            ShowOnly(Current);
            Kit = StageKit.Spawn(gameObject.scene);
            if (Kit == null) return;
            Kit.Director.Run = this;
            Wire(Current);
            Kit.gameObject.SetActive(true);      // StageSetup bakes the walk area and seats the squad
            FocusCamera();
            StageEntered?.Invoke(Current);
            if (ShowMainMenu)
            {
                Kit.Director.HasMainMenu = true;
                gameObject.AddComponent<BoneSquadKiting.UI.MainMenu>().Run = this;
            }
        }

        /// <summary>Switch the maps so only one stage's is on. Also usable in the editor to look at one map.</summary>
        public void ShowOnly(int index)
        {
            for (int i = 0; i < Stages.Count; i++)
            {
                var map = Stages[i].Map;
                if (map != null && map.activeSelf != (i == index)) map.SetActive(i == index);
            }
        }

        [ContextMenu("Show Only The Start Stage's Map")]
        void ShowStartStage() => ShowOnly(Mathf.Clamp(StartStage, 0, Mathf.Max(0, Stages.Count - 1)));

        /// <summary>
        /// Use another wave plan for a stage. If that stage is being played, its waves follow from the next one.
        /// </summary>
        public void SetWavePlan(int stage, WavePlan plan)
        {
            if (stage < 0 || stage >= Stages.Count) return;
            Stages[stage].WavePlan = plan;
            if (stage == Current && Kit != null) WireWaves(Stages[stage]);
        }

        // Point the kit at one stage: its places come from the map, its rules from the list.
        void Wire(int index)
        {
            var entry = Stages[index];
            var start = entry.Map != null ? entry.Map.GetComponentInChildren<StageStart>(true) : null;
            if (start == null) Debug.LogError($"StageRun: stage {index + 1} has no StageStart in its map.", this);
            Kit.Setup.StartPoint = start != null ? start.transform : null;
            Kit.Setup.EndPoint = start != null ? start.EndPoint : null;
            if (start != null) Kit.Setup.EndRadius = start.EndRadius;
            Kit.Director.StageTitle = entry.Title;
            Kit.Director.ClearRule = entry.ClearRule;
            Kit.Director.HasBoss = false;
            // The camera keeps its downward tilt; only which way it faces is the stage's.
            var cam = Kit.Cam.transform;
            cam.rotation = Quaternion.Euler(cam.eulerAngles.x, entry.CameraYaw, 0f);
            WireVolume(entry);
            WireWaves(entry);
        }

        // One global volume for the whole run; each stage puts its own profile in it. Swapped while the
        // screen is dark, so the look never changes in view.
        void WireVolume(Entry entry)
        {
            if (entry.Volume == null) { if (StageVolume != null) StageVolume.gameObject.SetActive(false); return; }
            if (StageVolume == null)
            {
                var go = new GameObject("Stage Volume");
                go.transform.SetParent(transform, false);
                StageVolume = go.AddComponent<Volume>();
                StageVolume.isGlobal = true;
                StageVolume.priority = 10f;      // over any default global volume
            }
            StageVolume.sharedProfile = entry.Volume;
            StageVolume.gameObject.SetActive(true);
        }

        void WireWaves(Entry entry)
        {
            if (_waves == null) _waves = Kit.Setup.GetComponent<WaveSpawner>();
            if (entry.WavePlan == null) { Kit.Director.Waves = null; return; }
            if (_waves == null) _waves = Kit.Setup.gameObject.AddComponent<WaveSpawner>();
            _waves.Plan = entry.WavePlan;
            Kit.Director.Waves = _waves;
        }

        void FocusCamera()
        {
            Kit.Cam.LookAt = Kit.Setup.Squad.Anchor;
            Kit.Cam.Apply();
        }

        /// <summary>
        /// Set a stage up, at once and without the fade: swap the maps, rebuild the walk area, clear the
        /// field and start with a fresh full squad on the stage's start point.
        /// </summary>
        public void Enter(int index)
        {
            if (Kit == null || index < 0 || index >= Stages.Count) return;
            Current = index;
            ShowOnly(index);
            Physics.SyncTransforms();            // the new map's colliders must be in place before the bake
            Wire(index);
            Kit.Setup.Apply();
            Kit.Director.Restart();
            FocusCamera();
            StageEntered?.Invoke(index);
        }

        /// <summary>Go on to the next stage through the fade. Does nothing on the last stage or mid-switch.</summary>
        public void Advance()
        {
            if (Switching || IsLast || Kit == null) return;
            StartCoroutine(SwitchTo(Current + 1));
        }

        /// <summary>
        /// Start playing from a stage's start point with a full squad (the main menu's new game and
        /// continue). A number past the last map means the scene that follows the maps.
        /// </summary>
        public void StartAt(int index)
        {
            if (Switching || Kit == null) return;
            if (index >= Stages.Count)
            {
                if (string.IsNullOrEmpty(NextScene)) index = Stages.Count - 1;
                else { Game.GameDirector.LoadStageScene(NextScene); return; }
            }
            index = Mathf.Max(0, index);
            if (index == Current) Kit.Director.Restart();      // already set up and on screen
            else StartCoroutine(SwitchTo(index));
        }

        IEnumerator SwitchTo(int index)
        {
            Switching = true;
            Kit.Director.Frozen = true;
            yield return Fade(0f, 1f);
            Enter(index);
            // Give the new map two frames under the dark screen: terrain, lights and the squad's models
            // settle before anyone sees them.
            yield return null;
            yield return null;
            yield return Fade(1f, 0f);
            Kit.Director.Frozen = false;         // the stage starts moving once it is fully visible
            Switching = false;
        }

        IEnumerator Fade(float from, float to)
        {
            if (_fade == null) _fade = BuildFade();
            _fade.gameObject.SetActive(true);
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            _fade.alpha = to;
            if (to <= 0f) _fade.gameObject.SetActive(false);
        }

        // A black sheet over everything, HUD included. It also swallows clicks while it is up.
        CanvasGroup BuildFade()
        {
            var go = new GameObject("Stage Fade", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var image = new GameObject("Black", typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(go.transform, false);
            image.color = Color.black;
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }
    }
}
