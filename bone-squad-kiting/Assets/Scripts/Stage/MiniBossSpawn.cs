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
    public enum MiniBossIntroWhen { OnAppear, OnArrive }

    public class MiniBossSpawn : MonoBehaviour
    {
        public EliteDefinition Definition;
        [Tooltip("Approach radius for a distance-triggered spawn, or for the top health bar of an enemy present from the start.")]
        [Min(0f)] public float TriggerRadius = 12f;

        [Tooltip("On: it stands here from the start of the stage (a fixed cannon). Off: it appears when the squad comes within Trigger Radius.")]
        public bool PresentFromStart;

        [Tooltip("Play the intro moment (game frozen, camera goes to it) when it appears.")]
        public bool PlayIntro = true;

        [Tooltip("If set, this one stays away until every mini boss listed here is dead. Then it follows the two settings above.")]
        public MiniBossSpawn[] ActivateAfter;

        [Tooltip("0 or more: it appears when the squad's progress through the stage (0 = start, 1 = end point) reaches this, instead of by distance. Needs the stage to have waves and an end point. Below 0 = off.")]
        public float AtProgress = -1f;

        [Header("This encounter")]
        [Tooltip("Optional attack cycle for this marker, in casting order. Empty uses the enemy definition.")]
        public PatternDefinition[] PatternCycle;
        [Tooltip("Seconds in attack range before the first warning. A negative value uses the enemy's usual opening delay.")]
        public float OpeningDelay = -1f;
        [Tooltip("World-space directions revealed when this enemy dies.")]
        public GameObject DefeatGuide;

        [Header("Entry route (optional)")]
        [Tooltip("Points it walks through on its way in, in order. It appears at the first one and ends on this marker. Empty = it appears on the marker.")]
        public Transform[] Route;
        [Min(0.1f)] public float EntrySpeed = 4f;

        [Header("Intro")]
        [Tooltip("When the intro moment plays for one that walks in: as it appears, or when it reaches the marker.")]
        public MiniBossIntroWhen IntroWhen = MiniBossIntroWhen.OnAppear;
        [Tooltip("Id of a presentation effect (Assets/Data/Fx/PresentationDatabase) played with the intro. Empty = none.")]
        public string IntroEffectId = "";
        [Tooltip("Seconds the intro freezes the game. 0 = the mini boss kind's own Intro Duration.")]
        [Min(0f)] public float IntroSeconds;

        /// <summary>The entry route as ground points, first to last, ending on the marker. Null when there is none.</summary>
        public System.Collections.Generic.List<Vector2> RoutePoints()
        {
            if (Route == null) return null;
            var points = new System.Collections.Generic.List<Vector2>();
            foreach (var t in Route) if (t != null) points.Add(new Vector2(t.position.x, t.position.z));
            if (points.Count == 0) return null;
            points.Add(Position);
            return points;
        }

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

        public void ResetSpawn()
        {
            Triggered = false;
            Elite = null;
            if (DefeatGuide != null) DefeatGuide.SetActive(false);
        }
        public void MarkTriggered(Elite elite) { Triggered = true; Elite = elite; }

        void OnDrawGizmos()
        {
            Gizmos.color = PresentFromStart ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.55f, 0.1f);
            if (ActivateAfter != null)
                foreach (var other in ActivateAfter)
                    if (other != null) Gizmos.DrawLine(transform.position + Vector3.up, other.transform.position + Vector3.up);   // waits for these
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.8f, Definition != null ? Definition.Radius : 0.8f);
            if (Route != null)
            {
                Vector3? last = null;
                foreach (var t in Route)
                {
                    if (t == null) continue;
                    Gizmos.DrawWireSphere(t.position + Vector3.up * 0.3f, 0.3f);
                    if (last.HasValue) Gizmos.DrawLine(last.Value + Vector3.up * 0.3f, t.position + Vector3.up * 0.3f);
                    last = t.position;
                }
                if (last.HasValue) Gizmos.DrawLine(last.Value + Vector3.up * 0.3f, transform.position + Vector3.up * 0.3f);
            }
            if (AtProgress >= 0f) return;   // appears by progress, not by distance: no trigger circle
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
