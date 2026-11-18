using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Squad;

public class QuarterViewCameraTests
{
    FormationController _fc;
    QuarterViewCamera _cam;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        var go = new GameObject("cam");
        go.transform.rotation = Quaternion.Euler(50f, 205f, 0f);
        _cam = go.AddComponent<QuarterViewCamera>();
        _cam.enabled = false; // tests drive Tick() themselves
        _cam.Squad = _fc; _cam.Distance = 30f; _cam.SmoothTime = 0.3f;
        _cam.Snap();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_fc.gameObject); Object.Destroy(_cam.gameObject);
    }

    [Test]
    public void Snap_LooksStraightAtTheAnchor()
    {
        var anchor = new Vector3(_fc.Anchor.x, 0, _fc.Anchor.y);
        var toAnchor = anchor - _cam.transform.position;
        Assert.That(toAnchor.magnitude, Is.EqualTo(30f).Within(1e-3f));
        Assert.That(Vector3.Angle(toAnchor, _cam.transform.forward), Is.LessThan(0.01f));
    }

    [Test]
    public void LookOffset_ShiftsTheLookAtPoint()
    {
        var before = _cam.DesiredPosition;
        _cam.LookOffset = new Vector3(0, 0, -3f);
        Assert.That(_cam.DesiredPosition - before, Is.EqualTo(new Vector3(0, 0, -3f)).Using(Vector3EqualityComparer.Instance));
    }

    [Test]
    public void Follow_LagsBehindThenCatchesUp_WithoutRotating()
    {
        var rotation = _cam.transform.rotation;
        var start = _cam.transform.position;
        _fc.Anchor = new Vector2(5f, -2f);

        _cam.Tick(0.016f);
        float firstStep = Vector3.Distance(start, _cam.transform.position);
        float remaining = Vector3.Distance(_cam.transform.position, _cam.DesiredPosition);
        Assert.That(firstStep, Is.GreaterThan(0f));
        Assert.That(remaining, Is.GreaterThan(firstStep), "eases in instead of jumping");

        for (int i = 0; i < 200; i++) _cam.Tick(0.016f);
        Assert.That(Vector3.Distance(_cam.transform.position, _cam.DesiredPosition), Is.LessThan(0.01f));
        Assert.AreEqual(rotation, _cam.transform.rotation);
    }
}
