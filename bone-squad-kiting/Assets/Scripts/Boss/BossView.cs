using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Visuals;

namespace BoneSquadKiting.Bosses
{
    /// <summary>Scene object for the boss: keeps the collider + EnemyRef in sync with the Boss state.</summary>
    [RequireComponent(typeof(EnemyRef))]
    public class BossView : MonoBehaviour
    {
        public Color HitFlashColor = new(4f, 2.5f, 2f, 1f); // emission while the boss's hit flash is at full

        Boss _boss;
        Renderer _renderer;
        MaterialPropertyBlock _block;
        HitFlash _flash;

        void Awake()
        {
            _renderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

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

            if (_renderer == null) return;
            _flash.Apply(_renderer, _block, _boss.Flash, HitFlashColor);
        }
    }
}
