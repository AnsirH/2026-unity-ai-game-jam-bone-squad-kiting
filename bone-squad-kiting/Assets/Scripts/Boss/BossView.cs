using UnityEngine;
using BoneSquadKiting.Combat;

namespace BoneSquadKiting.Bosses
{
    /// <summary>Scene object for the boss: keeps the collider + EnemyRef in sync with the Boss state.</summary>
    [RequireComponent(typeof(EnemyRef))]
    public class BossView : MonoBehaviour
    {
        Boss _boss;

        public void Bind(Boss boss)
        {
            _boss = boss;
            GetComponent<EnemyRef>().Enemy = boss;
        }

        void Update()
        {
            if (_boss == null) return;
            var p = transform.position;
            transform.position = new Vector3(_boss.Pos.x, p.y, _boss.Pos.y);
        }
    }
}
