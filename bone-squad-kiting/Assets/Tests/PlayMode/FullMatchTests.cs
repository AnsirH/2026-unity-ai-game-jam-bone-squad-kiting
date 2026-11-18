using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

/// <summary>
/// Plays whole matches with the shipped data assets, with time fast-forwarded through GameDirector.Step.
/// A player who always takes the recommended formation must win without losses; one who never reacts must lose.
/// </summary>
public class FullMatchTests
{
    FormationController _fc;
    GameDirector _dir;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("match");
        _fc = go.AddComponent<FormationController>();
        foreach (var id in new[] { "dense", "line", "ring", "cross" })
            _fc.Formations.Add(AssetDatabase.LoadAssetAtPath<FormationDefinition>($"Assets/Data/Formations/{id}.asset"));
        _dir = go.AddComponent<GameDirector>();
        _dir.enabled = false;
        _dir.Squad = _fc;
        _dir.Sequence = AssetDatabase.LoadAssetAtPath<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
    }

    [TearDown] public void TearDown() { Object.Destroy(_fc.gameObject); }

    float Play(float maxSeconds)
    {
        float t = 0;
        for (; t < maxSeconds && _dir.Phase == GamePhase.Play; t += 0.02f) _dir.Step(0.02f);
        return t;
    }

    [Test]
    public void FollowingEveryRecommendation_WinsWithoutLosingAUnit()
    {
        _dir.PatternStarted += p => _dir.ChangeFormation(p.Recommend);
        int hits = 0; bool phaseTwo = false;
        _dir.PatternResolved += (hit, lost) => hits += hit;
        _dir.PhaseTwoStarted += () => phaseTwo = true;

        _dir.Restart();
        float seconds = Play(180f);

        Assert.AreEqual(GamePhase.Win, _dir.Phase, $"still {_dir.Boss.Hp:0} boss HP after {seconds:0}s");
        Assert.AreEqual(0, hits, "the recommended formation dodged every pattern");
        Assert.AreEqual(GameConstants.SquadSize, _dir.AliveCount);
        Assert.IsTrue(phaseTwo, "the match went through phase two");
        Debug.Log($"FullMatch: perfect play wins in {seconds:0.0}s");
    }

    [Test]
    public void NeverChangingFormation_LosesTheSquad()
    {
        _dir.Restart();
        Play(180f);
        Assert.AreEqual(GamePhase.Lose, _dir.Phase);
        Assert.AreEqual(0, _dir.AliveCount);
        Assert.That(_dir.Boss.Hp, Is.GreaterThan(0f));
    }
}
