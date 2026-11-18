using UnityEngine;

namespace BoneSquadKiting.Data
{
    /// <summary>Ordered boss patterns. The boss loops through them.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/AttackSequence")]
    public class AttackSequence : ScriptableObject
    {
        public PatternDefinition[] Patterns;
    }
}
