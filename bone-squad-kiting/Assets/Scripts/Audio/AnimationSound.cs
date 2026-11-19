using UnityEngine;

namespace BoneSquadKiting.Audio
{
    [DisallowMultipleComponent]
    public sealed class AnimationSound : MonoBehaviour
    {
        // The live thrower binds its pending projectile. An isolated prefab can also audition the clip.
        public System.Func<bool> TryReleaseJavelin;

        public void ReleaseJavelin()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (TryReleaseJavelin != null) TryReleaseJavelin();
            else CastSounds.ThrowJavelin();
        }

        // Animation Event on the object that owns the Animator or legacy Animation.
        public void PlayClipSound(string id)
        {
            if (Application.isPlaying && isActiveAndEnabled) SoundManager.Current?.PlaySfx(id);
        }
    }
}
