using UnityEngine;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Core;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.Stage
{
    /// <summary>
    /// Connects a stage's level data to the game when the scene loads: bakes the walkable grid and hands
    /// it to the squad (movement boundary), limits the camera to the grid, and places the squad on the
    /// start point. The end point is recorded and can be queried; what reaching it means is up to the
    /// stage's clear condition.
    /// </summary>
    public class StageSetup : MonoBehaviour
    {
        public WalkGridBaker Ground;
        public FormationController Squad;
        public EdgeScrollCamera Cam;       // optional
        public Transform StartPoint;       // where the squad's anchor begins; optional
        public Transform EndPoint;         // optional
        public float EndRadius = 3f;       // the squad has arrived when its anchor is this close to EndPoint

        public WalkGrid Grid { get; private set; }

        /// <summary>True once the squad's anchor is inside the end zone. Always false without an end point.</summary>
        public bool ReachedEnd
        {
            get
            {
                if (EndPoint == null || Squad == null) return false;
                var end = new Vector2(EndPoint.position.x, EndPoint.position.z);
                return (Squad.Anchor - end).magnitude <= EndRadius;
            }
        }

        void Awake() => Apply();

        /// <summary>Bake and wire everything. Runs in Awake, before the director fields the squad in Start.</summary>
        public void Apply()
        {
            if (Ground == null || Squad == null) return;
            Grid = Ground.Bake();
            Squad.Grid = Grid;

            if (StartPoint != null)
            {
                var start = Grid.NearestWalkable(new Vector2(StartPoint.position.x, StartPoint.position.z));
                Squad.StartAnchor = start;
                Squad.Anchor = Squad.Destination = start;
            }

            if (Cam != null)
            {
                Cam.BoundsMin = Grid.Origin;
                Cam.BoundsMax = Grid.Origin + new Vector2(Grid.Width, Grid.Height) * Grid.CellSize;
                Cam.Apply();
            }
        }

        void OnDrawGizmos()
        {
            if (StartPoint != null)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.5f);
                Gizmos.DrawSphere(StartPoint.position, 0.4f);
                Gizmos.DrawLine(StartPoint.position, StartPoint.position + Vector3.up * 3f);
            }
            if (EndPoint != null)
            {
                Gizmos.color = new Color(0.3f, 0.6f, 1f);
                Gizmos.DrawLine(EndPoint.position, EndPoint.position + Vector3.up * 3f);
                const int n = 48;
                var prev = EndPoint.position + new Vector3(EndRadius, 0.1f, 0);
                for (int i = 1; i <= n; i++)
                {
                    float a = Mathf.PI * 2f * i / n;
                    var next = EndPoint.position + new Vector3(Mathf.Cos(a) * EndRadius, 0.1f, Mathf.Sin(a) * EndRadius);
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }
        }
    }
}
