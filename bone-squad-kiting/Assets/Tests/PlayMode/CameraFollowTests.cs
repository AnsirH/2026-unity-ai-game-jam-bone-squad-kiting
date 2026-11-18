using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Cameras;

/// <summary>The camera stays on the squad and leans toward the mouse, never losing the squad's center.</summary>
public class CameraFollowTests
{
    EdgeScrollCamera _cam;
    Camera _view;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("cam");
        go.transform.rotation = Quaternion.Euler(60f, 180f, 0f);   // same angle as the game camera
        _view = go.AddComponent<Camera>();
        _view.fieldOfView = 40f; _view.aspect = 16f / 9f;
        _cam = go.AddComponent<EdgeScrollCamera>();
        _cam.enabled = false;                                      // tests drive Follow() themselves
        _cam.Distance = 30f;
        _cam.BoundsMin = new Vector2(-500, -500); _cam.BoundsMax = new Vector2(500, 500);
        _cam.KeepMargin = 0.15f; _cam.LookAhead = 1f; _cam.DeadZone = 0.1f; _cam.SmoothTime = 0f;
        _cam.Apply();
    }

    [TearDown] public void TearDown() { Object.DestroyImmediate(_cam.gameObject); }

    Vector2 OnScreen(Vector2 ground)
    {
        var p = _view.WorldToViewportPoint(new Vector3(ground.x, 0f, ground.y));
        return new Vector2(p.x, p.y);
    }

    [Test]
    public void Aim_IsZeroAroundTheCenter_AndFullAtTheEdges()
    {
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.AimFromViewport(new Vector2(0.5f, 0.5f), 0.1f));
        Assert.AreEqual(Vector2.zero, EdgeScrollCamera.AimFromViewport(new Vector2(0.54f, 0.47f), 0.1f), "inside the dead zone");
        Assert.That(EdgeScrollCamera.AimFromViewport(new Vector2(1f, 0.5f), 0.1f).x, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(EdgeScrollCamera.AimFromViewport(new Vector2(0f, 0f), 0.1f), Is.EqualTo(new Vector2(-1f, -1f)));
        Assert.That(EdgeScrollCamera.AimFromViewport(new Vector2(3f, 0.5f), 0.1f).x, Is.EqualTo(1f).Within(1e-4f), "clamped past the edge");
    }

    [Test]
    public void MouseAtTheCenter_TheViewIsCenteredOnTheSquad()
    {
        var squad = new Vector2(7f, -3f);
        _cam.Follow(squad, Vector2.zero, 0.02f);
        Assert.That(Vector2.Distance(_cam.LookAt, squad), Is.LessThan(1e-3f));
        Assert.That(Vector2.Distance(OnScreen(squad), new Vector2(0.5f, 0.5f)), Is.LessThan(1e-3f));
    }

    [Test]
    public void MouseToTheRight_TheViewShiftsRight_UntilTheSquadSitsAtTheLeftMargin()
    {
        var squad = new Vector2(2f, 4f);
        _cam.Follow(squad, new Vector2(1f, 0f), 0.02f);
        var p = OnScreen(squad);
        Assert.That(p.x, Is.EqualTo(0.15f).Within(2e-3f), "the squad's center is at the keep margin on the left");
        Assert.That(p.y, Is.EqualTo(0.5f).Within(2e-3f));
    }

    [Test]
    public void WhereverTheMousePulls_TheSquadCenterStaysInsideTheMargins()
    {
        var squad = new Vector2(-6f, 9f);
        for (int ix = -1; ix <= 1; ix++)
            for (int iy = -1; iy <= 1; iy++)
            {
                _cam.Follow(squad, new Vector2(ix, iy), 0.02f);
                var p = OnScreen(squad);
                Assert.That(p.x, Is.InRange(0.15f - 2e-3f, 0.85f + 2e-3f), $"x for aim ({ix},{iy})");
                Assert.That(p.y, Is.InRange(0.15f - 2e-3f, 0.85f + 2e-3f), $"y for aim ({ix},{iy})");
            }
    }

    [Test]
    public void LookAhead_ScalesHowFarTheViewLeans()
    {
        var squad = Vector2.zero;
        _cam.LookAhead = 0.5f;
        _cam.Follow(squad, new Vector2(1f, 0f), 0.02f);
        Assert.That(OnScreen(squad).x, Is.EqualTo(0.5f - 0.35f * 0.5f).Within(2e-3f));
        _cam.LookAhead = 0f;
        _cam.Follow(squad, new Vector2(1f, 1f), 0.02f);
        Assert.That(Vector2.Distance(_cam.LookAt, squad), Is.LessThan(1e-3f), "no lean at all");
    }

    [Test]
    public void TheViewFollowsTheSquadAsItWalks()
    {
        _cam.Follow(new Vector2(0f, 0f), new Vector2(0f, 1f), 0.02f);
        var lean = _cam.Lean;
        _cam.Follow(new Vector2(30f, -40f), new Vector2(0f, 1f), 0.02f);
        Assert.That(Vector2.Distance(_cam.LookAt, new Vector2(30f, -40f) + lean), Is.LessThan(1e-3f));
    }

    [Test]
    public void WithSmoothing_TheLeanArrivesGradually()
    {
        _cam.SmoothTime = 0.2f;
        var squad = Vector2.zero;
        _cam.Follow(squad, new Vector2(1f, 0f), 0.02f);
        float first = Mathf.Abs(OnScreen(squad).x - 0.5f);
        Assert.That(first, Is.GreaterThan(0f).And.LessThan(0.2f), "only part of the way after one frame");
        for (int i = 0; i < 200; i++) _cam.Follow(squad, new Vector2(1f, 0f), 0.02f);
        Assert.That(OnScreen(squad).x, Is.EqualTo(0.15f).Within(5e-3f));
    }
}
