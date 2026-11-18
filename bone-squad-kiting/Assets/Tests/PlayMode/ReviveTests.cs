using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

public class ReviveTests
{
    FormationController _fc;
    GameDirector _dir;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc;
        _dir.HasBoss = false;                                 // nothing attacks; tests kill units by hand
        _dir.ClearRule = BoneSquadKiting.Stage.StageClearRule.ReachEnd;
        _dir.Restart();
    }

    [TearDown] public void TearDown() { Object.DestroyImmediate(_fc.gameObject); }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _dir.Step(0.02f); }
    void Kill(params int[] ids) { foreach (int i in ids) _fc.Units[i].Dead = true; _fc.Reform(); }

    [Test]
    public void NobodyDead_FDoesNothing()
    {
        Assert.IsFalse(_dir.TryRevive());
        Assert.AreEqual(0, _dir.Revives.Active.Count);
    }

    [Test]
    public void Revive_BringsOneUnitBack_AtFullHealth_AfterTheDuration()
    {
        Kill(7);
        _fc.Units[7].Hp = 0;
        Assert.AreEqual(1, _dir.DeadCount);
        Assert.IsTrue(_dir.TryRevive());

        Run(2.9f);
        Assert.IsTrue(_fc.Units[7].Dead, "not yet");
        Run(0.2f);
        Assert.IsFalse(_fc.Units[7].Dead);
        Assert.AreEqual(GameConstants.UnitMax, _fc.Units[7].Hp);
        Assert.AreEqual(0, _dir.DeadCount);
        Assert.AreEqual(0, _dir.Revives.Active.Count);
    }

    [Test]
    public void RevivedUnit_AppearsNearTheCenter_NotOnIt_ThenWalksToAFreeSlot()
    {
        Kill(20);
        _dir.TryRevive();
        Vector2 seen = default; _dir.ReviveCompleted += (reviver, revived) => seen = new Vector2(revived.X, revived.Z);
        Run(3.05f);
        float fromCenter = (seen - _fc.Anchor).magnitude;
        Assert.That(fromCenter, Is.EqualTo(_dir.Revives.AppearRadius).Within(0.05f), "close to the center, but not the center itself");

        Run(2f);
        var u = _fc.Units[20];
        Assert.That(Vector2.Distance(new Vector2(u.X, u.Z), _fc.Anchor + u.Slot), Is.LessThan(0.05f), "it took a slot");
        for (int i = 0; i < _fc.Units.Count; i++)
            if (i != 20) Assert.That(Vector2.Distance(_fc.Units[i].Slot, u.Slot), Is.GreaterThan(1e-3f), "a slot of its own");
    }

    [Test]
    public void Reviver_KeepsItsPlace_AndCannotFireWhileReviving()
    {
        Kill(3);
        Run(1.5f);   // the formation closes up around the gap first; the reviver is picked at random and might be one that moves
        SquadUnit reviver = null; _dir.ReviveStarted += (r, d) => reviver = r;
        _dir.TryRevive();
        Assert.IsNotNull(reviver);
        Assert.IsTrue(reviver.Reviving);
        var place = new Vector2(reviver.X, reviver.Z);

        reviver.Moving = false;
        Assert.IsFalse(FireRules.CanFire(reviver, SquadOrder.Move(_fc.Anchor)));
        Run(1f);
        Assert.That(Vector2.Distance(place, new Vector2(reviver.X, reviver.Z)), Is.LessThan(0.01f), "it does not leave its slot");

        Run(2.2f);
        Assert.IsFalse(reviver.Reviving);
        reviver.Moving = false;
        Assert.IsTrue(FireRules.CanFire(reviver, SquadOrder.Move(_fc.Anchor)), "back to shooting once done");
    }

    [Test]
    public void SeveralPresses_RunSeveralRevivesSideBySide_UpToTheNumberOfDead()
    {
        Kill(1, 2, 3);
        Assert.IsTrue(_dir.TryRevive());
        Run(1f);
        Assert.IsTrue(_dir.TryRevive());
        Assert.IsTrue(_dir.TryRevive());
        Assert.IsFalse(_dir.TryRevive(), "every dead unit already has someone reviving it");
        Assert.AreEqual(3, _dir.Revives.Active.Count);

        Run(2.1f);
        Assert.AreEqual(2, _dir.DeadCount, "the first one finished");
        Run(1f);
        Assert.AreEqual(0, _dir.DeadCount);
    }

    [Test]
    public void RevivesCannotOutnumberTheLiving()
    {
        var dead = new int[23]; for (int i = 0; i < 23; i++) dead[i] = i + 2;   // two units left alive
        Kill(dead);
        Assert.IsTrue(_dir.TryRevive());
        Assert.IsTrue(_dir.TryRevive());
        Assert.IsFalse(_dir.TryRevive(), "both living units are busy");
    }

    [Test]
    public void GaugeKeepsFilling_WhileTheSquadMoves()
    {
        Kill(9);
        _dir.TryRevive();
        _dir.IssueOrder(SquadOrder.Move(_fc.Anchor + new Vector2(6f, 0)));
        Run(3.1f);
        Assert.AreEqual(0, _dir.DeadCount);
    }

    [Test]
    public void ReviverDies_TheReviveIsCancelled_AndTheGaugeIsLost()
    {
        Kill(5);
        SquadUnit reviver = null; int cancelled = 0;
        _dir.ReviveStarted += (r, d) => reviver = r;
        _dir.ReviveCancelled += (r, d) => cancelled++;
        _dir.TryRevive();
        Run(2.5f);
        reviver.Dead = true;
        Run(1f);
        Assert.AreEqual(1, cancelled);
        Assert.IsTrue(_fc.Units[5].Dead, "the unit stays dead");
        Assert.AreEqual(0, _dir.Revives.Active.Count);
        Assert.IsFalse(reviver.Reviving);

        Assert.IsTrue(_dir.TryRevive(), "a new revive starts from zero");
        Run(2.5f);
        Assert.IsTrue(_fc.Units[5].Dead || reviver.Dead, "2.5s is not enough for a fresh gauge");
    }

    [Test]
    public void Reviver_IsChosenFromUnitsThatAreNotShooting_WhenThereAreAny()
    {
        Kill(0);
        foreach (var u in _fc.Units) u.Cd = 0.5f;            // everyone just fired...
        _fc.Units[11].Cd = 0f;                               // ...except this one
        SquadUnit reviver = null; _dir.ReviveStarted += (r, d) => reviver = r;
        _dir.TryRevive();
        Assert.AreSame(_fc.Units[11], reviver);
    }

    [Test]
    public void ReviveSeconds_IsTunable()
    {
        _dir.ReviveSeconds = 1f;
        Kill(4);
        _dir.TryRevive();
        Run(1.1f);
        Assert.AreEqual(0, _dir.DeadCount);
    }

    [Test]
    public void Restart_DropsRevivesInProgress()
    {
        Kill(4);
        _dir.TryRevive();
        _dir.Restart();
        Assert.AreEqual(0, _dir.Revives.Active.Count);
        Assert.AreEqual(0, _dir.DeadCount);
        foreach (var u in _fc.Units) Assert.IsFalse(u.Reviving);
    }
}
