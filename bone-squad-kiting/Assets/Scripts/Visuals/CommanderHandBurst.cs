using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public sealed class CommanderHandBurst : MonoBehaviour
{
    [SerializeField] private AnimationClip burstClip;

    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;

    private void OnEnable()
    {
        if (burstClip == null)
        {
            Debug.LogError("Assign the Commander hand burst clip.", this);
            return;
        }

        var animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        graph = PlayableGraph.Create(nameof(CommanderHandBurst));
        graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        clipPlayable = AnimationClipPlayable.Create(graph, burstClip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetDuration(burstClip.length);
        var output = AnimationPlayableOutput.Create(graph, "Hand Burst", animator);
        output.SetSourcePlayable(clipPlayable);
        graph.Play();
        ResetToBuried();
    }

    public void PlayBurst()
    {
        if (!graph.IsValid()) return;
        clipPlayable.SetDone(false);
        clipPlayable.SetTime(0);
        clipPlayable.SetSpeed(1);
        graph.Evaluate(0);
    }

    public void ResetToBuried()
    {
        if (!graph.IsValid()) return;
        clipPlayable.SetDone(false);
        clipPlayable.SetTime(0);
        clipPlayable.SetSpeed(0);
        graph.Evaluate(0);
    }

    private void LateUpdate()
    {
        if (!graph.IsValid() || clipPlayable.GetSpeed() == 0 ||
            clipPlayable.GetTime() < burstClip.length) return;

        clipPlayable.SetDone(false);
        clipPlayable.SetTime(burstClip.length);
        clipPlayable.SetSpeed(0);
        graph.Evaluate(0);
    }

    private void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
