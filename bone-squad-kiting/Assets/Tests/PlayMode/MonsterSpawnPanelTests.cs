using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.UI;

/// <summary>The move tuning scene's test panel: summon enemies at the mark, invincible squad, soldier count.</summary>
public class MonsterSpawnPanelTests
{
    FormationController _fc;
    GameDirector _dir;
    MonsterSpawnPanel _panel;
    MobDefinition _mob;
    EliteDefinition _elite;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc(leader: true);
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc; _dir.HasBoss = false;
        _dir.Restart();

        _mob = ScriptableObject.CreateInstance<MobDefinition>(); _mob.Speed = 0f;
        _elite = ScriptableObject.CreateInstance<EliteDefinition>(); _elite.Speed = 0f;

        _panel = MakePanel(_fc.gameObject.scene.name);
        var point = new GameObject("point"); point.transform.SetParent(_panel.transform);
        point.transform.position = new Vector3(30f, 0f, -12f);
        _panel.SpawnPoint = point.transform;
        _panel.Mobs = new[] { _mob }; _panel.Elites = new[] { _elite };
    }

    // The panel checks its scene's name when it wakes, so the name is set before the object is switched on.
    static MonsterSpawnPanel MakePanel(string sceneName)
    {
        var go = new GameObject("panel"); go.SetActive(false);
        var p = go.AddComponent<MonsterSpawnPanel>();
        p.OnlyInScene = sceneName;
        go.SetActive(true);
        return p;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_panel.gameObject);
        Object.DestroyImmediate(_fc.gameObject);
        Object.DestroyImmediate(_mob); Object.DestroyImmediate(_elite);
        SquadDamage.Invincible = false;
    }

    [Test]
    public void SummonMobs_PutsAGroupAtTheMark()
    {
        _panel.MobCount = 6; _panel.MobSpread = 1f;
        Assert.AreEqual(6, _panel.SummonMobs(_mob));
        Assert.AreEqual(6, _dir.Mobs.AliveCount);
        foreach (var m in _dir.Mobs.Mobs)
            Assert.That(Vector2.Distance(m.Pos, new Vector2(30f, -12f)), Is.LessThanOrEqualTo(1.001f));
    }

    [Test]
    public void SummonElite_PutsOneMiniBossAtTheMark_AndClearAllRemovesEverything()
    {
        var e = _panel.SummonElite(_elite);
        Assert.IsNotNull(e);
        Assert.That(Vector2.Distance(e.Pos, new Vector2(30f, -12f)), Is.LessThan(0.001f));
        _panel.SummonMobs(_mob);
        _panel.ClearAll();
        Assert.AreEqual(0, _dir.Mobs.AliveCount);
        Assert.AreEqual(0, _dir.Elites.Elites.Count);
    }

    [Test]
    public void Invincible_TheSquadTakesNoDamage_AndItEndsWithThePanel()
    {
        var unit = _fc.Units[3];
        _panel.Invincible = true;
        SquadDamage.Apply(_fc.Units, unit, 500f);
        Assert.AreEqual(GameConstants.UnitMax, unit.Hp);
        Assert.IsFalse(unit.Dead);

        _panel.enabled = false;                       // leaving the scene switches it back off
        Assert.IsFalse(SquadDamage.Invincible);
        SquadDamage.Apply(_fc.Units, unit, 500f);
        Assert.IsTrue(unit.Dead);
    }

    [Test]
    public void SoldierCount_CanBeLoweredAndRaised_TheLeaderStays()
    {
        Assert.AreEqual(GameConstants.SquadSize - 1, _panel.SoldierCount);
        _panel.SetSoldierCount(5);
        Assert.AreEqual(5, _panel.SoldierCount);
        Assert.IsFalse(_fc.Units[0].Dead, "the leader is never removed");
        Assert.AreEqual(6, _fc.AliveCount);

        _panel.SetSoldierCount(0);
        Assert.AreEqual(0, _panel.SoldierCount);
        Assert.IsFalse(_fc.Units[0].Dead);

        _panel.SetSoldierCount(999);
        Assert.AreEqual(GameConstants.SquadSize - 1, _panel.SoldierCount);
        foreach (var u in _fc.Units) Assert.AreEqual(GameConstants.UnitMax, u.Hp, "back at full health");
    }

    [Test]
    public void InAnyOtherScene_ThePanelSwitchesItselfOff_AndDoesNothing()
    {
        LogAssertOff(() =>
        {
            var other = MakePanel("SomeOtherScene");
            other.Mobs = new[] { _mob };
            Assert.IsFalse(other.enabled);
            Assert.AreEqual(0, other.SummonMobs(_mob));
            Assert.IsNull(other.SummonElite(_elite));
            int before = other.SoldierCount;
            other.SetSoldierCount(3);
            Assert.AreEqual(before, other.SoldierCount);
            Object.DestroyImmediate(other.gameObject);
        });
    }

    // The panel logs a warning when it switches itself off; that is expected here.
    static void LogAssertOff(System.Action body)
    {
        bool was = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try { body(); } finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = was; }
    }
}
