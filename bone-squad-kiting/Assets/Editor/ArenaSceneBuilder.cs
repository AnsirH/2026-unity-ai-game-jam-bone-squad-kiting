using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;
using BoneSquadKiting.Game;
using BoneSquadKiting.Input;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;
using BoneSquadKiting.UI;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Arena.unity from scratch with placeholder visuals and all gameplay wiring.
    /// Rebuilding overwrites the scene, so hand edits to it are lost — change this script instead
    /// (or stop using it once the scene is hand-authored).
    /// Run from the menu, or headless: -executeMethod BoneSquadKiting.EditorTools.ArenaSceneBuilder.Build
    /// </summary>
    public static class ArenaSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";
        internal const string MatDir = "Assets/Prototype/Materials";
        internal const string EnemyLayer = "Enemy";
        internal const string GroundLayer = "Ground", ObstacleLayer = "Obstacle";

        // Camera framing. Tune here (or on the camera, once the scene is hand-authored).
        internal const float CameraPitch = 60f;      // degrees down from horizontal
        internal const float CameraYaw = 0f;         // degrees turned around Y; 0 = straight on, boss directly ahead
        internal const float CameraFov = 30f;        // vertical field of view
        internal const float CameraDistance = 34f;   // from the ground point at the center of the view
        // Start between the squad (z = 3.4) and the boss (z = -7.4) so both are in frame.
        static readonly Vector2 CameraStartLookAt = new(0f, -0.1f);

        // Ranges as tuned by hand in the scene (2026-11-18). The squad has to close in on the boss to fire.
        internal const float UnitFireRange = 5.5f;
        internal const float SquadEngageRange = 5f;

        /// <summary>
        /// A placeholder progress stage: a corridor with a few obstacles, a start and an end point, endless
        /// waves that grow toward the end and, on most stages, a mini boss near the end. Every number here is
        /// a starting value, not a tuned one. Real maps will replace these scenes.
        /// </summary>
        class StageSpec
        {
            public string Name, Title, Next;
            public float Width = 18f, Length = 64f;
            public bool ReachEndOnly;                       // clear by reaching the end point even if mini bosses live
            public Placement[] MiniBosses = new Placement[0];
            public float GateAlong = -1f;                   // >= 0: a camera gate here (0 = start, 1 = end)...
            public int[] GateUntil;                         // ...closed until these mini boss placements are dead
            public float Interval0 = 12f, Interval1 = 5f;   // seconds between waves at the start / at the end
            public float Count0 = 8f, Count1 = 30f;         // mobs per wave at the start / at the end
            public string Path => $"Assets/Scenes/Test_{Name}.unity";   // test scenes: the game itself runs in Game.unity
        }

        /// <summary>One mini boss on a stage. Along = 0 at the start point, 1 at the end point.</summary>
        class Placement
        {
            public string Elite; public float X, Along;
            public bool FromStart;          // a fixed cannon that is simply there
            public int[] After;             // indices of placements that must die before this one activates
            public Placement(string elite, float x, float along, bool fromStart = false, int[] after = null)
            { Elite = elite; X = x; Along = along; FromStart = fromStart; After = after; }
        }

        // Stage lineup (decisions #45, #46). Positions and wave numbers are placeholders.
        //  1: three catapults along the way, the dark-flame mage at the end
        //  2: the shadow swordsman, three catapults; once those three are down, one more catapult and the spear thrower
        //  3: many catapults and the telekinetic mage, but the stage is cleared by reaching the end point
        //  4: the telekinetic mage twice
        //  5: the boss arena
        static readonly StageSpec[] Stages =
        {
            new() { Name = "Stage1", Title = "스테이지 1", Next = "", Length = 84f, Interval0 = 12f, Interval1 = 7f, Count0 = 6f, Count1 = 18f,
                    MiniBosses = new[] { new Placement("Catapult", -5f, 0.28f, true), new Placement("Catapult", 5f, 0.45f, true),
                                         new Placement("Catapult", 0f, 0.64f, true), new Placement("DarkFlameMage", 0f, 0.9f) },
                    GateAlong = 0.74f, GateUntil = new[] { 0, 1, 2 } },
            new() { Name = "Stage2", Title = "스테이지 2", Next = "", Length = 96f, Interval0 = 11f, Interval1 = 6f, Count0 = 8f, Count1 = 24f,
                    MiniBosses = new[] { new Placement("ShadowSwordsman", 0f, 0.22f),
                                         new Placement("Catapult", -5f, 0.42f, true), new Placement("Catapult", 5f, 0.52f, true), new Placement("Catapult", 6f, 0.62f, true),
                                         new Placement("Catapult", 4f, 0.8f, true, new[] { 1, 2, 3 }), new Placement("SpearThrower", 0f, 0.9f, false, new[] { 1, 2, 3 }) },
                    GateAlong = 0.72f, GateUntil = new[] { 1, 2, 3 } },
            new() { Name = "Stage3", Title = "스테이지 3", Next = "", Length = 108f, ReachEndOnly = true, Interval0 = 9f, Interval1 = 3.5f, Count0 = 12f, Count1 = 40f,
                    MiniBosses = new[] { new Placement("Catapult", -5f, 0.2f, true), new Placement("Catapult", 5f, 0.32f, true), new Placement("Catapult", -5f, 0.46f, true),
                                         new Placement("Catapult", 5f, 0.58f, true), new Placement("Catapult", 0f, 0.72f, true), new Placement("TelekineticMage", 0f, 0.9f) },
                    GateAlong = 0.8f, GateUntil = new[] { 0, 1, 2, 3, 4 } },
            new() { Name = "Stage4", Title = "스테이지 4", Next = "", Length = 84f, Interval0 = 10f, Interval1 = 5f, Count0 = 10f, Count1 = 30f,
                    MiniBosses = new[] { new Placement("TelekineticMageGate", 0f, 0.45f), new Placement("TelekineticMageQuadrant", 0f, 0.9f) } },
        };
        const string ArenaTitle = "스테이지 5";

        [MenuItem("BoneSquadKiting/Rebuild Arena Scene")]
        public static void Build() => BuildScene(ScenePath, null);

        /// <summary>The four test stages (Test_Stage1..4) and the arena. The arena is left open.</summary>
        [MenuItem("BoneSquadKiting/Rebuild All Stage Scenes")]
        public static void BuildAllStages()
        {
            foreach (var spec in Stages) BuildScene(spec.Path, spec);
            BuildScene(ScenePath, null);
        }

        static void BuildScene(string scenePath, StageSpec mobStage)
        {
            int enemyLayer = EnsureLayer(EnemyLayer);
            EnsureFolder("Assets", "Prototype"); EnsureFolder("Assets/Prototype", "Materials");

            var ground = Lit("Ground", Hex("#0d2434"));
            var unit = Lit("Unit", Hex("#35e0d0"), Hex("#35e0d0") * 0.6f);
            var boss = Lit("Boss", Hex("#c22a2a"), Hex("#ff3b30") * 1.6f);
            var shot = Lit("Shot", Hex("#ffe27a"), Hex("#ffd24a") * 2.5f);
            var telegraph = Upsert($"{MatDir}/Telegraph.mat", TelegraphShader());
            telegraph.color = Color.white;
            EditorUtility.SetDirty(telegraph);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ground: Unity's plane is 10x10, scaled to the prototype's hard limits.
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.localScale = new Vector3(
                (GameConstants.LimitXMax - GameConstants.LimitXMin) / 10f, 1f,
                (GameConstants.LimitZMax - GameConstants.LimitZMin) / 10f);
            plane.GetComponent<Renderer>().sharedMaterial = ground;
            plane.layer = EnsureLayer(GroundLayer);   // its collider is the stage's walkable floor

            // Quarter view: a perspective camera looking down at a steep angle (framing modelled on
            // Shape of Dreams: ~60 degrees, narrow FOV, no Y turn). It sits on the +z side, so the boss
            // (negative z) is at the far end and world +x is screen-left.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = false;
            cam.fieldOfView = CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#050d17");
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 200f;
            camGo.transform.rotation = Quaternion.Euler(CameraPitch, 180f + CameraYaw, 0f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = Hex("#cfe4ff");
            lightGo.transform.rotation = Quaternion.Euler(55f, 150f, 0f); // lit from behind the camera, no glare off the floor

            // Global post: bloom for the emissive units/boss/shots, vignette for the prototype's mood.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/ArenaVolume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/Settings/ArenaVolume.asset");
            }
            var bloom = profile.TryGet<Bloom>(out var b) ? b : profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f); bloom.intensity.Override(0.8f);
            var vignette = profile.TryGet<Vignette>(out var vg) ? vg : profile.Add<Vignette>(true);
            vignette.intensity.Override(0.35f);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // Gameplay root: director + squad + input.
            var game = new GameObject("Game");
            var squad = game.AddComponent<FormationController>();
            squad.Formations = new List<FormationDefinition>
            {
                Load<FormationDefinition>("Assets/Data/Formations/dense.asset"),
                Load<FormationDefinition>("Assets/Data/Formations/line.asset"),
                Load<FormationDefinition>("Assets/Data/Formations/ring.asset"),
                Load<FormationDefinition>("Assets/Data/Formations/cross.asset"),
            };
            var director = game.AddComponent<GameDirector>();
            director.Squad = squad;
            director.UnitFireRange = UnitFireRange; director.SquadEngageRange = SquadEngageRange;
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/GameTuning.asset");
            director.Tuning = tuning;   // the shared numbers; the two fields above are only the fallback
            director.Sequence = Load<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
            // RTS camera: it does not follow the squad; the mouse at a screen edge slides it over the arena.
            var scroll = camGo.AddComponent<EdgeScrollCamera>();
            scroll.Distance = CameraDistance; scroll.LookAt = CameraStartLookAt;
            scroll.Tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/GameTuning.asset");
            scroll.Apply();

            // Stage data: walkable grid from the floor collider, a no-go square around the boss, start point.
            var stageGo = new GameObject("Stage");
            var baker = stageGo.AddComponent<WalkGridBaker>();
            baker.Size = new Vector2(GameConstants.LimitXMax - GameConstants.LimitXMin, GameConstants.LimitZMax - GameConstants.LimitZMin);
            baker.GroundMask = 1 << EnsureLayer(GroundLayer); baker.ObstacleMask = 1 << EnsureLayer(ObstacleLayer);

            var bossArea = new GameObject("Boss Area (Exclude)");
            bossArea.transform.SetParent(stageGo.transform, false);
            bossArea.AddComponent<WalkOutline>().Mode = WalkOutlineMode.Exclude;
            const float keepOut = 1.7f;
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                var point = new GameObject("Point");
                point.transform.SetParent(bossArea.transform, false);
                point.transform.position = new Vector3(GameConstants.BossX + corner.x * keepOut, 0f, GameConstants.BossZ + corner.y * keepOut);
            }

            var startPoint = new GameObject("Start Point");
            startPoint.transform.SetParent(stageGo.transform, false);
            startPoint.transform.position = new Vector3(squad.Anchor.x, 0f, squad.Anchor.y);

            var stage = stageGo.AddComponent<StageSetup>();
            stage.Ground = baker; stage.Squad = squad; stage.Cam = scroll; stage.StartPoint = startPoint.transform;

            game.AddComponent<RangeGizmos>().Director = director; // Scene-view circles for tuning the two ranges

            var input = game.AddComponent<SquadInputController>();
            input.Cam = cam; input.Director = director; input.EnemyMask = 1 << enemyLayer;

            var squadView = new GameObject("Squad View").AddComponent<SquadView>();
            squadView.Squad = squad; squadView.UnitMaterial = unit;
            var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
            squadView.Art = art;
            // Pre-place the squad in its starting layout so it is visible without entering Play mode.
            var startSlots = FormationSlots.Generate(squad.Formations[0], GameConstants.SquadSize);
            for (int i = 0; i < GameConstants.SquadSize; i++)
            {
                var u = SquadView.BuildUnit(i, art, squadView.UnitScale, squadView.LeaderScale, unit);
                u.transform.SetParent(squadView.transform, false);
                bool model = u.GetComponent<MeshFilter>() == null;   // a model stands on the ground; a capsule floats at its center
                u.transform.position = new Vector3(squad.Anchor.x + startSlots[i].x, model ? 0f : squadView.UnitScale, squad.Anchor.y + startSlots[i].y);
                if (model) u.transform.rotation = Quaternion.Euler(0f, 180f + (art != null ? art.ModelYawOffset : 0f), 0f);   // facing the far end
            }

            var shotView = new GameObject("Shot View").AddComponent<ShotView>();
            shotView.Director = director; shotView.ShotMaterial = shot;

            // Boss: the collider stays so a right click can pick it.
            var bossGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bossGo.name = "Boss";
            bossGo.layer = enemyLayer;
            bossGo.transform.localScale = new Vector3(2.4f, 1.2f, 2.4f);
            bossGo.transform.position = new Vector3(GameConstants.BossX, 1.2f, GameConstants.BossZ);
            bossGo.GetComponent<Renderer>().sharedMaterial = boss;
            bossGo.AddComponent<EnemyRef>();
            director.BossView = bossGo.AddComponent<BossView>();

            var teleGo = new GameObject("Telegraph");
            var tele = teleGo.AddComponent<TelegraphView>();
            tele.Director = director; tele.Material = telegraph;

            // Debug overlay (F1) for playtesting, plus the player HUD.
            var testHud = game.AddComponent<TestHud>();
            testHud.Director = director; testHud.Input = input;
            HudBuilder.Build(director, out var flashImage);

            // Game feel: shake, flash, hit particles, blast ring.
            var particleMat = Upsert($"{MatDir}/Particle.mat", Shader.Find("Sprites/Default"));
            particleMat.color = Color.white; EditorUtility.SetDirty(particleMat);

            var particlesGo = new GameObject("Hit Particles");
            var particles = particlesGo.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false; main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 0.55f, 0.25f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = particles.emission; emission.enabled = false;   // bursts come from JuiceFx.Emit
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);                    // spray upward
            particlesGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;

            var ringGo = new GameObject("Blast Ring");
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.loop = true; ring.useWorldSpace = true; ring.widthMultiplier = 0.14f;
            ring.sharedMaterial = particleMat; ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.positionCount = 0; ring.enabled = false;

            var fx = game.AddComponent<JuiceFx>();
            fx.Director = director; fx.Cam = scroll; fx.FlashImage = flashImage;
            fx.HitParticles = particles; fx.Ring = ring;
            AddPresentationManager(game, scroll);

            if (mobStage != null) ConvertToMobStage(mobStage, plane, baker, stage, director, squad, squadView, scroll, bossGo, bossArea, teleGo, startPoint);
            else { director.StageTitle = ArenaTitle; director.NextScene = ""; director.Stage = stage; }

            EditorSceneManager.SaveScene(scene, scenePath);

            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("ArenaSceneBuilder: wrote " + scenePath);
        }

        /// <summary>
        /// Build order = play order: the game scene (stages 1.. in one scene; it opens on the title screen),
        /// then the arena. The Test_Stage scenes are for testing and are not built.
        /// </summary>
        internal static void SetBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (System.IO.File.Exists(StageKitBuilder.GamePath)) scenes.Add(new EditorBuildSettingsScene(StageKitBuilder.GamePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Reshape the freshly built arena into a corridor stage fought against rounds of mobs.
        static void ConvertToMobStage(StageSpec spec, GameObject plane, WalkGridBaker baker, StageSetup stage, GameDirector director,
            FormationController squad, SquadView squadView, EdgeScrollCamera scroll, GameObject bossGo, GameObject bossArea,
            GameObject teleGo, GameObject startPoint)
        {
            float width = spec.Width, length = spec.Length;
            var start = new Vector2(0f, length / 2f - 6f);   // near end; the camera looks toward -z, so "forward" is up-screen
            var end = new Vector2(0f, -(length / 2f - 6f));
            director.StageTitle = spec.Title; director.NextScene = spec.Next;

            plane.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
            baker.Size = new Vector2(width, length);

            Object.DestroyImmediate(bossGo); Object.DestroyImmediate(bossArea);   // the telegraph view stays: the mini boss uses it
            director.BossView = null; director.HasBoss = false;
            director.ClearRule = spec.ReachEndOnly ? StageClearRule.ReachEnd : StageClearRule.DefeatMiniBossThenReachEnd;
            director.Stage = stage;

            // Move the squad and its pre-placed units to the start point.
            var delta = start - squad.Anchor;
            squad.StartAnchor = squad.Anchor = squad.Destination = start;
            foreach (Transform unit in squadView.transform) unit.position += new Vector3(delta.x, 0f, delta.y);
            startPoint.transform.position = new Vector3(start.x, 0f, start.y);
            // The camera's limits must cover this stage before its start position is applied, or the
            // position gets clamped to the arena's size.
            scroll.BoundsMin = new Vector2(-width / 2f, -length / 2f); scroll.BoundsMax = new Vector2(width / 2f, length / 2f);
            scroll.LookAt = start + new Vector2(0f, -4f); scroll.Apply();

            var endPoint = new GameObject("End Point");
            endPoint.transform.SetParent(stage.transform, false);
            endPoint.transform.position = new Vector3(end.x, 0f, end.y);
            stage.EndPoint = endPoint.transform; stage.EndRadius = 3f;

            // A few blocks to walk (and path) around.
            var obstacleMat = Lit("Obstacle", Hex("#2b3d52"));
            int obstacleLayer = EnsureLayer(ObstacleLayer);
            // Laid out as fractions of the corridor's half-length so longer stages spread them out.
            float h = length / 2f;
            var blocks = new[] { new Vector4(-4.5f, h * 0.44f, 6f, 2f), new Vector4(5f, h * 0.12f, 5f, 2f), new Vector4(-3f, -h * 0.19f, 8f, 2f), new Vector4(5.5f, -h * 0.47f, 4f, 4f) };
            foreach (var b in blocks)   // x, z, size x, size z
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Obstacle";
                block.transform.SetParent(stage.transform, false);
                block.transform.position = new Vector3(b.x, 0.75f, b.y);
                block.transform.localScale = new Vector3(b.z, 1.5f, b.w);
                block.layer = obstacleLayer;
                block.GetComponent<Renderer>().sharedMaterial = obstacleMat;
            }

            // Endless waves that grow as the squad advances toward the end point.
            var waves = stage.gameObject.AddComponent<WaveSpawner>();
            waves.Mob = Load<MobDefinition>("Assets/Data/Mobs/Grunt.asset");
            waves.Interval = AnimationCurve.Linear(0f, spec.Interval0, 1f, spec.Interval1);
            waves.Count = AnimationCurve.Linear(0f, spec.Count0, 1f, spec.Count1);
            waves.Plan = WavePlanFor(spec.Name, WavePlanGuidIn(spec.Path));
            director.Waves = waves;

            // Mini bosses, placed along the corridor between the start and the end point.
            var spawns = new MiniBossSpawn[spec.MiniBosses.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                var pl = spec.MiniBosses[i];
                var go = new GameObject($"Mini Boss {i + 1} ({pl.Elite})");
                go.transform.SetParent(stage.transform, false);
                go.transform.position = new Vector3(pl.X, 0f, Mathf.Lerp(start.y, end.y, pl.Along));
                spawns[i] = go.AddComponent<MiniBossSpawn>();
                spawns[i].Definition = Load<EliteDefinition>($"Assets/Data/Elites/{pl.Elite}.asset");
                spawns[i].PresentFromStart = pl.FromStart;
                spawns[i].TriggerRadius = 12f;
            }
            for (int i = 0; i < spawns.Length; i++)
            {
                var after = spec.MiniBosses[i].After;
                if (after == null) continue;
                spawns[i].ActivateAfter = System.Array.ConvertAll(after, k => spawns[k]);
            }

            // A camera gate: the view cannot be scrolled past this line until the listed mini bosses are dead.
            if (spec.GateAlong >= 0f && spec.GateUntil != null)
            {
                var gateGo = new GameObject("Camera Gate");
                gateGo.transform.SetParent(stage.transform, false);
                gateGo.transform.position = new Vector3(0f, 0f, Mathf.Lerp(start.y, end.y, spec.GateAlong));
                gateGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // arrow toward -z: the far side is closed
                gateGo.AddComponent<CameraGate>().Until = System.Array.ConvertAll(spec.GateUntil, k => spawns[k]);
            }

            var eliteView = new GameObject("Mini Boss View").AddComponent<EliteView>();
            eliteView.Director = director;
            eliteView.EnemyLayer = EnsureLayer(EnemyLayer);
            eliteView.EliteMaterial = Lit("MiniBoss", Hex("#c9722a"), Hex("#ff8a2a") * 1.4f);

            var mobView = new GameObject("Mob View").AddComponent<MobView>();
            mobView.Director = director;
            mobView.MobMaterial = Lit("Mob", Hex("#8a3fd1"), Hex("#b455ff") * 0.9f);
        }

        // The wave plan a rebuilt stage keeps: the one its scene pointed at before the rebuild, else the
        // stage's first version. Wave plans are hand-tuned assets, so a rebuild never changes which is in use.
        static WavePlan WavePlanFor(string stageName, string previousGuid)
        {
            if (!string.IsNullOrEmpty(previousGuid))
            {
                var kept = AssetDatabase.LoadAssetAtPath<WavePlan>(AssetDatabase.GUIDToAssetPath(previousGuid));
                if (kept != null) return kept;
            }
            return AssetDatabase.LoadAssetAtPath<WavePlan>($"Assets/Data/Waves/{stageName}/{stageName}_v01.asset");
        }

        // The guid of the wave plan a scene file's WaveSpawner points at, or null.
        static string WavePlanGuidIn(string scenePath)
        {
            if (!System.IO.File.Exists(scenePath)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(scenePath),
                @"\bPlan: \{fileID: 11400000, guid: ([0-9a-f]{32})");
            return m.Success ? m.Groups[1].Value : null;
        }

        internal static void AddPresentationManager(GameObject game, EdgeScrollCamera camera)
        {
            var manager = game.AddComponent<PresentationManager>();
            manager.Cam = camera;
            manager.Database = Load<PresentationDatabase>("Assets/Data/Fx/PresentationDatabase.asset");
        }

        internal static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError("ArenaSceneBuilder: missing " + path + " (run Rebuild Default Data first)");
            return a;
        }

        internal static Material Lit(string name, Color baseColor, Color? emission = null)
        {
            var m = Upsert($"{MatDir}/{name}.mat", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", 0.1f);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Danger zones draw over everything (no depth test), so terrain never hides them.</summary>
        internal static Shader TelegraphShader() =>
            Shader.Find("BoneSquadKiting/TelegraphOverlay") ?? Shader.Find("Sprites/Default");

        internal static Material Upsert(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { m.shader = shader; return m; }
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        internal static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        internal static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        /// <summary>Find or create a user layer and return its index.</summary>
        internal static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 6; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            Debug.LogError("ArenaSceneBuilder: no free layer slot for " + name);
            return 0;
        }
    }
}
