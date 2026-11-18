using NUnit.Framework;
using UnityEditor;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;

/// <summary>
/// The core promise of the game: the formation a pattern recommends dodges that pattern, for every
/// squad size, using the shipped data assets.
/// </summary>
public class FormationDodgeTests
{
    [Test]
    public void RecommendedFormation_DodgesItsPattern_AtEverySquadSize([Range(1, 25)] int alive)
    {
        var seq = AssetDatabase.LoadAssetAtPath<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
        Assert.IsNotNull(seq);
        foreach (var p in seq.Patterns)
        {
            var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>($"Assets/Data/Formations/{p.Recommend}.asset");
            Assert.IsNotNull(formation, p.Recommend);
            var shape = p.ScaleWithSquad ? p.Shape.Scaled(FormationSlots.SquadFraction(alive)) : p.Shape;
            var slots = FormationSlots.Generate(formation, alive);
            for (int i = 0; i < slots.Length; i++)
                Assert.IsFalse(DangerShapeEval.InDanger(shape, 0, 0, slots[i].x, slots[i].y),
                    $"{p.name}: {p.Recommend} slot {i} of {alive} is hit");
        }
    }

    [Test]
    public void PatternAdvice_NamesTheHotkeyOfItsRecommendedFormation()
    {
        var seq = AssetDatabase.LoadAssetAtPath<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
        foreach (var p in seq.Patterns)
        {
            var formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>($"Assets/Data/Formations/{p.Recommend}.asset");
            StringAssert.StartsWith(formation.HotKey.ToString(), p.Advice, p.name);
            StringAssert.Contains(formation.DisplayName, p.Advice, p.name);
        }
    }

    [Test]
    public void CorePattern_ScalesWithTheSquad_OthersDoNot()
    {
        var seq = AssetDatabase.LoadAssetAtPath<AttackSequence>("Assets/Data/Sequences/BossMain.asset");
        foreach (var p in seq.Patterns)
            Assert.AreEqual(p.Id == PatternId.Core, p.ScaleWithSquad, p.name);
    }
}
