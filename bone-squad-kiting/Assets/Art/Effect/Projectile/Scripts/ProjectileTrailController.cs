using UnityEngine;

public class ProjectileTrailController : MonoBehaviour
{
    [SerializeField] private TrailRenderer trail;

    private void OnEnable()
    {
        trail.Clear();
        trail.emitting = true;
    }
}
