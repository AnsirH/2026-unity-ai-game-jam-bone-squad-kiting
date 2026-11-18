using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>Zones that turn (burst and sustained), the formation's turning speed, and camera gates.</summary>
public class RotatingPatternTests
{
    FormationController _fc;
    PatternDefinition _gate;
    AttackSequence _seq;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _fc.SetFormation("line");
        foreach (var u in _fc.Units) { u.X = _fc.Anchor.x + u.Slot.x; u.Z = _fc.Anchor.y + u.Slot.y; }
        _gate = ScriptableObject.CreateInstance<PatternDefinition>();
        _gate.Time = 2f; _gate.Shape = DangerShape.BandsX(0.83f, 6.4f, 7.1f);
        _seq = ScriptableObject.CreateInstance<AttackSequence>();
        _seq.Patterns = new[] { _gate };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_gate); Object.DestroyImmediate(_seq);
    }

    BossAttackSystem Caster(float rotationSpeed)
        => new() { Sequence = _seq, NextAttack = 0.1f, TrackSquad = false, FixedGap = 100f, RotationSpeed = rotationSpeed };

    void Snap()   // put every unit exactly on its slot
    {
        foreach (var u in _fc.Units) { u.X = _fc.Anchor.x + u.Slot.x; u.Z = _fc.Anchor.y + u.Slot.y; }
    }

    [Test]
    public void BurstZone_TurnsWhileTelegraphed_AndGoesOffAtTheAngleItReached()
    {
        var sys = Caster(45f);                              // 2s telegraph x 45 deg/s = a quarter turn
        int hit = -1; sys.OnResolved += (h, l) => hit = h;
        for (float t = 0; t < 1.1f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.That(sys.Current.Value.Angle, Is.EqualTo(45f).Within(3f), "halfway through, an eighth of a turn");
        for (float t = 0; t < 1.2f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.That(hit, Is.GreaterThan(15), "the line did not turn with the zone, so most of it is caught");
    }

    [Test]
    public void TurningTheFormationWithTheZone_DodgesIt()
    {
        var sys = Caster(45f);
        int hit = -1; sys.OnResolved += (h, l) => hit = h;
        _fc.RotationStepDegrees = 90f; _fc.Rotate(1); Snap();   // a quarter turn, the same way the zone turns
        for (float t = 0; t < 2.3f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.AreEqual(0, hit);
    }

    [Test]
    public void NegativeSpeed_TurnsTheOtherWay()
    {
        var sys = Caster(-45f);
        for (float t = 0; t < 1.1f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.That(Mathf.DeltaAngle(0f, sys.Current.Value.Angle), Is.EqualTo(-45f).Within(3f));
    }

    [Test]
    public void SustainedZone_StaysLive_AndDamagesAtIntervals_WhileItKeepsTurning()
    {
        _gate.Kind = PatternKind.Sustained; _gate.SustainSeconds = 2f; _gate.TickInterval = 0.5f; _gate.TickDamage = 15f;
        _fc.SetFormation("dense"); Snap();                  // the dense block sits across the gate's bands: some are always inside
        var sys = Caster(30f);
        int ticks = 0, resolved = 0, totalHits = 0;
        sys.OnDamage += (h, l) => ticks++;
        sys.OnResolved += (h, l) => { resolved++; totalHits = h; };

        for (int i = 0; i < 200 && !(sys.Current.HasValue && sys.Current.Value.Sustaining); i++) sys.Tick(_fc, null, 0.02f);
        Assert.IsTrue(sys.Current.HasValue && sys.Current.Value.Sustaining, "telegraph over: the zone is live, not gone");
        sys.Tick(_fc, null, 0.02f);
        Assert.AreEqual(1, ticks, "first damage lands as it goes live");
        Assert.AreEqual(0, resolved);
        float angleWhenLive = sys.Current.Value.Angle;

        for (float t = 0; t < 1.0f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.AreEqual(3, ticks, "one tick every half second");
        Assert.That(sys.Current.Value.Angle, Is.GreaterThan(angleWhenLive + 20f), "still turning");

        for (float t = 0; t < 1.2f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.IsNull(sys.Current, "over after its sustain time");
        Assert.AreEqual(1, resolved, "resolved once, at the end");
        Assert.That(totalHits, Is.GreaterThan(0));

        float lowest = 100f; foreach (var u in _fc.Units) lowest = Mathf.Min(lowest, u.Hp);
        Assert.That(lowest, Is.LessThan(100f));
        Assert.That((100f - lowest) % 15f, Is.EqualTo(0f).Within(0.01f), "in ticks of 15");
    }

    [Test]
    public void BurstPattern_IgnoresTheSustainedSettings()
    {
        _gate.SustainSeconds = 5f;                          // Kind stays Burst
        var sys = Caster(0f);
        int resolved = 0; sys.OnResolved += (h, l) => resolved++;
        for (float t = 0; t < 2.3f; t += 0.02f) sys.Tick(_fc, null, 0.02f);
        Assert.AreEqual(1, resolved);
        Assert.IsNull(sys.Current);
    }

    [Test]
    public void FormationTurnsInstantly_ByDefault()
    {
        _fc.Rotate(2);
        Assert.AreEqual(30f, _fc.RotationDegrees);
    }

    [Test]
    public void WithATurningSpeed_TheFormationSwingsTowardTheWheelsAngle()
    {
        _fc.RotationSpeed = 60f;
        _fc.Rotate(2);                                      // target 30 degrees
        Assert.AreEqual(0f, _fc.RotationDegrees, "nothing yet");
        Assert.AreEqual(30f, _fc.TargetRotationDegrees);
        for (float t = 0; t < 0.25f; t += 0.01f) _fc.Tick(0.01f);
        Assert.That(_fc.RotationDegrees, Is.EqualTo(15f).Within(1.5f), "a quarter second at 60 deg/s");
        for (float t = 0; t < 0.5f; t += 0.01f) _fc.Tick(0.01f);
        Assert.That(_fc.RotationDegrees, Is.EqualTo(30f).Within(0.05f), "it stops on the target");

        _fc.Rotate(-3);                                     // target 345: the short way is backwards through 0
        for (float t = 0; t < 0.4f; t += 0.01f) _fc.Tick(0.01f);
        float d = Mathf.DeltaAngle(0f, _fc.RotationDegrees);
        Assert.That(d, Is.LessThan(10f), "it went back the short way, not all the way round");
    }

    [Test]
    public void CameraGate_HoldsTheViewBack_UntilItsMiniBossesAreDead()
    {
        var spawnGo = new GameObject("cannon"); var spawn = spawnGo.AddComponent<MiniBossSpawn>();
        var gateGo = new GameObject("gate"); gateGo.transform.position = new Vector3(0, 0, 10f);
        gateGo.transform.rotation = Quaternion.Euler(0, 180f, 0);   // arrow points toward -z: that side is closed
        var gate = gateGo.AddComponent<CameraGate>(); gate.Until = new[] { spawn };

        Assert.IsTrue(gate.Closed, "its mini boss has not even appeared yet");
        Assert.That(Vector2.Distance(new Vector2(3f, 10f), gate.Clamp(new Vector2(3f, 2f))), Is.LessThan(1e-3f), "pulled back to the line");
        Assert.That(Vector2.Distance(new Vector2(3f, 14f), gate.Clamp(new Vector2(3f, 14f))), Is.LessThan(1e-3f), "the near side is free");

        var camGo = new GameObject("cam"); camGo.transform.rotation = Quaternion.Euler(60, 180, 0);
        var cam = camGo.AddComponent<EdgeScrollCamera>(); cam.enabled = false;
        cam.BoundsMin = new Vector2(-50, -50); cam.BoundsMax = new Vector2(50, 50);
        cam.Gates = new[] { gate };
        cam.LookAt = new Vector2(0, 20f); cam.Apply();
        for (int i = 0; i < 200; i++) cam.Scroll(new Vector2(0, 1), 0.05f);   // push up-screen, toward -z
        Assert.That(cam.LookAt.y, Is.EqualTo(10f).Within(0.01f), "the view stops at the gate");

        cam.FocusOn(new Vector2(0, -5f), 1f);               // a directed look may pass it
        for (int i = 0; i < 25; i++) cam.TickFocus(0.02f);
        Assert.That(cam.LookAt.y, Is.LessThan(5f));
        for (int i = 0; i < 40; i++) cam.TickFocus(0.02f);

        var elite = new Elite { Def = ScriptableObject.CreateInstance<EliteDefinition>(), Health = 0f };
        spawn.MarkTriggered(elite);                         // appeared and already dead
        Assert.IsFalse(gate.Closed);
        for (int i = 0; i < 200; i++) cam.Scroll(new Vector2(0, 1), 0.05f);
        Assert.That(cam.LookAt.y, Is.LessThan(-20f), "open: the view goes on");

        Object.DestroyImmediate(elite.Def); Object.DestroyImmediate(spawnGo); Object.DestroyImmediate(gateGo); Object.DestroyImmediate(camGo);
    }
}
