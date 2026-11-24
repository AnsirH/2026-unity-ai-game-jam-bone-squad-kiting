using UnityEngine;

namespace BoneSquadKiting.Combat
{
    /// <summary>Put on an enemy's collider object so a mouse raycast can resolve it to an IEnemy.</summary>
    public class EnemyRef : MonoBehaviour
    {
        public IEnemy Enemy; // set at runtime; interfaces are not serialized

        // Anchor the pick volume to the model, so it follows the actor through movement and pooling.
        public static EnemyRef AddBodyPick(GameObject model)
        {
            Renderer body = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null) body = model.GetComponentInChildren<MeshRenderer>();
            if (body == null) return null;

            var bounds = body.bounds;
            var pick = new GameObject("Click Target") { layer = LayerMask.NameToLayer("Enemy") };
            pick.transform.position = bounds.center;
            pick.transform.SetParent(model.transform, true);
            var box = pick.AddComponent<BoxCollider>();
            box.size = bounds.size;
            box.isTrigger = true;
            return pick.AddComponent<EnemyRef>();
        }

        public void Bind(IEnemy enemy)
        {
            Enemy = enemy;
            GetComponent<Collider>().enabled = enemy != null;
        }
    }
}
