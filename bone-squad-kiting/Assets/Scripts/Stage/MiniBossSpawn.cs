using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Place this where a mini boss should appear. It is not there at the start: it appears, with a short
    /// intro, the moment the squad's anchor comes within <see cref="TriggerRadius"/>.
    /// </summary>
    public class MiniBossSpawn : MonoBehaviour
    {
        public EliteDefinition Definition;
        public float TriggerRadius = 12f;

        /// <summary>Set once the squad has come close enough and the mini boss has appeared.</summary>
        public bool Triggered { get; private set; }
        public Elite Elite { get; private set; }
        public bool Defeated => Triggered && (Elite == null || !Elite.Alive);

        public Vector2 Position => new(transform.position.x, transform.position.z);

        public void ResetSpawn() { Triggered = false; Elite = null; }
        public void MarkTriggered(Elite elite) { Triggered = true; Elite = elite; }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.8f, Definition != null ? Definition.Radius : 0.8f);
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
