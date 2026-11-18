using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

public class SquadLeaderTests
{
    FormationController _fc;

    [SetUp] public void SetUp() { _fc = SquadTestRig.MakeFc(leader: true); }
    [TearDown] public void TearDown() { Object.DestroyImmediate(_fc.gameObject); }

    SquadUnit Leader => _fc.Units[0];

    [Test]
    public void ExactlyOneLeader_AtTheCenter_InEveryFormationButTheRing()
    {
        int leaders = 0; foreach (var u in _fc.Units) if (u.IsLeader) leaders++;
        Assert.AreEqual(1, leaders);
        foreach (var id in new[] { "dense", "line", "cross", "dense" })
        {
            _fc.SetFormation(id);
            Assert.That(Leader.Slot.magnitude, Is.LessThan(0.2f), $"{id}: leader holds the center slot");
        }
    }

    [Test]
    public void LeaderKeepsTheCenter_WhenUnitsDieAndTheSquadReforms()
    {
        for (int i = 5; i < 25; i++) _fc.Units[i].Dead = true;
        _fc.Reform();
        Assert.That(Leader.Slot.magnitude, Is.LessThan(1e-3f));
        _fc.RotationStepDegrees = 45f; _fc.Rotate(1);
        Assert.That(Leader.Slot.magnitude, Is.LessThan(1e-3f), "rotation turns around the leader");
    }

    [Test]
    public void InARing_TheLeaderStandsOnTheRim_FacingTheLastClick()
    {
        _fc.SetFormation("ring");
        Assert.That(Leader.Slot.magnitude, Is.EqualTo(3.7f).Within(1e-3f), "on the rim, not in the empty middle");
        Assert.That(Vector2.Dot(Leader.Slot.normalized, new Vector2(0, -1)), Is.GreaterThan(0.95f), "default: toward -z");

        _fc.FaceToward(_fc.Anchor + new Vector2(10f, 0f));
        for (int i = 0; i < 300; i++) _fc.Tick(0.02f);        // the ring turns there, it does not jump
        Assert.That(Vector2.Dot(Leader.Slot.normalized, new Vector2(1, 0)), Is.GreaterThan(0.95f), "moved round to face the click");

        _fc.FaceToward(_fc.Anchor);                           // a click on the anchor carries no direction
        Assert.That(Vector2.Dot(Leader.Slot.normalized, new Vector2(1, 0)), Is.GreaterThan(0.95f), "keeps the last one");

        var before = Leader.Slot;
        _fc.RotationStepDegrees = 15f; _fc.Rotate(3);
        Assert.That(Vector2.Angle(before, Leader.Slot), Is.LessThan(8f), "the wheel does not carry the leader round");

        for (int i = 0; i < _fc.Units.Count; i++)
            for (int j = i + 1; j < _fc.Units.Count; j++)
                Assert.That(Vector2.Distance(_fc.Units[i].Slot, _fc.Units[j].Slot), Is.GreaterThan(1e-3f), "everyone has a slot of their own");

        _fc.SetFormation("dense");
        Assert.That(Leader.Slot.magnitude, Is.LessThan(1e-3f), "back to the center in other formations");
    }

    [Test]
    public void LeaderDamage_GoesToTheNearestOtherUnit()
    {
        var took = SquadDamage.Apply(_fc.Units, Leader, 30f);
        Assert.AreNotSame(Leader, took);
        Assert.AreEqual(GameConstants.UnitMax, Leader.Hp);
        Assert.AreEqual(GameConstants.UnitMax - 30f, took.Hp);
        float d = Vector2.Distance(new Vector2(took.X, took.Z), new Vector2(Leader.X, Leader.Z));
        Assert.That(d, Is.EqualTo(0.53f).Within(1e-3f), "a neighbour in the dense grid");

        took.Hp = 10f;
        SquadDamage.Apply(_fc.Units, Leader, 30f);
        Assert.IsTrue(took.Dead, "the stand-in can die for the leader");
        Assert.IsFalse(Leader.Dead);
    }

    [Test]
    public void LastOneStanding_TheLeaderTakesDamageItself_AndCanDie()
    {
        for (int i = 1; i < 25; i++) _fc.Units[i].Dead = true;
        SquadDamage.Apply(_fc.Units, Leader, 60f);
        Assert.AreEqual(GameConstants.UnitMax - 60f, Leader.Hp);
        SquadDamage.Apply(_fc.Units, Leader, 60f);
        Assert.IsTrue(Leader.Dead);
    }

    [Test]
    public void Pattern_CatchingLeaderAndNeighbour_TheNeighbourPaysForBoth()
    {
        foreach (var u in _fc.Units) { u.X = 20f; u.Z = 20f; }     // everyone far away...
        Leader.X = 0; Leader.Z = 0;                                 // ...except the leader
        var neighbour = _fc.Units[1]; neighbour.X = 0.5f; neighbour.Z = 0;   // ...and one unit beside it
        var sys = new BossAttackSystem();
        int hit = -1, lost = -1; sys.OnResolved += (h, l) => { hit = h; lost = l; };

        sys.ForceResolve(DangerShape.Disc(3.10f), 0, 0, _fc.Units);

        Assert.AreEqual(2, hit, "two units were inside");
        Assert.AreEqual(1, lost);
        Assert.IsTrue(neighbour.Dead, "55 of its own + 55 for the leader");
        Assert.AreEqual(GameConstants.UnitMax, Leader.Hp);
    }

    [Test]
    public void IssuingOrders_TurnsTheLeaderInARing()
    {
        var dir = _fc.gameObject.AddComponent<GameDirector>();
        dir.enabled = false; dir.Squad = _fc; dir.HasBoss = false;
        dir.ClearRule = BoneSquadKiting.Stage.StageClearRule.ReachEnd;
        dir.Restart();
        dir.ChangeFormation("ring");
        dir.IssueOrder(SquadOrder.Move(_fc.Anchor + new Vector2(-6f, 0f)));
        Assert.That(Vector2.Dot(_fc.LeaderFacing, new Vector2(-1, 0)), Is.GreaterThan(0.95f), "the order sets where the ring should face");
        _fc.Halt(); for (int i = 0; i < 300; i++) _fc.Tick(0.02f);   // the ring turns there
        Assert.That(Vector2.Dot(_fc.Units[0].Slot.normalized, new Vector2(-1, 0)), Is.GreaterThan(0.95f));
        // Attack orders do not turn the ring: turning units cannot fire.
        var slots = new Vector2[_fc.Units.Count];
        for (int i = 0; i < slots.Length; i++) slots[i] = _fc.Units[i].Slot;
        dir.IssueOrder(SquadOrder.AttackMove(_fc.Anchor + new Vector2(0f, 6f)));
        _fc.Halt(); for (int i = 0; i < 100; i++) _fc.Tick(0.02f);
        for (int i = 0; i < slots.Length; i++) Assert.AreEqual(slots[i], _fc.Units[i].Slot, "attack-move leaves the ring as it is");

        // ...and an attack order given mid-turn stops the turn where it is.
        dir.IssueOrder(SquadOrder.Move(_fc.Anchor + new Vector2(0f, 6f)));
        _fc.Halt(); for (int i = 0; i < 10; i++) _fc.Tick(0.02f);
        dir.IssueOrder(SquadOrder.AttackMove(_fc.Anchor + new Vector2(6f, 0f)));
        _fc.Halt();
        for (int i = 0; i < slots.Length; i++) slots[i] = _fc.Units[i].Slot;
        for (int i = 0; i < 100; i++) _fc.Tick(0.02f);
        for (int i = 0; i < slots.Length; i++) Assert.AreEqual(slots[i], _fc.Units[i].Slot, "the turn stopped");
        foreach (var u in _fc.Units) Assert.IsFalse(u.Moving, "everyone is standing, so everyone can fire");
    }

    [Test]
    public void SquadWithoutALeader_BehavesAsBefore()
    {
        var plain = SquadTestRig.MakeFc();
        foreach (var u in plain.Units) Assert.IsFalse(u.IsLeader);
        var took = SquadDamage.Apply(plain.Units, plain.Units[0], 30f);
        Assert.AreSame(plain.Units[0], took);
        Object.DestroyImmediate(plain.gameObject);
    }

    [Test]
    public void Ring_TurnsToBringTheLeaderToTheFront_UnitsKeepTheirPlacesAndNobodyCutsAcross()
    {
        var fc = SquadTestRig.MakeFc(leader: true);
        fc.SetFormation("ring");
        for (int i = 0; i < 300; i++) fc.Tick(0.02f);                       // everyone settles on the ring
        float radius = fc.Units[0].Slot.magnitude;
        // Each unit's place relative to the leader, as an angle around the ring.
        float[] offset = new float[fc.Units.Count];
        for (int i = 0; i < offset.Length; i++) offset[i] = Vector2.SignedAngle(fc.Units[0].Slot, fc.Units[i].Slot);

        var rng = new System.Random(7);
        for (int click = 0; click < 12; click++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
            fc.FaceToward(fc.Anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f);
            for (int t = 0; t < 500; t++)
            {
                fc.Tick(0.02f);
                foreach (var u in fc.Units)
                {
                    Assert.That(u.Slot.magnitude, Is.EqualTo(radius).Within(1e-3f), "every place stays on the circle");
                    float fromCenter = (new Vector2(u.X, u.Z) - fc.Anchor).magnitude;
                    Assert.That(fromCenter, Is.GreaterThan(radius * 0.6f), "nobody cuts across the inside of the ring");
                }
                for (int i = 1; i < offset.Length; i++)
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(offset[i], Vector2.SignedAngle(fc.Units[0].Slot, fc.Units[i].Slot))), Is.LessThan(0.5f),
                        "units keep their place relative to the leader: the ring turns as one");
            }
            Assert.That(Vector2.Dot(fc.Units[0].Slot.normalized, fc.LeaderFacing), Is.GreaterThan(0.999f), "the leader ends at the front");
            var leaderPos = new Vector2(fc.Units[0].X, fc.Units[0].Z) - fc.Anchor;
            Assert.That(Vector2.Dot(leaderPos.normalized, fc.LeaderFacing), Is.GreaterThan(0.99f), "and has walked there");
        }

        // The exactly opposite direction still turns round the rim (it has to pick a side).
        fc.FaceToward(fc.Anchor - fc.LeaderFacing * 5f);
        for (int t = 0; t < 500; t++)
        {
            fc.Tick(0.02f);
            Assert.That((new Vector2(fc.Units[0].X, fc.Units[0].Z) - fc.Anchor).magnitude, Is.GreaterThan(radius * 0.6f));
        }
        Assert.That(Vector2.Dot(fc.Units[0].Slot.normalized, fc.LeaderFacing), Is.GreaterThan(0.999f));

        fc.RingTurnFactor = 0f;                                             // 0 = the old instant behaviour
        fc.FaceToward(fc.Anchor + new Vector2(3f, 4f));
        Assert.That(Vector2.Dot(fc.Units[0].Slot.normalized, fc.LeaderFacing), Is.GreaterThan(0.999f));
        Object.DestroyImmediate(fc.gameObject);
    }
}
