using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;
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
    public void RevivedUnit_AppearsBesideItsHelper_ThenWalksToAFreeSlot()
    {
        Kill(20);
        _dir.TryRevive();
        float distance = -1f;
        _dir.ReviveCompleted += (helper, revived) =>
            distance = Vector2.Distance(new Vector2(helper.X, helper.Z), new Vector2(revived.X, revived.Z));
        Run(3.05f);
        Assert.That(distance, Is.EqualTo(_dir.Revives.AppearRadius).Within(0.001f));

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
    public void MovingSquad_RevivesAtTheOffsetSelectedAtStart()
    {
        Kill(9);
        var startAnchor = _fc.Anchor;
        Vector2 startOffset = default;
        Vector2? completedOffset = null;
        _dir.ReviveStarted += (reviver, target) => startOffset = _dir.Revives.Active[0].AppearOffset;
        _dir.ReviveCompleted += (reviver, target) =>
        {
            Assert.That(Vector2.Distance(startAnchor, _fc.Anchor), Is.GreaterThan(0.1f));
            completedOffset = new Vector2(target.X - reviver.X, target.Z - reviver.Z);
        };
        Assert.IsTrue(_dir.TryRevive());
        Assert.That(startOffset.magnitude, Is.EqualTo(_dir.Revives.AppearRadius).Within(0.001f));
        var pair = _dir.Revives.Active.Single();
        var helperStart = new Vector2(pair.Reviver.X, pair.Reviver.Z);
        var other = _fc.Units.First(u => !u.Dead && u != pair.Reviver);
        var otherStart = new Vector2(other.X, other.Z);
        _dir.IssueOrder(SquadOrder.Move(startAnchor + new Vector2(6f, 0)));
        Run(2.5f);
        Assert.AreEqual(helperStart, new Vector2(pair.Reviver.X, pair.Reviver.Z));
        Assert.IsFalse(pair.Reviver.Moving);
        Assert.Greater(Vector2.Distance(otherStart, new Vector2(other.X, other.Z)), 0.1f);
        Run(0.7f);
        Assert.Greater(Vector2.Distance(helperStart, new Vector2(pair.Reviver.X, pair.Reviver.Z)), 0.01f);
        Assert.AreEqual(0, _dir.DeadCount);
        Assert.IsTrue(completedOffset.HasValue);
        Assert.That(Vector2.Distance(completedOffset.Value, startOffset), Is.LessThan(0.001f));
    }

    [Test]
    public void CancellingOneOfTwoRevives_LeavesTheOtherRunning()
    {
        Kill(3, 4);
        Assert.IsTrue(_dir.TryRevive());
        Run(1f);
        Assert.IsTrue(_dir.TryRevive());
        var first = _dir.Revives.Active[0];
        var second = _dir.Revives.Active[1];
        first.Reviver.Dead = true;
        Run(0.1f);

        CollectionAssert.AreEqual(new[] { second }, _dir.Revives.Active);
        Run(3f);
        Assert.IsTrue(first.Target.Dead);
        Assert.IsFalse(second.Target.Dead);
        Assert.IsEmpty(_dir.Revives.Active);
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

    [UnityTest]
    public IEnumerator RescueUsesTheSelectedModelsAndOnlyRestoresTheTargetAfterItStands()
    {
        var root = new GameObject("Squad models");
        root.transform.SetParent(_fc.transform);
        var view = root.AddComponent<SquadView>();
        view.Squad = _fc;
        view.Art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        Kill(7);
        yield return null;
        yield return null;
        var target = root.transform.GetChild(7);
        var body = target.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var originalMaterial = body.sharedMaterial;
        Assert.IsFalse(target.gameObject.activeSelf);
        Assert.IsNull(root.GetComponent<ReviveBoneFx>());

        Assert.IsTrue(_dir.TryRevive());
        var pair = _dir.Revives.Active.Single();
        var helper = root.transform.GetChild(pair.Reviver.Id).GetComponentInChildren<Animator>();
        var helperBody = helper.GetComponentInChildren<SkinnedMeshRenderer>();
        var originalHelperMaterial = helperBody.sharedMaterial;
        var climber = target.GetComponentInChildren<Animator>(true);
        yield return null;
        yield return null;
        Assert.IsTrue(target.gameObject.activeSelf);
        Assert.IsTrue(helper.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ReviveHelper"));
        Assert.IsTrue(climber.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ReviveClimber"));
        Assert.AreSame(view.Art.UnitRescueMaterial, body.sharedMaterial);
        Assert.AreSame(pair.Reviver.IsLeader ? view.Art.LeaderRescueMaterial : view.Art.UnitRescueMaterial, helperBody.sharedMaterial);
        Assert.AreEqual(0.1f, helperBody.sharedMaterial.GetColor("_FresnelColor").a);
        Assert.AreEqual(0.1f, body.sharedMaterial.GetColor("_FresnelColor").a);
        var hips = climber.GetBoneTransform(HumanBodyBones.Hips);
        Assert.Less(hips.position.y, target.position.y);
        Assert.IsEmpty(root.GetComponentsInChildren<ParticleSystem>().Where(p => p.name == "ReviveDust(Clone)"));
        var orbit = root.transform.Find("Rescue bone orbit");
        Assert.AreEqual(3, orbit.childCount);
        var bone = orbit.GetChild(0);
        var initialOffset = bone.localPosition;
        Run(0.24f);
        yield return null;
        yield return null;
        Assert.Greater(Vector3.Distance(initialOffset, bone.localPosition), 0.2f);
        Assert.That(Vector2.Distance(_fc.Anchor, new Vector2(bone.position.x, bone.position.z)),
            Is.EqualTo(3.2f).Within(0.03f), "the bone reaches the full squad orbit before converging");
        Run(1.26f);
        yield return null;
        yield return null;
        var dust = root.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == "ReviveDust(Clone)");
        foreach (Transform fragment in orbit)
            Assert.Less(Vector3.Distance(fragment.position, dust.transform.position), 0.001f);
        var head = climber.GetBoneTransform(HumanBodyBones.Head).position;
        Assert.That(Vector2.Distance(new Vector2(head.x, head.z),
            new Vector2(dust.transform.position.x, dust.transform.position.z)), Is.LessThan(0.05f));
        Assert.That(dust.transform.position.y, Is.EqualTo(BoneSquadKiting.Stage.GroundHeight.Y(head.x, head.z) + 0.04f).Within(0.01f));
        Run(1.4f);
        yield return null;
        yield return null;
        Assert.Greater(hips.position.y, target.position.y);
        Assert.IsFalse(orbit.gameObject.activeSelf);
        Assert.IsTrue(pair.Target.Dead);
        Run(0.12f);
        yield return null;
        yield return null;
        Assert.IsFalse(pair.Target.Dead);
        Assert.AreEqual(GameConstants.UnitMax, pair.Target.Hp);
        Assert.IsTrue(target.gameObject.activeSelf);
        Assert.IsFalse(climber.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ReviveClimber"));
        Assert.AreSame(originalMaterial, body.sharedMaterial);
        Assert.AreSame(originalHelperMaterial, helperBody.sharedMaterial);
    }

    ReviveBoneFx BindBoneFx()
    {
        if (Camera.main == null)
        {
            var camera = new GameObject("Revive test camera").AddComponent<Camera>();
            camera.transform.SetParent(_fc.transform);
            camera.transform.position = new Vector3(0, 8, -8);
            camera.transform.LookAt(Vector3.zero);
            camera.tag = "MainCamera";
        }
        var fx = new GameObject("Revive bones").AddComponent<ReviveBoneFx>();
        fx.transform.SetParent(_fc.transform);
        fx.Bind(_dir, AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset"));
        return fx;
    }

    static void AssertFragments(ReviveBoneFx fx, params string[] expected)
    {
        var visible = fx.GetComponentsInChildren<MeshRenderer>().Select(renderer => renderer.gameObject.name);
        CollectionAssert.AreEquivalent(expected.Select(name => name + "(Clone)"), visible);
    }

    [UnityTest]
    public IEnumerator FragmentsEnterAtZeroQuarterAndHalfProgress_AndHoldWhilePaused()
    {
        var fx = BindBoneFx();
        Kill(5);
        Assert.IsTrue(_dir.TryRevive());
        yield return null;
        AssertFragments(fx, "BrokenBone");

        _dir.Step(0.625f);
        yield return null;
        AssertFragments(fx, "BrokenBone");
        var first = fx.GetComponentInChildren<MeshRenderer>().transform;
        var pausedPosition = first.position;
        _dir.TogglePause();
        _dir.Step(1f);
        yield return null;
        AssertFragments(fx, "BrokenBone");
        Assert.AreEqual(pausedPosition, first.position);

        _dir.TogglePause();
        _dir.Step(0.125f);
        yield return null;
        AssertFragments(fx, "BrokenBone", "Vertebra");
        _dir.Step(0.625f);
        yield return null;
        AssertFragments(fx, "BrokenBone", "Vertebra");
        _dir.Step(0.125f);
        yield return null;
        AssertFragments(fx, "BrokenBone", "Vertebra", "Scapula");
    }

    [UnityTest]
    public IEnumerator CancelBeforeTheSecondFragment_NoMoreBonesArrive()
    {
        var fx = BindBoneFx();
        Kill(5);
        Assert.IsTrue(_dir.TryRevive());
        _dir.Step(0.625f);
        yield return null;
        AssertFragments(fx, "BrokenBone");

        _dir.Revives.Active[0].Reviver.Dead = true;
        _dir.Step(0.125f);
        yield return null;
        AssertFragments(fx, "BrokenBone");
        _dir.Step(1f);
        yield return null;
        AssertFragments(fx);
        Assert.IsTrue(_fc.Units[5].Dead);
    }
}
