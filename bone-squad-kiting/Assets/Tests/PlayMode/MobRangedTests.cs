using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;

/// <summary>Ranged mobs: they stop at range, shoot straight at where a unit was, and the shot can be dodged.</summary>
public class MobRangedTests
{
    FormationController _fc;
    MobDefinition _def;
    MobSystem _mobs;
    EnemyRegistry _reg;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _def = ScriptableObject.CreateInstance<MobDefinition>();
        _def.Speed = 3f; _def.AttackRange = 6f; _def.ShotSpeed = 6f; _def.AttackPeriod = 2f; _def.AttackDamage = 10f;
        _mobs = new MobSystem();
        _reg = new EnemyRegistry();
    }

    [TearDown] public void TearDown() { Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_def); }

    void Run(float seconds) { for (float t = 0; t < seconds; t += 0.02f) _mobs.Tick(_fc, _reg, null, null, 0.02f); }

    float NearestUnitDistance(Vector2 from)
    {
        float best = float.MaxValue;
        foreach (var u in _fc.Units) if (!u.Dead) best = Mathf.Min(best, Vector2.Distance(from, new Vector2(u.X, u.Z)));
        return best;
    }

    float TotalHp() { float s = 0; foreach (var u in _fc.Units) if (!u.Dead) s += u.Hp; return s; }

    [Test]
    public void RangedMob_StopsAtRange_InsteadOfWalkingUp()
    {
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 15f), _reg);
        m.AttackCd = 999f;                       // never shoots in this test
        Run(8f);
        float d = NearestUnitDistance(m.Pos);
        Assert.That(d, Is.LessThanOrEqualTo(_def.AttackRange), "it came into range");
        Assert.That(d, Is.GreaterThan(_def.AttackRange * 0.7f), "and stayed back");
    }

    [Test]
    public void RangedMob_ShootsAUnitInRange_AndTheShotHurtsIt()
    {
        float before = TotalHp();
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 7f), _reg);
        m.AttackCd = 0f;
        Run(0.1f);
        Assert.AreEqual(1, _mobs.Shots.Count, "one shot in the air");
        Assert.AreEqual(before, TotalHp(), "nothing is hurt until it lands");
        m.AttackCd = 999f;                       // only the one shot
        Run(1.9f);
        Assert.AreEqual(0, _mobs.Shots.Count);
        Assert.AreEqual(before - _def.AttackDamage, TotalHp(), 0.001f);
    }

    [Test]
    public void OutOfRange_ItDoesNotShoot()
    {
        _def.Speed = 0f;
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 14f), _reg);
        m.AttackCd = 0f;
        Run(1f);
        Assert.AreEqual(0, _mobs.Shots.Count);
    }

    [Test]
    public void Shot_FliesStraight_SoASquadThatMovedAwayIsNotHit()
    {
        _def.Speed = 0f;
        float before = TotalHp();
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 5f), _reg);
        m.AttackCd = 0f;
        Run(0.02f);
        Assert.AreEqual(1, _mobs.Shots.Count);
        m.AttackCd = 999f;
        foreach (var u in _fc.Units) u.X += 10f;   // the whole squad steps aside
        Run(3f);
        Assert.AreEqual(0, _mobs.Shots.Count, "the shot ran out of travel");
        Assert.AreEqual(before, TotalHp(), "and hit nobody");
    }

    [Test]
    public void MeleeMob_NeverMakesShots()
    {
        _def.AttackRange = 0f;
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 3f), _reg);
        m.AttackCd = 0f;
        Run(3f);
        Assert.AreEqual(0, _mobs.Shots.Count);
    }

    [Test]
    public void Clear_RemovesShotsToo()
    {
        var m = _mobs.Spawn(_def, _fc.Anchor + new Vector2(0, 7f), _reg);
        m.AttackCd = 0f;
        Run(0.05f);
        _mobs.Clear(_reg);
        Assert.AreEqual(0, _mobs.Shots.Count);
    }
}
