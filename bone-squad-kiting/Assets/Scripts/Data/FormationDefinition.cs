using UnityEngine;
using UnityEngine.InputSystem;

namespace BoneSquadKiting.Data
{
    public enum FormationMode { Grid, Line, Ring, Cross }

    /// <summary>A squad formation as data. Slots are produced by FormationSlots.Generate.</summary>
    [CreateAssetMenu(menuName = "BoneSquadKiting/Formation")]
    public class FormationDefinition : ScriptableObject
    {
        public string Id, DisplayName;
        public Key HotKey;
        public FormationMode Mode;
        public float Spacing;  // Grid cell size / Cross arm step / Line z step
        public float Stagger;  // Line x zig-zag width
        public float Radius;   // Ring radius
    }
}
