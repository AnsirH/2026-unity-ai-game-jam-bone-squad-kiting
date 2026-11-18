using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Data;

/// <summary>A wave plan read as a table: which row applies, and what a wave is made of.</summary>
public class WavePlanTests
{
    WavePlan _plan;
    MobDefinition _melee, _ranged;

    [SetUp]
    public void SetUp()
    {
        _melee = ScriptableObject.CreateInstance<MobDefinition>();
        _ranged = ScriptableObject.CreateInstance<MobDefinition>();
        _plan = ScriptableObject.CreateInstance<WavePlan>();
        _plan.Sections.Add(Row(0f, 12f, (_melee, 8)));
        _plan.Sections.Add(Row(0.5f, 6f, (_melee, 20), (_ranged, 4)));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_plan); Object.DestroyImmediate(_melee); Object.DestroyImmediate(_ranged);
    }

    static WaveSection Row(float from, float interval, params (MobDefinition mob, int count)[] mobs)
    {
        var row = new WaveSection { FromProgress = from, Interval = interval };
        foreach (var (mob, count) in mobs) row.Mobs.Add(new WaveEntry { Mob = mob, Count = count });
        return row;
    }

    int Count(float progress, MobDefinition mob)
    {
        var list = new List<WaveEntry>();
        _plan.CompositionAt(progress, list);
        foreach (var e in list) if (e.Mob == mob) return e.Count;
        return 0;
    }

    [Test]
    public void TheRowThatHasStarted_Applies_UntilTheNextOneStarts()
    {
        Assert.AreEqual(0, _plan.SectionIndexAt(0f));
        Assert.AreEqual(0, _plan.SectionIndexAt(0.49f));
        Assert.AreEqual(1, _plan.SectionIndexAt(0.5f));
        Assert.AreEqual(1, _plan.SectionIndexAt(1f));
        Assert.AreEqual(12f, _plan.IntervalAt(0.49f));
        Assert.AreEqual(6f, _plan.IntervalAt(0.5f));
        Assert.AreEqual(8, Count(0.49f, _melee));
        Assert.AreEqual(0, Count(0.49f, _ranged));
        Assert.AreEqual(20, Count(0.5f, _melee));
        Assert.AreEqual(4, Count(0.5f, _ranged));
    }

    [Test]
    public void RowsNeedNotBeWrittenInOrder()
    {
        _plan.Sections.Reverse();
        Assert.AreEqual(12f, _plan.IntervalAt(0.2f));
        Assert.AreEqual(6f, _plan.IntervalAt(0.8f));
    }

    [Test]
    public void BeforeTheFirstRowStarts_ThereAreNoWaves()
    {
        _plan.Sections[0].FromProgress = 0.2f;
        Assert.AreEqual(-1, _plan.SectionIndexAt(0.1f));
        Assert.AreEqual(0f, _plan.IntervalAt(0.1f));
        Assert.AreEqual(0, _plan.CountAt(0.1f));
    }

    [Test]
    public void Blend_SlidesFromOneRowToTheNext()
    {
        _plan.Blend = true;
        Assert.AreEqual(12f, _plan.IntervalAt(0f), 1e-4f);
        Assert.AreEqual(9f, _plan.IntervalAt(0.25f), 1e-4f);
        Assert.AreEqual(14, Count(0.25f, _melee));
        Assert.AreEqual(2, Count(0.25f, _ranged));
        Assert.AreEqual(6f, _plan.IntervalAt(0.75f), 1e-4f, "the last row has nothing to slide toward");
        Assert.AreEqual(20, Count(0.75f, _melee));
    }

    [Test]
    public void MobsPerMinute_IsCountOverInterval()
    {
        Assert.AreEqual(40f, _plan.MobsPerMinuteAt(0f), 1e-3f);     // 8 every 12 s
        Assert.AreEqual(240f, _plan.MobsPerMinuteAt(0.6f), 1e-3f);  // 24 every 6 s
    }

    [Test]
    public void EmptyRowsAndMissingMobs_AreIgnored()
    {
        _plan.Sections[0].Mobs.Add(new WaveEntry { Mob = null, Count = 5 });
        _plan.Sections[0].Mobs.Add(null);
        Assert.AreEqual(8, _plan.CountAt(0f));
    }
}
