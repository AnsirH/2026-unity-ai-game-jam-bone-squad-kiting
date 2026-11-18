using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// The one object a map scene needs to become playable. Put it where the squad should begin. When the
    /// scene starts playing it brings in the game kit (squad, game camera, HUD, input) from the StageKit
    /// prefab, places the squad here and points the camera at it. Nothing is saved into the scene: the
    /// kit exists only while playing, so the map stays purely the artist's.
    ///
    /// The cameras already in the scene are switched off while playing (the game uses its own).
    /// Where the squad may walk is drawn with WalkOutline objects in the same scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class StageStart : MonoBehaviour
    {
        public const string KitResource = "StageKit";

        [Tooltip("Optional. The squad clears the stage by reaching this point.")]
        public Transform EndPoint;
        public float EndRadius = 3f;
        [Tooltip("Shown on the title screen.")]
        public string StageTitle = "Stage";

        /// <summary>The kit this marker brought in, while playing.</summary>
        public StageKit Kit { get; private set; }

        void Awake()
        {
            // A scene that already has the game in it (the generated stages) needs nothing from this marker.
            foreach (var d in FindObjectsByType<GameDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d.gameObject.scene == gameObject.scene) return;

            var prefab = Resources.Load<GameObject>(KitResource);
            if (prefab == null) { Debug.LogError("StageStart: Resources/" + KitResource + " is missing (menu BoneSquadKiting/Rebuild Stage Kit)."); return; }

            // The map's own cameras and listeners step aside for the game camera. Play mode only: not saved.
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.gameObject.scene == gameObject.scene) cam.gameObject.SetActive(false);
            bool hasEventSystem = FindAnyObjectByType<EventSystem>() != null;

            var go = Instantiate(prefab);                       // the prefab's root is inactive: nothing wakes yet
            go.name = prefab.name;
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);   // outlines and markers are looked up per scene
            Kit = go.GetComponent<StageKit>();
            Kit.Setup.StartPoint = transform;
            Kit.Setup.EndPoint = EndPoint; Kit.Setup.EndRadius = EndRadius;
            Kit.Director.StageTitle = StageTitle;
            if (hasEventSystem) foreach (var es in go.GetComponentsInChildren<EventSystem>(true)) Destroy(es.gameObject);
            go.SetActive(true);                                 // StageSetup bakes the walk area and seats the squad

            Kit.Cam.LookAt = Kit.Setup.Squad.Anchor;
            Kit.Cam.Apply();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
            Ring(transform.position, 1.6f);                     // roughly the room the 25 units take
            if (EndPoint == null) return;
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            Gizmos.DrawLine(EndPoint.position, EndPoint.position + Vector3.up * 4f);
            Ring(EndPoint.position, EndRadius);
        }

        static void Ring(Vector3 center, float radius)
        {
            const int n = 40;
            var prev = center + new Vector3(radius, 0.1f, 0f);
            for (int i = 1; i <= n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                var next = center + new Vector3(Mathf.Cos(a) * radius, 0.1f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
