using BoneSquadKiting.Core;

namespace BoneSquadKiting.Audio
{
    public enum CastVoice { None, FireMage, JavelinKnight }

    // Cues for zone attacks. The same geometry is shared by several enemies,
    // so only the caster that owns the sound can request it.
    internal static class CastSounds
    {
        public static void ThrowJavelin()
        {
            SoundManager.Current?.PlayVolleySfx("enemy.javelin.shout");
            SoundManager.Current?.PlayVolleySfx("enemy.javelin.flight");
        }

        public static void Begin(CastVoice voice, PatternId pattern)
        {
            if (voice == CastVoice.FireMage)
                SoundManager.Current?.PlaySfx("enemy.firemage.scorch");
            else if (voice == CastVoice.JavelinKnight && pattern == PatternId.Gate)
                ThrowJavelin();
        }

        public static void Approach(CastVoice voice, PatternId pattern)
        {
            if (voice == CastVoice.JavelinKnight && pattern == PatternId.Gate)
                SoundManager.Current?.PlaySfx("enemy.javelin.split");
        }

        public static void Land(CastVoice voice, PatternId pattern)
        {
            if (voice == CastVoice.FireMage)
                SoundManager.Current?.PlaySfx("enemy.firemage.eruption");
            else if (voice == CastVoice.JavelinKnight && pattern == PatternId.Gate)
                SoundManager.Current?.PlaySfx("enemy.javelin.impact");
        }
    }
}
