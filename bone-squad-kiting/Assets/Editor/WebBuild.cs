using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Unity Play에 올릴 웹 빌드를 만든다. 메뉴: Tools > Build > Web (Unity Play).
    /// 텍스처 최대 크기는 빌드하는 동안에만 낮추므로 Windows 빌드의 화질은 그대로다.
    /// </summary>
    public static class WebBuild
    {
        public const string Output = "Builds/Web";

        /// <summary>웹 빌드에서만 쓰는 텍스처 최대 크기. 용량이 넘치면 512로 낮춘다.</summary>
        public const int MaxTextureSize = 1024;

        [MenuItem("Tools/Build/Web (Unity Play)")]
        public static void Build()
        {
            int code = Run() ? 0 : 1;
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static bool Run()
        {
            int size = MaxTextureSize;
            string arg = Environment.GetCommandLineArgs().SkipWhile(a => a != "-webMaxTexture").Skip(1).FirstOrDefault();
            if (arg != null) int.TryParse(arg, out size);
            return BuildTo(Output, size);
        }

        /// <summary>
        /// 웹에서는 시작할 때 셰이더 변형을 한꺼번에 미리 컴파일하면(Preloaded Shaders) 브라우저가
        /// 몇 분 동안 멈춘다. 그래서 웹 빌드를 만드는 동안에만 미리 불러오기 목록을 비운다.
        /// </summary>
        public static bool BuildTo(string output, int size)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            var graphics = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            SerializedProperty preloaded = graphics.FindProperty("m_PreloadedShaders");
            var kept = new UnityEngine.Object[preloaded.arraySize];
            for (int i = 0; i < kept.Length; i++) kept[i] = preloaded.GetArrayElementAtIndex(i).objectReferenceValue;
            preloaded.ClearArray();
            graphics.ApplyModifiedPropertiesWithoutUndo();

            int before = EditorUserBuildSettings.overrideMaxTextureSize;
            EditorUserBuildSettings.overrideMaxTextureSize = size;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    locationPathName = output,
                    options = BuildOptions.None,
                };
                BuildSummary s = BuildPipeline.BuildPlayer(options).summary;
                Debug.Log($"[WebBuild] result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                          $"size={s.totalSize / 1048576}MB time={s.totalTime} maxTexture={size} out={s.outputPath}");
                return s.result == BuildResult.Succeeded;
            }
            finally
            {
                EditorUserBuildSettings.overrideMaxTextureSize = before;
                graphics.Update();
                preloaded.arraySize = kept.Length;
                for (int i = 0; i < kept.Length; i++) preloaded.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
