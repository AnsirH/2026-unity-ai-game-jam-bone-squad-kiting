using UnityEngine;

namespace BoneSquadKiting.Combat
{
    /// <summary>Put on an enemy's collider object so a mouse raycast can resolve it to an IEnemy.</summary>
    public class EnemyRef : MonoBehaviour
    {
        public IEnemy Enemy; // set at runtime; interfaces are not serialized
    }
}
