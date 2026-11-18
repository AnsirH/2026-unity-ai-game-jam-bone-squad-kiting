using UnityEngine;
using BoneSquadKiting.Core;

namespace BoneSquadKiting.Data
{
    /// <summary>One boss attack: telegraph time, the formation that dodges it, and its danger geometry.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Pattern")]
    public class PatternDefinition : ScriptableObject
    {
        public PatternId Id;
        public string DisplayName, Advice, Detail;
        public string Recommend;   // FormationDefinition.Id that dodges this pattern
        public float Time;         // telegraph duration in seconds
        public DangerShape Shape;
    }
}
