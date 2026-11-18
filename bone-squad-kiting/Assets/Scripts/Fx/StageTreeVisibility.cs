using UnityEngine;
using UnityEngine.Rendering;

namespace BoneSquadKiting.Fx
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class StageTreeVisibility : MonoBehaviour
    {
        static readonly int VisibilityMap = Shader.PropertyToID("_VisibilityMap");
        static readonly int Darkness = Shader.PropertyToID("_Darkness");
        static readonly int TreeMask = Shader.PropertyToID("_StageTreeMask");
        static readonly int TreeDarkness = Shader.PropertyToID("_StageTreeDarkness");
        static readonly int TreeU = Shader.PropertyToID("_StageTreeU");
        static readonly int TreeV = Shader.PropertyToID("_StageTreeV");

        MeshRenderer overlay;

        void OnEnable()
        {
            overlay = GetComponent<MeshRenderer>();
            RenderPipelineManager.beginCameraRendering += BindMask;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BindMask;
            Shader.SetGlobalFloat(TreeDarkness, 0f);
        }

        void BindMask(ScriptableRenderContext context, Camera camera)
        {
            var material = overlay.sharedMaterial;
            if (!overlay.enabled || !material || camera.cameraType == CameraType.Preview ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0)
            {
                Shader.SetGlobalFloat(TreeDarkness, 0f);
                return;
            }

            // The overlay quad maps local XY [-0.5, 0.5] onto the full mask.
            var worldToQuad = transform.worldToLocalMatrix;
            Shader.SetGlobalVector(TreeU, worldToQuad.GetRow(0));
            Shader.SetGlobalVector(TreeV, worldToQuad.GetRow(1));
            Shader.SetGlobalTexture(TreeMask, material.GetTexture(VisibilityMap));
            Shader.SetGlobalFloat(TreeDarkness, material.GetFloat(Darkness));
        }
    }
}
