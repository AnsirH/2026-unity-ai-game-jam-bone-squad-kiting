using System.Linq;
using UnityEditor;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.UI;
using A = BoneSquadKiting.EditorTools.ArenaSceneBuilder;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Adds the test panel to the move tuning scene: a mark on the ground where enemies appear and the panel
    /// with one button per enemy kind (every Mob and Elite asset under Assets/Data), the invincible switch
    /// and the soldier slider. The panel refuses to work in any other scene.
    /// </summary>
    public static class MonsterSpawnerBuilder
    {
        public const string RootName = "Monster Spawner (test)";

        /// <summary>
        /// Make sure the active scene has a spawner. An existing one keeps its place; its lists of enemy
        /// kinds are refreshed. Returns the panel.
        /// </summary>
        public static MonsterSpawnPanel AddTo(Vector3 point)
        {
            var panel = Object.FindAnyObjectByType<MonsterSpawnPanel>();
            if (panel == null)
            {
                var root = new GameObject(RootName);
                panel = root.AddComponent<MonsterSpawnPanel>();

                // The mark: a flat bright disc with a post, so the spot is easy to find from the game camera.
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mark.name = "Monster Spawn Point (move me)";
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.transform.SetParent(root.transform, false);
                mark.transform.position = point + Vector3.up * 0.03f;
                mark.transform.localScale = new Vector3(3f, 0.02f, 3f);
                var mat = A.Lit("SpawnPointMark", A.Hex("#b0303a"), A.Hex("#ff3040") * 1.6f);
                mark.GetComponent<Renderer>().sharedMaterial = mat;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post";
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(mark.transform, false);
                post.transform.localScale = new Vector3(0.05f, 60f, 0.05f);   // the parent is squashed flat: this makes a thin 1.2 tall post
                post.transform.localPosition = new Vector3(0f, 60f, 0f);
                post.GetComponent<Renderer>().sharedMaterial = mat;
                panel.SpawnPoint = mark.transform;
            }
            panel.Mobs = All<MobDefinition>();
            panel.Elites = All<EliteDefinition>();
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static T[] All<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).OrderBy(a => a.name, System.StringComparer.Ordinal).ToArray();
    }
}
