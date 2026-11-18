using UnityEngine;

namespace BoneSquadKiting.Game
{
    /// <summary>
    /// What "continue" resumes from, kept between sessions: the number of the game scene's stage to play
    /// next (0 = nothing cleared yet, no save), or, once every map is cleared, the name of the scene that
    /// follows them (the boss arena).
    /// </summary>
    public static class GameProgress
    {
        /// <summary>PlayerPrefs key. Tests use their own so they never touch a real save.</summary>
        public static string Key = "bsk.nextStage";
        static string SceneKey => Key + ".scene";

        public static int NextStage => Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));
        /// <summary>Scene to continue in instead of a map of the game scene; empty when it is a map.</summary>
        public static string NextScene => PlayerPrefs.GetString(SceneKey, "");
        public static bool HasSave => NextStage > 0;

        /// <summary>A stage was cleared: continue starts from <paramref name="nextStage"/> (in <paramref name="scene"/> if given).</summary>
        public static void Save(int nextStage, string scene = "")
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(0, nextStage));
            PlayerPrefs.SetString(SceneKey, scene ?? "");
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(SceneKey);
            PlayerPrefs.Save();
        }
    }
}
