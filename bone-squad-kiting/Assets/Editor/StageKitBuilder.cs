using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;
using BoneSquadKiting.Game;
using BoneSquadKiting.Input;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;
using BoneSquadKiting.UI;
using A = BoneSquadKiting.EditorTools.ArenaSceneBuilder;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Builds what a hand-made map scene needs to be playable without anyone editing the map for it:
    /// the StageKit prefab (the game: squad, camera, HUD, input — brought in at play time by a StageStart
    /// marker), the marker prefabs an artist drags into their scene, and a guide scene that shows the setup.
    /// Everything is built in a throwaway scene, so the scene that happens to be open is never touched.
    /// </summary>
    public static class StageKitBuilder
    {
        public const string KitPath = "Assets/Resources/StageKit.prefab";
        public const string MarkerDir = "Assets/Prefabs/Stage";
        public const string GuidePath = "Assets/Scenes/StageSetupGuide.unity";
        public const string MoveTuningPath = "Assets/Scenes/MoveTuning.unity";
        public const string GamePath = "Assets/Scenes/Game.unity";
        public const string ArtScenePath = "Assets/Scenes/BackgroundTest.unity";

        [MenuItem("BoneSquadKiting/Rebuild Stage Kit")]
        public static void BuildKit()
        {
            A.EnsureFolder("Assets", "Resources"); A.EnsureFolder("Assets", "Prefabs"); A.EnsureFolder("Assets/Prefabs", "Stage");
            InTempScene(_ =>
            {
                var root = BuildKitObjects();
                root.SetActive(false);   // StageStart wires it up first, then switches it on
                PrefabUtility.SaveAsPrefabAsset(root, KitPath);

                var start = new GameObject("Stage Start");
                start.AddComponent<StageStart>();
                PrefabUtility.SaveAsPrefabAsset(start, MarkerDir + "/Stage Start.prefab");

                PrefabUtility.SaveAsPrefabAsset(Outline("Walk Area (Include)", WalkOutlineMode.Include, Vector3.zero,
                    new Vector2(-6, -6), new Vector2(6, -6), new Vector2(6, 6), new Vector2(-6, 6)), MarkerDir + "/Walk Area (Include).prefab");
                PrefabUtility.SaveAsPrefabAsset(Outline("Blocked Area (Exclude)", WalkOutlineMode.Exclude, Vector3.zero,
                    new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f)), MarkerDir + "/Blocked Area (Exclude).prefab");
                BuildMarkerSet();
            });
            AssetDatabase.SaveAssets();
            Debug.Log("StageKitBuilder: wrote " + KitPath + " and the marker prefabs in " + MarkerDir);
        }

        /// <summary>
        /// The whole marker set as one prefab: drop it into a map, move the points, and the map is playable.
        /// Delete it and the map is plain art again.
        /// </summary>
        [MenuItem("BoneSquadKiting/Rebuild Stage Markers Prefab")]
        public static void BuildMarkerSetMenu()
        {
            A.EnsureFolder("Assets", "Prefabs"); A.EnsureFolder("Assets/Prefabs", "Stage");
            InTempScene(_ => BuildMarkerSet());
            AssetDatabase.SaveAssets();
            Debug.Log("StageKitBuilder: wrote " + MarkerDir + "/Stage Markers.prefab");
        }

        static void BuildMarkerSet()
        {
            var set = new GameObject("Stage Markers");
            var start = new GameObject("Stage Start");
            start.transform.SetParent(set.transform, false);
            start.transform.localPosition = new Vector3(0f, 0f, 8f);
            var end = new GameObject("End Point");
            end.transform.SetParent(set.transform, false);
            end.transform.localPosition = new Vector3(0f, 0f, -8f);
            start.AddComponent<StageStart>().EndPoint = end.transform;
            Outline("Walk Area (Include)", WalkOutlineMode.Include, Vector3.zero,
                new Vector2(-8, 12), new Vector2(8, 12), new Vector2(8, -12), new Vector2(-8, -12)).transform.SetParent(set.transform, false);
            Outline("Blocked Area (Exclude)", WalkOutlineMode.Exclude, Vector3.zero,
                new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f)).transform.SetParent(set.transform, false);
            PrefabUtility.SaveAsPrefabAsset(set, MarkerDir + "/Stage Markers.prefab");
        }

        // The game, minus any level: everything under one root.
        static GameObject BuildKitObjects()
        {
            int enemyLayer = A.EnsureLayer(A.EnemyLayer);
            var unit = A.Lit("Unit", A.Hex("#35e0d0"), A.Hex("#35e0d0") * 0.6f);
            var shot = A.Lit("Shot", A.Hex("#ffe27a"), A.Hex("#ffd24a") * 2.5f);
            var telegraph = A.TelegraphMaterial();
            var particleMat = A.Upsert($"{A.MatDir}/Particle.mat", Shader.Find("Sprites/Default"));
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/GameTuning.asset");

            var root = new GameObject("StageKit");
            var kit = root.AddComponent<StageKit>();
            void Under(GameObject go) => go.transform.SetParent(root.transform, false);

            var camGo = new GameObject("Game Camera") { tag = "MainCamera" }; Under(camGo);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = false; cam.fieldOfView = A.CameraFov;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 300f;
            camGo.transform.rotation = Quaternion.Euler(A.CameraPitch, 180f + A.CameraYaw, 0f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var scroll = camGo.AddComponent<EdgeScrollCamera>();
            scroll.Distance = A.CameraDistance; scroll.Tuning = tuning;

            var game = new GameObject("Game"); Under(game);
            var squad = game.AddComponent<FormationController>();
            squad.Formations = new List<FormationDefinition>
            {
                A.Load<FormationDefinition>("Assets/Data/Formations/dense.asset"),
                A.Load<FormationDefinition>("Assets/Data/Formations/line.asset"),
                A.Load<FormationDefinition>("Assets/Data/Formations/ring.asset"),
                A.Load<FormationDefinition>("Assets/Data/Formations/cross.asset"),
            };
            var director = game.AddComponent<GameDirector>();
            director.Squad = squad; director.Tuning = tuning;
            director.UnitFireRange = A.UnitFireRange; director.SquadEngageRange = A.SquadEngageRange;
            director.Sequence = A.Load<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
            director.HasBoss = false; director.ClearRule = StageClearRule.ReachEnd;
            director.StageTitle = "Stage"; director.NextScene = "";

            var stageGo = new GameObject("Stage"); Under(stageGo);
            var baker = stageGo.AddComponent<WalkGridBaker>();
            baker.FitToScene = true;
            int groundMask = (1 << 0) | (1 << A.EnsureLayer(A.GroundLayer));   // hand-made maps leave their terrain on Default
            baker.GroundMask = groundMask; baker.ObstacleMask = 1 << A.EnsureLayer(A.ObstacleLayer);
            var stage = stageGo.AddComponent<StageSetup>();
            stage.Ground = baker; stage.Squad = squad; stage.Cam = scroll; stage.GroundFollowMask = groundMask;
            director.Stage = stage;

            game.AddComponent<RangeGizmos>().Director = director;
            var input = game.AddComponent<SquadInputController>();
            input.Cam = cam; input.Director = director; input.EnemyMask = 1 << enemyLayer;

            var squadView = new GameObject("Squad View").AddComponent<SquadView>(); Under(squadView.gameObject);
            squadView.Squad = squad; squadView.UnitMaterial = unit;
            squadView.Art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");

            var shotView = new GameObject("Shot View").AddComponent<ShotView>(); Under(shotView.gameObject);
            shotView.Director = director; shotView.ShotMaterial = shot;

            var tele = new GameObject("Telegraph").AddComponent<TelegraphView>(); Under(tele.gameObject);
            tele.Director = director; tele.Material = telegraph;

            var eliteView = new GameObject("Mini Boss View").AddComponent<EliteView>(); Under(eliteView.gameObject);
            eliteView.Director = director; eliteView.EnemyLayer = enemyLayer;
            eliteView.EliteMaterial = A.Lit("MiniBoss", A.Hex("#c9722a"), A.Hex("#ff8a2a") * 1.4f, faction: A.Faction.Enemy);
            var mobView = new GameObject("Mob View").AddComponent<MobView>(); Under(mobView.gameObject);
            mobView.Director = director;
            mobView.MobMaterial = A.Lit("Mob", A.Hex("#8a3fd1"), A.Hex("#b455ff") * 0.9f);

            var testHud = game.AddComponent<TestHud>();
            testHud.Director = director; testHud.Input = input;
            var hud = HudBuilder.Build(director, out var flashImage);
            Under(hud.gameObject);
            var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null && eventSystem.gameObject.scene == root.scene) Under(eventSystem.gameObject);

            var particlesGo = new GameObject("Hit Particles"); Under(particlesGo);
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
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            particlesGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;

            var ringGo = new GameObject("Blast Ring"); Under(ringGo);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.loop = true; ring.useWorldSpace = true; ring.widthMultiplier = 0.14f;
            ring.sharedMaterial = particleMat; ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.positionCount = 0; ring.enabled = false;

            var fx = game.AddComponent<JuiceFx>();
            fx.Director = director; fx.Cam = scroll; fx.FlashImage = flashImage;
            fx.HitParticles = particles; fx.Ring = ring;
            A.AddPresentationManager(game, scroll);

            kit.Setup = stage; kit.Director = director; kit.Cam = scroll;
            return root;
        }

        static GameObject Outline(string name, WalkOutlineMode mode, Vector3 at, params Vector2[] corners)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            go.AddComponent<WalkOutline>().Mode = mode;
            for (int i = 0; i < corners.Length; i++)
            {
                var p = new GameObject("Point " + (i + 1));
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = new Vector3(corners[i].x, 0f, corners[i].y);
            }
            return go;
        }

        /// <summary>
        /// A small example map set up the way a real one should be, with the steps written on screen.
        /// Press Play in it: the squad appears on the Stage Start and can only walk inside the outline.
        /// </summary>
        [MenuItem("BoneSquadKiting/Rebuild Stage Guide Scene")]
        public static void BuildGuide()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KitPath) == null) BuildKit();
            InTempScene(scene =>
            {
                // Stand-in for the artist's map: ground on the Default layer, a few props. No game objects.
                var map = new GameObject("Map (artist's work - example)");
                var groundMat = A.Lit("GuideGround", A.Hex("#3d5a3a"));
                var roadMat = A.Lit("GuideRoad", A.Hex("#8a7a5c"));
                var propMat = A.Lit("GuideProp", A.Hex("#5b5f66"));
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground"; ground.transform.SetParent(map.transform, false);
                ground.transform.localScale = new Vector3(7f, 1f, 6f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                void Box(string n, Vector3 pos, Vector3 size, Material m, bool collider)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = n; b.transform.SetParent(map.transform, false);
                    b.transform.position = pos; b.transform.localScale = size;
                    b.GetComponent<Renderer>().sharedMaterial = m;
                    if (!collider) Object.DestroyImmediate(b.GetComponent<Collider>());
                }
                Box("Road A", new Vector3(0f, 0.01f, 12f), new Vector3(12f, 0.02f, 26f), roadMat, false);
                Box("Road B", new Vector3(-9f, 0.01f, -8f), new Vector3(30f, 0.02f, 14f), roadMat, false);
                Box("Rock", new Vector3(-8f, 1f, -8f), new Vector3(3f, 2f, 3f), propMat, false);
                Box("Wall 1", new Vector3(7f, 1f, 12f), new Vector3(1f, 2f, 26f), propMat, false);
                Box("Wall 2", new Vector3(-7f, 1f, 14f), new Vector3(1f, 2f, 22f), propMat, false);

                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(55f, 150f, 0f);

                // The artist's own preview camera. While playing, the game camera replaces it.
                var camGo = new GameObject("Preview Camera (off while playing)") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 40f;
                camGo.AddComponent<AudioListener>();
                camGo.transform.position = new Vector3(0f, 46f, 34f); camGo.transform.rotation = Quaternion.Euler(58f, 180f, 0f);

                // What makes it playable: three kinds of markers, all under one object.
                var markers = new GameObject("Stage Markers (this is all the game needs)");
                var start = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MarkerDir + "/Stage Start.prefab"), scene);
                start.transform.SetParent(markers.transform, false);
                start.transform.position = new Vector3(0f, 0f, 21f);
                var end = new GameObject("End Point");
                end.transform.SetParent(markers.transform, false); end.transform.position = new Vector3(-20f, 0f, -8f);
                start.GetComponent<StageStart>().EndPoint = end.transform;
                start.GetComponent<StageStart>().StageTitle = "Stage Setup Guide";

                Outline("Walk Area (Include)", WalkOutlineMode.Include, Vector3.zero,
                    new Vector2(-6, 25), new Vector2(6, 25), new Vector2(6, -1), new Vector2(6, -15), new Vector2(-24, -15), new Vector2(-24, -1), new Vector2(-6, -1))
                    .transform.SetParent(markers.transform, true);
                Outline("Blocked Area (Exclude) - rock", WalkOutlineMode.Exclude, new Vector3(-8f, 0f, -8f),
                    new Vector2(-2, -2), new Vector2(2, -2), new Vector2(2, 2), new Vector2(-2, 2))
                    .transform.SetParent(markers.transform, true);

                BuildGuideUi();
                EditorSceneManager.SaveScene(scene, GuidePath);
            });
            Debug.Log("StageKitBuilder: wrote " + GuidePath);
        }

        /// <summary>
        /// A playground for tuning how the squad moves: open ground, walls to go around, a slanted edge and a
        /// narrow passage, plus a dummy that cannot die for checking ranges. No waves, no mini bosses, not in
        /// the build. Play it and edit Assets/Data/GameTuning.asset in the Inspector.
        /// </summary>
        [MenuItem("BoneSquadKiting/Rebuild Move Tuning Scene")]
        public static void BuildMoveTuning()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KitPath) == null) BuildKit();
            InTempScene(scene =>
            {
                var groundMat = A.Lit("GuideGround", A.Hex("#3d5a3a"));
                var wallMat = A.Lit("GuideProp", A.Hex("#5b5f66"));
                int obstacle = A.EnsureLayer(A.ObstacleLayer);
                var map = new GameObject("Map");
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground"; ground.transform.SetParent(map.transform, false);
                ground.transform.localScale = new Vector3(8f, 1f, 6f);   // 80 x 60
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                void Wall(string n, float x, float z, float sx, float sz, float yaw = 0f)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = n; b.transform.SetParent(map.transform, false);
                    b.transform.position = new Vector3(x, 0.75f, z); b.transform.localScale = new Vector3(sx, 1.5f, sz);
                    b.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    b.layer = obstacle;                                   // walls block by collider, no outline needed
                    b.GetComponent<Renderer>().sharedMaterial = wallMat;
                }
                Wall("Wall (long, go around)", -6f, 8f, 16f, 1.5f);
                Wall("Pillar 1", 10f, 12f, 3f, 3f);
                Wall("Pillar 2", 16f, 6f, 3f, 3f);
                Wall("Slanted wall", -18f, -2f, 14f, 1.5f, 35f);
                Wall("Passage left", 12f, -10f, 12f, 8f);                 // 3 wide gap between these two:
                Wall("Passage right", 27f, -10f, 12f, 8f);                // narrower than the formation
                Wall("Dead end back", 30f, 14f, 8f, 1.5f);
                Wall("Dead end side A", 26f, 17f, 1.5f, 6f);
                Wall("Dead end side B", 34f, 17f, 1.5f, 6f);

                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1f;
                lightGo.transform.rotation = Quaternion.Euler(55f, 150f, 0f);

                var markers = new GameObject("Stage Markers");
                var start = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MarkerDir + "/Stage Start.prefab"), scene);
                start.transform.SetParent(markers.transform, false);
                start.transform.position = new Vector3(0f, 0f, 20f);
                start.GetComponent<StageStart>().StageTitle = "Move Tuning";
                // The playable area, with one slanted edge (south-west corner cut off).
                Outline("Walk Area (Include)", WalkOutlineMode.Include, Vector3.zero,
                    new Vector2(-38, 28), new Vector2(38, 28), new Vector2(38, -28), new Vector2(-14, -28), new Vector2(-38, -6))
                    .transform.SetParent(markers.transform, true);

                var dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dummy.name = "Training Dummy (cannot die)";
                dummy.transform.position = new Vector3(-12f, 1f, 18f); dummy.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
                dummy.layer = A.EnsureLayer(A.EnemyLayer);
                dummy.GetComponent<Renderer>().sharedMaterial = A.Lit("MiniBoss", A.Hex("#c9722a"), A.Hex("#ff8a2a") * 1.4f, faction: A.Faction.Enemy);
                dummy.AddComponent<EnemyRef>();
                dummy.AddComponent<TrainingDummy>();

                // Test panel: summon any enemy at a marked point (nothing is there until a button is pressed).
                MonsterSpawnerBuilder.AddTo(new Vector3(16f, 0f, 22f));

                EditorSceneManager.SaveScene(scene, MoveTuningPath);
            });
            Debug.Log("StageKitBuilder: wrote " + MoveTuningPath);
        }

        /// <summary>
        /// The scene the game is played in: every stage's map in one scene, run by a StageRun. It starts as a
        /// copy of the artists' scene (so it has their lighting and their map instances; the maps stay prefab
        /// instances, so later art changes arrive by themselves), with the stage list added. It is made only
        /// when it does not exist yet: after that it is edited by hand (more stages, other wave plans).
        /// </summary>
        [MenuItem("BoneSquadKiting/Create Game Scene (if missing)")]
        public static void BuildGame()
        {
            if (System.IO.File.Exists(GamePath)) { Debug.Log("StageKitBuilder: " + GamePath + " already exists; left as it is."); ArenaSceneBuilder.SetBuildScenes(); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KitPath) == null) BuildKit();
            if (!AssetDatabase.CopyAsset(ArtScenePath, GamePath)) { Debug.LogError("StageKitBuilder: could not copy " + ArtScenePath); return; }

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
            try
            {
                // The maps: root objects that hold a StageStart, in name order (Stage01Map, Stage02Map, ...).
                var maps = new List<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<StageStart>(true) != null) maps.Add(root);
                maps.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

                PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Audio/SoundManager"), scene);
                var go = new GameObject("Stage Run");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetAsFirstSibling();
                var run = go.AddComponent<StageRun>();
                run.NextScene = "";
                for (int i = 0; i < maps.Count; i++)
                {
                    run.Stages.Add(new StageRun.Entry { Map = maps[i], Title = $"스테이지 {i + 1}", WavePlan = LatestWavePlan($"Stage{i + 1}"), ClearRule = StageClearRule.ReachEnd,
                        Volume = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>($"Assets/Settings/Stage{i + 1}Volume.asset") });
                    maps[i].SetActive(i == 0);
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            ArenaSceneBuilder.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("StageKitBuilder: wrote " + GamePath);
        }

        // The newest version in a stage's wave folder (names sort as _v01, _v02, ...), or null.
        static WavePlan LatestWavePlan(string stage)
        {
            string folder = "Assets/Data/Waves/" + stage;
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:WavePlan", new[] { folder })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(string.CompareOrdinal);
            return paths.Count > 0 ? AssetDatabase.LoadAssetAtPath<WavePlan>(paths[paths.Count - 1]) : null;
        }

        // The how-to, written on screen (Game view) so it is in front of whoever opens the scene.
        static void BuildGuideUi()
        {
            var canvasGo = new GameObject("Guide Text (delete freely)", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -10;   // under the game HUD
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1f;
            var root = canvasGo.GetComponent<RectTransform>();

            var panel = HudBuilder.Panel(root, "Steps", new Color(0f, 0f, 0f, 0.72f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-222, 0), new Vector2(424, 560));
            panel.raycastTarget = false;
            var title = HudBuilder.Label(panel.rectTransform, "Title", 20, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -40), new Vector2(-14, -10), bold: true, stretch: true);
            title.text = "맵 씬을 플레이 가능하게 만드는 법";
            var body = HudBuilder.Label(panel.rectTransform, "Body", 14, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -46), stretch: true);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.12f;
            body.text =
                "프리팹 위치: Assets/Prefabs/Stage\n" +
                "<b>가장 쉬운 방법:</b> Stage Markers 프리팹 하나를 끌어다 놓으면 아래 1~3이 세트로 들어옵니다. 점만 옮기면 되고, 지우면 원래 맵으로 돌아갑니다.\n\n" +
                "<b>1. 시작 지점</b>\n" +
                "Stage Start 프리팹을 씬에 끌어다 놓고, 분대가 시작할 곳으로 옮깁니다. 씬에 하나만 둡니다.\n\n" +
                "<b>2. 이동 가능한 영역 (초록 선)</b>\n" +
                "Walk Area (Include) 프리팹을 놓습니다. 자식 Point들이 꼭짓점입니다. 하이어라키 순서대로 이어지고 마지막 점은 첫 점과 자동으로 연결됩니다. Point를 옮기거나 복제(Ctrl+D)해서 길 모양을 따라 그립니다. 여러 개 놓아도 됩니다.\n\n" +
                "<b>3. 막을 곳 (빨간 선)</b>\n" +
                "Blocked Area (Exclude) 프리팹으로 바위·건물처럼 영역 안에서 못 가는 곳을 둘러쌉니다.\n\n" +
                "<b>4. 확인</b>\n" +
                "Play를 누르면 분대·게임 카메라·HUD가 자동으로 들어옵니다. Enter로 시작, 우클릭 이동, 화면 가장자리로 카메라 이동. 씬에 있던 카메라는 플레이 중에만 꺼집니다.\n\n" +
                "<b>주의</b>\n" +
                "· 바닥에는 콜라이더가 있어야 합니다(Terrain은 기본으로 있음).\n" +
                "· 선은 Scene 뷰에서 Gizmos가 켜져 있어야 보입니다.\n" +
                "· 높이(Y)는 상관없습니다. 위에서 본 모양만 씁니다.\n" +
                "· (선택) Stage Start의 End Point에 빈 오브젝트를 넣으면 그곳에 도착했을 때 클리어됩니다.";
            body.supportRichText = true;
        }

        // Build in a scene of our own, added beside whatever is open and removed afterwards.
        static void InTempScene(System.Action<Scene> build)
        {
            var previous = SceneManager.GetActiveScene();
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);
            try { build(temp); }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(temp, true);
            }
        }
    }
}
