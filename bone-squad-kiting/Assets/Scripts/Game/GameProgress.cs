using UnityEngine;

namespace BoneSquadKiting.Game
{
    /// <summary>
    /// Continue loads the saved stage, its optional mid-stage checkpoint, or a named scene.
    /// </summary>
    public static class GameProgress
    {
        /// <summary>PlayerPrefs key. Tests use their own so they never touch a real save.</summary>
        public static string Key = "bsk.nextStage";
        static string SceneKey => Key + ".scene";

        public static int NextStage => Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));
        /// <summary>Scene to continue in instead of a map of the game scene; empty when it is a map.</summary>
        public static string NextScene => PlayerPrefs.GetString(SceneKey, "");
        static string CheckpointKey => Key + ".checkpoint";
        public static bool AtCheckpoint => PlayerPrefs.GetInt(CheckpointKey, 0) == 1;
        public static bool HasSave => NextStage > 0 || AtCheckpoint;

        /// <summary>Record where the next retry or Continue should begin.</summary>
        public static void Save(int nextStage, string scene = "", bool atCheckpoint = false)
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(0, nextStage));
            PlayerPrefs.SetString(SceneKey, scene ?? "");
            if (atCheckpoint) PlayerPrefs.SetInt(CheckpointKey, 1);
            else PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(SceneKey);
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }
    }
}
