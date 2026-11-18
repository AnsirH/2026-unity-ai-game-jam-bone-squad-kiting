using UnityEngine;

namespace BoneSquadKiting.Game
{
    /// <summary>
    /// What "continue" resumes from: the number of the stage to play next, kept between sessions.
    /// 0 = nothing cleared yet (no save). Stage numbers count the game scene's maps first (0, 1, ...);
    /// the number just past the last map means the scene that follows them (the boss arena).
    /// </summary>
    public static class GameProgress
    {
        /// <summary>PlayerPrefs key. Tests use their own so they never touch a real save.</summary>
        public static string Key = "bsk.nextStage";

        public static int NextStage => Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));
        public static bool HasSave => NextStage > 0;

        /// <summary>A stage was cleared: continue starts from <paramref name="nextStage"/>.</summary>
        public static void Save(int nextStage)
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(0, nextStage));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
