using UnityEngine;
using BoneSquadKiting.Combat;

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
        Color _baseEmission;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_renderer != null && _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(EmissionId))
                _baseEmission = _renderer.sharedMaterial.GetColor(EmissionId);
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
            if (_boss.Flash > 0f)
            {
                _block.SetColor(EmissionId, Color.Lerp(_baseEmission, HitFlashColor, Mathf.Clamp01(_boss.Flash)));
                _renderer.SetPropertyBlock(_block);
            }
            else if (_renderer.HasPropertyBlock()) _renderer.SetPropertyBlock(null);
        }
    }
}
