using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using BoneSquadKiting.UI;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Writes the main menu scene: a fixed camera over a patch of ground, the commander's hand buried in
    /// it (it bursts out when a game is started), an event system and the MainMenu (which draws its own
    /// buttons). Nothing of the game is in it. It is the first scene of the build.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string Path = "Assets/Scenes/MainMenu.unity";
        const string HandPath = "Assets/Art/MainMenu/CommanderHandBurst/Commander_Hand_Burst.fbx";
        const string CommanderMat = "Assets/Art/Character/Commander/Materials/Commander.mat";
        const string GroundMatPath = "Assets/Art/MainMenu/MenuGround.mat";

        [MenuItem("BoneSquadKiting/Rebuild Main Menu Scene")]
        public static void Build()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var cam = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
                cam.fieldOfView = 35f;
                // From the side the open palm faces, a little off to one side and above.
                cam.transform.position = new Vector3(1.31f, 0.67f, -0.43f);
                cam.transform.LookAt(new Vector3(-0.1f, 0.22f, 0.08f));

                var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.3f; light.color = new Color(1f, 0.93f, 0.82f);
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(40f, -60f, 0f);   // from behind the camera, onto the palm

                MainMenu menu = new GameObject("Main Menu", typeof(MainMenu)).GetComponent<MainMenu>();
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

                var hand = BuildHand();
                if (hand != null)
                {
                    menu.Burst = hand;
                    // Wide opaque ground: it hides the commander, who waits just below it.
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    ground.name = "Ground";
                    Object.DestroyImmediate(ground.GetComponent<Collider>());
                    ground.transform.position = new Vector3(0f, -0.004f, 0f);
                    ground.transform.localScale = new Vector3(6f, 1f, 6f);
                    ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();
                }
                EditorSceneManager.SaveScene(scene, Path);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            ArenaSceneBuilder.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("MainMenuSceneBuilder: wrote " + Path);
        }

        // The hand model, set up to be played by CommanderHandBurst (no Animator controller).
        static CommanderHandBurst BuildHand()
        {
            var importer = AssetImporter.GetAtPath(HandPath) as ModelImporter;
            if (importer == null) { Debug.LogWarning("MainMenuSceneBuilder: no hand model at " + HandPath + "; the menu starts games without the burst."); return null; }
            var commander = AssetDatabase.LoadAssetAtPath<Material>(CommanderMat);
            if (commander != null)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Commander"), commander);
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HandPath);
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(HandPath))
                if (o is AnimationClip c && !c.name.StartsWith("__")) { clip = c; break; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "Commander Hand";
            foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;   // it starts far below its bounds
            var burst = go.AddComponent<CommanderHandBurst>();
            var so = new SerializedObject(burst);
            so.FindProperty("burstClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            return burst;
        }

        static Material GroundMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MenuGround" };
            // Match the loose earth that comes with the hand, so the patch blends into the ground.
            var color = new Color(0.33f, 0.25f, 0.18f);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(HandPath))
                if (o is Material m && m.name.StartsWith("Soil") && m.HasProperty("_BaseColor")) { color = m.GetColor("_BaseColor"); break; }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(mat, GroundMatPath);
            return mat;
        }
    }
}
