using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Cameras;

public class EdgeScrollCameraTests
{
    static readonly Vector2 Screen = new(1280, 720);
    EdgeScrollCamera _cam;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("cam");
        go.transform.rotation = Quaternion.Euler(60f, 180f, 0f); // same angle as the arena camera
        _cam = go.AddComponent<EdgeScrollCamera>();
        _cam.enabled = false; // tests drive Scroll() themselves
        _cam.ScrollSpeed = 10f; _cam.Distance = 30f;
        _cam.LookAt = Vector2.zero;
        _cam.BoundsMin = new Vector2(-5, -5); _cam.BoundsMax = new Vector2(5, 5);
        _cam.Apply();
    }

    [TearDown] public void TearDown() { Object.Destroy(_cam.gameObject); }

    [Test]
    public void ScrollDirection_OnlyAtEdgesInsideTheScreen()
    {
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.ScrollDirection(new Vector2(640, 360), Screen, 18));
        Assert.AreEqual(new Vector2(-1, 0), EdgeScrollCamera.ScrollDirection(new Vector2(5, 360), Screen, 18));
        Assert.AreEqual(new Vector2(1, 0), EdgeScrollCamera.ScrollDirection(new Vector2(1275, 360), Screen, 18));
        Assert.AreEqual(new Vector2(0, 1), EdgeScrollCamera.ScrollDirection(new Vector2(640, 715), Screen, 18));
        Assert.AreEqual(new Vector2(-1, -1), EdgeScrollCamera.ScrollDirection(new Vector2(3, 3), Screen, 18));
    }

    [Test]
    public void ScrollDirection_IsZeroWhenCursorLeavesTheView()
    {
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.ScrollDirection(new Vector2(-40, 360), Screen, 18));
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.ScrollDirection(new Vector2(1400, 360), Screen, 18));
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.ScrollDirection(new Vector2(640, 900), Screen, 18));
    }

    [Test]
    public void Apply_LooksAtTheLookAtPointFromTheGivenDistance()
    {
        var toPoint = Vector3.zero - _cam.transform.position;
        Assert.That(toPoint.magnitude, Is.EqualTo(30f).Within(1e-3f));
        Assert.That(Vector3.Angle(toPoint, _cam.transform.forward), Is.LessThan(0.01f));
    }

    [Test]
    public void Scroll_MovesParallelToGround_InTheScreenDirection()
    {
        float height = _cam.transform.position.y;
        var rotation = _cam.transform.rotation;

        _cam.Scroll(new Vector2(1, 0), 0.1f);   // screen right; the camera faces -z, so that is world -x
        Assert.That(_cam.LookAt.x, Is.EqualTo(-1f).Within(1e-3f));
        Assert.That(_cam.LookAt.y, Is.EqualTo(0f).Within(1e-3f));

        _cam.Scroll(new Vector2(0, 1), 0.1f);   // screen up = further away = world -z
        Assert.That(_cam.LookAt.y, Is.EqualTo(-1f).Within(1e-3f));

        Assert.That(_cam.transform.position.y, Is.EqualTo(height).Within(1e-4f), "height never changes");
        Assert.AreEqual(rotation, _cam.transform.rotation);
    }

    [Test]
    public void Scroll_StopsAtTheBounds_AndIdleDoesNotMove()
    {
        for (int i = 0; i < 100; i++) _cam.Scroll(new Vector2(-1, -1), 0.1f);
        Assert.AreEqual(new Vector2(5, 5), _cam.LookAt);

        var pos = _cam.transform.position;
        _cam.Scroll(Vector2.zero, 0.1f);
        Assert.AreEqual(pos, _cam.transform.position);
    }
}
