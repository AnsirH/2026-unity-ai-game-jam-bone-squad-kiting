using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Visuals;

/// <summary>Attacks with a wind-up (the leader's throw) and projectiles drawn as models.</summary>
public class ShotVisualTests
{
    class FakeEnemy : IEnemy, IDamageable
    {
        public Vector2 Pos { get; set; }
        public bool Alive => Hp > 0;
        public float Hp = 100000;
        public float Reserved { get; set; }
        public void ApplyDamage(float amount) { Hp -= amount; }
    }

    FormationController _fc;
    ShotSystem _shots;
    EnemyRegistry _reg;
    FakeEnemy _enemy;
    SquadOrder _order;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc(leader: true);
        _shots = new ShotSystem();
        _reg = new EnemyRegistry();
        _enemy = new FakeEnemy { Pos = _fc.Anchor + new Vector2(6f, 0f) };
        _reg.Register(_enemy);
        _order = SquadOrder.Move(_fc.Anchor);
        foreach (var u in _fc.Units) u.Cd = u.IsLeader ? 0f : 1000f;      // only the leader attacks in these tests
    }

    [TearDown] public void TearDown() { if (_fc != null) Object.DestroyImmediate(_fc.gameObject); }

    void Step(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.01f) { _fc.Tick(0.01f); _shots.Tick(_fc.Units, _reg, _order, 0.01f); } }

    [Test]
    public void WindUp_TheShotLeavesAfterTheDelay_IsTheLeaders_AndTheNextWaitsForTheMotionToEnd()
    {
        var leader = _fc.Units[0];
        leader.AttackWindup = 0.5f; leader.AttackDuration = 1.2f;
        Step(0.02f);
        Assert.AreEqual(1, leader.ShotCount, "the attack has started (the model begins its throw)");
        Assert.AreEqual(0, _shots.Shots.Count, "nothing in the air during the wind-up");
        Assert.AreEqual(GameConstants.UnitDamage, _enemy.Reserved, "but the damage is already counted on");
        Step(0.4f);
        Assert.AreEqual(0, _shots.Shots.Count);
        Step(0.12f);
        Assert.AreEqual(1, _shots.Shots.Count, "released when the wind-up is over");
        Assert.IsTrue(_shots.Shots[0].Heavy, "marked as the leader's projectile");
        Assert.That(Vector2.Dot(_shots.Shots[0].Dir, new Vector2(1, 0)), Is.GreaterThan(0.99f));
        Step(0.5f);
        Assert.AreEqual(1, leader.ShotCount, "no second attack before the motion ends");
        Step(0.4f);
        Assert.AreEqual(2, leader.ShotCount);
    }

    [Test]
    public void AnimationEvent_ReleasesTheShotEarly_AndOnlyOnce()
    {
        var leader = _fc.Units[0];
        leader.AttackWindup = 0.5f; leader.AttackDuration = 1.2f;
        Step(0.1f);
        _shots.ReleasePending(leader);                       // what the model's AttackRelease event calls
        Assert.AreEqual(1, _shots.Shots.Count);
        _shots.ReleasePending(leader);
        Step(0.5f);
        Assert.That(_shots.Shots.Count, Is.LessThanOrEqualTo(1), "the timer does not throw a second one");
        Assert.That(_enemy.Hp, Is.EqualTo(100000 - GameConstants.UnitDamage).Within(0.01f), "exactly one hit landed");
    }

    [Test]
    public void MovingDuringTheWindUp_CallsTheThrowOff()
    {
        var leader = _fc.Units[0];
        leader.AttackWindup = 0.5f; leader.AttackDuration = 1.2f;
        Step(0.1f);
        Assert.IsNotNull(leader.PendingTarget);
        _order = SquadOrder.Move(_fc.Anchor + new Vector2(0f, 4f)); _fc.Order = _order; _fc.SetDestination(_order.Point);
        Step(0.6f);
        Assert.IsNull(leader.PendingTarget);
        Assert.AreEqual(0, _shots.Shots.Count, "no shot was thrown");
        Assert.AreEqual(0f, _enemy.Reserved, "and the damage is no longer counted on");
    }

    [Test]
    public void WithoutAWindUp_ShotsLeaveAtOnce_AsBefore()
    {
        Step(0.02f);
        Assert.AreEqual(1, _shots.Shots.Count);
        Assert.IsNull(_fc.Units[0].PendingTarget);
    }

    [Test]
    public void ProjectAssets_TheThrowClipHasTheReleaseEvent_AndTheArtSetHasTheSpear()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Character/Commander/Animations/Commander_Thrrow.anim");
        if (clip == null) Assert.Ignore("no commander throw clip");
        bool found = false;
        foreach (var e in clip.events) if (e.functionName == UnitVisual.AttackEventName) found = true;
        Assert.IsTrue(found, "the clip's event calls " + UnitVisual.AttackEventName);
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        Assert.IsNotNull(art.LeaderShotPrefab, "leader projectile set");
        Assert.AreEqual("Commander_Thrrow", art.LeaderStates.Attack);
    }

    [UnityTest]
    public IEnumerator InPlay_TheLeaderThrowsASpearOnTheAnimationEvent_TipFirst_AndSoldiersShootTheirModel()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        if (art == null || art.LeaderPrefab == null || art.LeaderShotPrefab == null) Assert.Ignore("no leader model or projectile in the art set");
        foreach (var u in _fc.Units) u.Cd = 0.05f;
        var dir = _fc.gameObject.AddComponent<GameDirector>();
        dir.HasBoss = false; dir.Squad = _fc; dir.ClearRule = BoneSquadKiting.Stage.StageClearRule.ReachEnd;
        var squadView = new GameObject("squad view").AddComponent<SquadView>(); squadView.Squad = _fc; squadView.Art = art;
        var shotView = new GameObject("shot view").AddComponent<ShotView>(); shotView.Director = dir;
        yield return null;
        dir.Restart();
        var dummy = new GameObject("dummy") { transform = { position = new Vector3(_fc.Anchor.x + 4f, 0f, _fc.Anchor.y) } }.AddComponent<TrainingDummy>();
        yield return null; yield return null;

        var leader = _fc.Units[0];
        // Times are the clip's, divided by the speed its state plays at (known once the state has played).
        var leaderVisual = squadView.transform.GetChild(0).GetComponent<UnitVisual>();
        Assert.That(leader.AttackWindup * leaderVisual.AttackSpeed, Is.EqualTo(0.933f).Within(0.01f), "wind-up read from the clip's event");
        Assert.That(leader.AttackDuration * leaderVisual.AttackSpeed, Is.EqualTo(2f).Within(0.01f));

        Transform spear = null, arrow = null;
        float spearSeenAt = -1f, thrownAt = -1f; int shotsBefore = leader.ShotCount;
        for (float t = 0f; t < 5f && (spear == null || arrow == null); t += Time.deltaTime)
        {
            yield return null;
            if (thrownAt < 0f && leader.ShotCount > shotsBefore) thrownAt = Time.time;
            foreach (Transform holder in shotView.transform)
            {
                if (!holder.gameObject.activeSelf) continue;
                if (holder.name.StartsWith(art.LeaderShotPrefab.name) && spear == null) { spear = holder; spearSeenAt = Time.time; }
                if (art.ShotPrefab != null && holder.name.StartsWith(art.ShotPrefab.name)) arrow = holder;
            }
        }
        Assert.IsNotNull(spear, "the leader's spear flew");
        // The state may play faster or slower than the clip (its speed is set in the Animator).
        Assert.That(leaderVisual.AttackSpeed, Is.GreaterThan(0f));
        Assert.That(spearSeenAt - thrownAt, Is.EqualTo(0.933f / leaderVisual.AttackSpeed).Within(0.15f), "it left the hand at the animation's release point");
        Assert.That(leader.AttackDuration, Is.EqualTo(2f / leaderVisual.AttackSpeed).Within(0.05f), "and the next throw waits for the motion as it actually plays");
        var toDummy = (dummy.transform.position - spear.position); toDummy.y = 0f;
        Assert.That(Vector3.Dot(spear.forward, toDummy.normalized), Is.GreaterThan(0.98f), "the holder points along the flight");
        // The spear mesh's tip is its local +Z end (checked against the model: the head is on that side).
        var mesh = spear.GetComponentInChildren<MeshFilter>().transform;
        Assert.That(Vector3.Dot(mesh.forward, spear.forward), Is.GreaterThan(0.98f), "tip first");
        if (art.ShotPrefab != null) Assert.IsNotNull(arrow, "soldiers' shots use the shot prefab");
        Object.Destroy(squadView.gameObject); Object.Destroy(shotView.gameObject); Object.Destroy(dummy.gameObject);
        LogAssert.NoUnexpectedReceived();
    }
}
