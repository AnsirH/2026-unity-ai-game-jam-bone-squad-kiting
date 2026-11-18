using UnityEngine;
using UnityEngine.Rendering;

namespace BoneSquadKiting.Fx
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class StageTreeVisibility : MonoBehaviour
    {
        static readonly int VisibilityMap = Shader.PropertyToID("_VisibilityMap");
        static readonly int Darkness = Shader.PropertyToID("_Darkness");
        // Adjacent maps in the art scene must bind independent root-position masks.
        [SerializeField] bool secondaryMask;
        static readonly int[] TreeMask = MaskIds("_StageTreeMask");
        static readonly int[] TreeDarkness = MaskIds("_StageTreeDarkness");
        static readonly int[] TreeU = MaskIds("_StageTreeU");
        static readonly int[] TreeV = MaskIds("_StageTreeV");

        int Slot => secondaryMask ? 1 : 0;

        static int[] MaskIds(string name) =>
            new[] { Shader.PropertyToID(name), Shader.PropertyToID(name + "2") };

        MeshRenderer overlay;

        void OnEnable()
        {
            overlay = GetComponent<MeshRenderer>();
            RenderPipelineManager.beginCameraRendering += BindMask;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BindMask;
            Shader.SetGlobalFloat(TreeDarkness[Slot], 0f);
        }

        void BindMask(ScriptableRenderContext context, Camera camera)
        {
            var material = overlay.sharedMaterial;
            if (!overlay.enabled || !material || camera.cameraType == CameraType.Preview ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0)
            {
                Shader.SetGlobalFloat(TreeDarkness[Slot], 0f);
                return;
            }

            // The overlay quad maps local XY [-0.5, 0.5] onto the full mask.
            var worldToQuad = transform.worldToLocalMatrix;
            Shader.SetGlobalVector(TreeU[Slot], worldToQuad.GetRow(0));
            Shader.SetGlobalVector(TreeV[Slot], worldToQuad.GetRow(1));
            Shader.SetGlobalTexture(TreeMask[Slot], material.GetTexture(VisibilityMap));
            Shader.SetGlobalFloat(TreeDarkness[Slot], material.GetFloat(Darkness));
        }
    }
}
