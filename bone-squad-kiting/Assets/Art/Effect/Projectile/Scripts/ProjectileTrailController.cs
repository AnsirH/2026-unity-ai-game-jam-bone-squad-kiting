using System.Collections;
using UnityEngine;

public class ProjectileTrailController : MonoBehaviour
{
    [SerializeField] private TrailRenderer trail;
    [SerializeField, Min(0f)] private float startDelay = 0.04f;
    private Coroutine startRoutine;

    private void OnEnable()
    {
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>(true);
        if (trail == null)
            return;

        trail.emitting = false;
        trail.Clear();
        startRoutine = StartCoroutine(StartTrail());
    }

    private IEnumerator StartTrail()
    {
        yield return new WaitForSeconds(startDelay);
        trail.Clear();
        trail.emitting = true;
        startRoutine = null;
    }

    private void OnDisable()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }
    }
}
