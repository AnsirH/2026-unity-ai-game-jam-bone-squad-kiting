using UnityEngine;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Combat
{
    /// <summary>
    /// A target that takes shots and never dies, for checking ranges and firing behaviour in a test scene.
    /// Needs a collider on the Enemy layer and an EnemyRef on the same object to be right-clickable.
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IEnemy, IDamageable
    {
        public float BodyRadius = 0.8f;
        public float DamageTaken;          // running total, visible in the Inspector

        GameDirector _director;

        public Vector2 Pos => new(transform.position.x, transform.position.z);
        public bool Alive => isActiveAndEnabled;
        public float Radius => BodyRadius;
        public float Reserved { get; set; }
        public void ApplyDamage(float amount) => DamageTaken += amount;

        void Update()
        {
            // The game may arrive after this object (StageStart brings it in), and a restart makes a new registry.
            if (_director == null) _director = FindAnyObjectByType<GameDirector>();
            if (_director == null || _director.Enemies == null) return;
            if (!_director.Enemies.Enemies.Contains(this)) _director.Enemies.Register(this);
            if (TryGetComponent<EnemyRef>(out var r)) r.Enemy = this;
        }
    }
}
