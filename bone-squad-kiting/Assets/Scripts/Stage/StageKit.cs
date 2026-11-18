using UnityEngine;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Stage
{
    /// <summary>The root of the StageKit prefab: handles to the parts a StageStart wires up.</summary>
    public class StageKit : MonoBehaviour
    {
        public StageSetup Setup;
        public GameDirector Director;
        public EdgeScrollCamera Cam;
    }
}
