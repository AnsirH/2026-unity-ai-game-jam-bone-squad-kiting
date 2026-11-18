using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Stage
{
    /// <summary>The root of the StageKit prefab: handles to the parts a StageStart or StageRun wires up.</summary>
    public class StageKit : MonoBehaviour
    {
        public const string Resource = "StageKit";

        public StageSetup Setup;
        public GameDirector Director;
        public EdgeScrollCamera Cam;

        /// <summary>
        /// Bring the game kit into a scene, still switched off so the caller can wire it before anything
        /// wakes. The scene's own cameras step aside for the game camera (play mode only: nothing is saved),
        /// and the kit's event system is dropped when the scene already has one.
        /// </summary>
        public static StageKit Spawn(Scene scene)
        {
            var prefab = Resources.Load<GameObject>(Resource);
            if (prefab == null) { Debug.LogError("StageKit: Resources/" + Resource + " is missing (menu BoneSquadKiting/Rebuild Stage Kit)."); return null; }
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.gameObject.scene == scene) cam.gameObject.SetActive(false);
            bool hasEventSystem = FindAnyObjectByType<EventSystem>() != null;

            var go = Instantiate(prefab);                        // the prefab's root is inactive: nothing wakes yet
            go.name = prefab.name;
            SceneManager.MoveGameObjectToScene(go, scene);       // outlines and markers are looked up per scene
            if (hasEventSystem) foreach (var es in go.GetComponentsInChildren<EventSystem>(true)) Destroy(es.gameObject);
            return go.GetComponent<StageKit>();
        }
    }
}
