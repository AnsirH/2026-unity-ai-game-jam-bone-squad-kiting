using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>One kind of swarm enemy. All numbers are starting values to tune by playing.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Mob")]
    public class MobDefinition : ScriptableObject
    {
        public string DisplayName = "Grunt";
        public float Radius = 0.3f;
        public float Speed = 3.2f;          // well under the squad's 7.4 so it can always be outrun
        public float Hp = 66f;              // two unit shots
        public float AttackDamage = 10f;
        public float AttackPeriod = 1.0f;   // seconds between hits
        public float AttackReach = 0.1f;    // extra distance beyond touching at which it can hit

        [Header("Looks (optional: empty = the placeholder sphere)")]
        public GameObject Prefab;           // model with an Animator; drawn with its feet on the ground
        public float ModelScale = 1f;       // multiplies the prefab's own scale
        public float ModelYawOffset = 0f;   // degrees around Y if the model's front is not +Z
        public AnimStates States = new() { Idle = "", Run = "Run", Attack = "Attack", Die = "" };
    }
}
