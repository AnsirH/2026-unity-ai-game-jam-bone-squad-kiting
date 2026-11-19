using UnityEngine;

namespace BoneSquadKiting.Fx
{
    /// <summary>One cast clock for all three blades, tip trails and fading ground marks.</summary>
    public sealed class SovereignVolley : MonoBehaviour
    {
        static readonly int Elapsed = Shader.PropertyToID("_Elapsed");
        static readonly int ExtraTravel = Shader.PropertyToID("_ExtraTravel");
        static readonly int GroundMarkLift = Shader.PropertyToID("_GroundMarkLift");
        const float AuthoredLength = 10f;
        const float EffectLifetime = 2.4f;
        Renderer[] parts;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            parts = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            SetAge(0f);
        }

        public void FitLanes(BoneSquadKiting.Core.DangerShape shape, float groundMarkLift = 0f)
        {
            var properties = new MaterialPropertyBlock();
            foreach (Transform lane in transform)
            {
                int side = System.Math.Sign(lane.localPosition.x);
                lane.localPosition = new Vector3(side * shape.LaneSpacing, lane.localPosition.y, -shape.HalfZ);
                float extra = 2f * shape.HalfZ / lane.lossyScale.z - AuthoredLength;
                foreach (var part in lane.GetComponentsInChildren<MeshRenderer>())
                {
                    part.GetPropertyBlock(properties);
                    properties.SetFloat(ExtraTravel, extra);
                    properties.SetFloat(GroundMarkLift, groundMarkLift);
                    part.SetPropertyBlock(properties);

                    var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                    bounds.Encapsulate(bounds.max + new Vector3(0f, groundMarkLift, Mathf.Max(0f, extra)));
                    part.localBounds = bounds;
                }
            }
        }

        public void LiftGroundMarks(float metres)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var part in GetComponentsInChildren<Renderer>())
            {
                part.GetPropertyBlock(properties);
                properties.SetFloat(GroundMarkLift, metres);
                part.SetPropertyBlock(properties);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            SetAge(age);
            if (age >= EffectLifetime) Destroy(gameObject);
        }

        void SetAge(float seconds)
        {
            foreach (var part in parts)
            {
                part.GetPropertyBlock(block);
                block.SetFloat(Elapsed, seconds);
                part.SetPropertyBlock(block);
            }
        }
    }
}
