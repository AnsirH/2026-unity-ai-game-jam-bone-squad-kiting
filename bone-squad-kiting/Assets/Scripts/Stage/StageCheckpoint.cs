using UnityEngine;

namespace BoneSquadKiting.Stage
{
    /// <summary>Respawn position and completed encounters for a checkpoint on this map.</summary>
    public sealed class StageCheckpoint : MonoBehaviour
    {
        public MiniBossSpawn Trigger;
        public MiniBossSpawn[] EarlierEncounters = System.Array.Empty<MiniBossSpawn>();
        public Vector2 Position => new(transform.position.x, transform.position.z);

        public void RestoreEncounters()
        {
            foreach (var marker in EarlierEncounters) Restore(marker);
            Restore(Trigger);
        }

        static void Restore(MiniBossSpawn marker)
        {
            if (marker == null) return;
            marker.MarkTriggered(null);
            if (marker.ApproachGuide != null) marker.ApproachGuide.SetActive(true);
            if (marker.DefeatGuide != null) marker.DefeatGuide.SetActive(true);
            if (marker.NextWaveClearGuide != null) marker.NextWaveClearGuide.SetActive(true);
        }
    }
}
