using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Place this where a mini boss should be. By default it is not there at the start: it appears, with a
    /// short intro, the moment the squad's anchor comes within <see cref="TriggerRadius"/>. It can instead
    /// stand there from the start, and it can wait for other mini bosses to die first.
    /// </summary>
    public class MiniBossSpawn : MonoBehaviour
    {
        public EliteDefinition Definition;
        public float TriggerRadius = 12f;

        [Tooltip("On: it stands here from the start of the stage (a fixed cannon). Off: it appears when the squad comes within Trigger Radius.")]
        public bool PresentFromStart;

        [Tooltip("Play the intro moment (game frozen, camera goes to it) when it appears.")]
        public bool PlayIntro = true;

        [Tooltip("If set, this one stays away until every mini boss listed here is dead. Then it follows the two settings above.")]
        public MiniBossSpawn[] ActivateAfter;

        /// <summary>True once the mini bosses this one waits for (if any) are all dead.</summary>
        public bool Unlocked
        {
            get
            {
                if (ActivateAfter == null) return true;
                foreach (var other in ActivateAfter) if (other != null && !other.Defeated) return false;
                return true;
            }
        }

        /// <summary>Set once the squad has come close enough and the mini boss has appeared.</summary>
        public bool Triggered { get; private set; }
        public Elite Elite { get; private set; }
        public bool Defeated => Triggered && (Elite == null || !Elite.Alive);

        public Vector2 Position => new(transform.position.x, transform.position.z);

        public void ResetSpawn() { Triggered = false; Elite = null; }
        public void MarkTriggered(Elite elite) { Triggered = true; Elite = elite; }

        void OnDrawGizmos()
        {
            Gizmos.color = PresentFromStart ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.1f);
            if (ActivateAfter != null)
                foreach (var other in ActivateAfter)
                    if (other != null) Gizmos.DrawLine(transform.position + Vector3.up, other.transform.position + Vector3.up);   // waits for these
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.8f, Definition != null ? Definition.Radius : 0.8f);
            if (PresentFromStart) return;   // no trigger circle: it is simply there
            const int n = 64;
            var prev = transform.position + new Vector3(TriggerRadius, 0.1f, 0);
            for (int i = 1; i <= n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                var next = transform.position + new Vector3(Mathf.Cos(a) * TriggerRadius, 0.1f, Mathf.Sin(a) * TriggerRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
