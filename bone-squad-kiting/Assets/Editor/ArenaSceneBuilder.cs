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
        const string MatDir = "Assets/Prototype/Materials";
        const string EnemyLayer = "Enemy";
        const string GroundLayer = "Ground", ObstacleLayer = "Obstacle";

        // Camera framing. Tune here (or on the camera, once the scene is hand-authored).
        const float CameraPitch = 60f;      // degrees down from horizontal
        const float CameraYaw = 0f;         // degrees turned around Y; 0 = straight on, boss directly ahead
        const float CameraFov = 30f;        // vertical field of view
        const float CameraDistance = 34f;   // from the ground point at the center of the view
        // Start between the squad (z = 3.4) and the boss (z = -7.4) so both are in frame.
        static readonly Vector2 CameraStartLookAt = new(0f, -0.1f);

        // Ranges as tuned by hand in the scene (2026-11-18). The squad has to close in on the boss to fire.
        const float UnitFireRange = 5.5f;
        const float SquadEngageRange = 5f;

        [MenuItem("BoneSquadKiting/Rebuild Arena Scene")]
        public static void Build()
        {
            int enemyLayer = EnsureLayer(EnemyLayer);
            EnsureFolder("Assets", "Prototype"); EnsureFolder("Assets/Prototype", "Materials");

            var ground = Lit("Ground", Hex("#0d2434"));
            var unit = Lit("Unit", Hex("#35e0d0"), Hex("#35e0d0") * 0.6f);
            var boss = Lit("Boss", Hex("#c22a2a"), Hex("#ff3b30") * 1.6f);
            var shot = Lit("Shot", Hex("#ffe27a"), Hex("#ffd24a") * 2.5f);
            var telegraph = Upsert($"{MatDir}/Telegraph.mat", Shader.Find("Sprites/Default"));
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
            director.Sequence = Load<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
            // RTS camera: it does not follow the squad; the mouse at a screen edge slides it over the arena.
            var scroll = camGo.AddComponent<EdgeScrollCamera>();
            scroll.Distance = CameraDistance; scroll.LookAt = CameraStartLookAt;
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
            // Pre-place the squad in its starting layout so it is visible without entering Play mode.
            var startSlots = FormationSlots.Generate(squad.Formations[0], GameConstants.SquadSize);
            for (int i = 0; i < GameConstants.SquadSize; i++)
            {
                var u = SquadView.BuildUnit(i, squadView.UnitScale, unit);
                u.transform.SetParent(squadView.transform, false);
                u.transform.position = new Vector3(squad.Anchor.x + startSlots[i].x, squadView.UnitScale, squad.Anchor.y + startSlots[i].y);
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
            tele.Director = director;
            teleGo.GetComponent<MeshRenderer>().sharedMaterial = telegraph;
            teleGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

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

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("ArenaSceneBuilder: wrote " + ScenePath);
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError("ArenaSceneBuilder: missing " + path + " (run Rebuild Default Data first)");
            return a;
        }

        static Material Lit(string name, Color baseColor, Color? emission = null)
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

        static Material Upsert(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { m.shader = shader; return m; }
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        /// <summary>Find or create a user layer and return its index.</summary>
        static int EnsureLayer(string name)
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
