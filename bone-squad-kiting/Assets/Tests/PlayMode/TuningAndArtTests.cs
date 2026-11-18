using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Visuals;

/// <summary>The shared tuning asset and the swappable unit models.</summary>
public class TuningAndArtTests
{
    FormationController _fc;
    GameDirector _dir;
    GameTuning _tuning;

    [SetUp]
    public void SetUp()
    {
        _fc = SquadTestRig.MakeFc();
        _dir = _fc.gameObject.AddComponent<GameDirector>();
        _dir.enabled = false; _dir.Squad = _fc; _dir.HasBoss = false;
        _dir.ClearRule = BoneSquadKiting.Stage.StageClearRule.ReachEnd;
        _tuning = ScriptableObject.CreateInstance<GameTuning>();
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(_fc.gameObject); Object.DestroyImmediate(_tuning); }

    [Test]
    public void TuningAsset_DrivesEveryStagesNumbers_AndCanBeChangedWhilePlaying()
    {
        _tuning.UnitFireRange = 7f; _tuning.SquadEngageRange = 6f; _tuning.ReviveSeconds = 1.5f;
        _tuning.RotationSpeed = 90f; _tuning.RotationStepDegrees = 30f; _tuning.SeatSize = 0.7f;
        _tuning.ThreatRadius = 2f; _tuning.MobMaxAlive = 50; _tuning.MobSwitchDistance = 9f; _tuning.ReviveAppearRadius = 1.1f;
        _tuning.AnchorSpeed = 3f; _tuning.UnitBaseSpeed = 5f; _tuning.UnitSpeedDistGain = 1f; _tuning.UnitSpeedDistCap = 2f; _tuning.UnitArrive = 0.05f; _tuning.AnchorArrive = 0.1f;
        _dir.Tuning = _tuning;
        _dir.Restart();
        _dir.Step(0.02f);
        Assert.AreEqual(3f, _fc.AnchorSpeed); Assert.AreEqual(0.1f, _fc.AnchorArrive);
        Assert.AreEqual(5f, _fc.Units[3].BaseSpeed); Assert.AreEqual(1f, _fc.Units[3].SpeedDistGain);
        Assert.AreEqual(2f, _fc.Units[3].SpeedDistCap); Assert.AreEqual(0.05f, _fc.Units[3].Arrive);
        var before = _fc.Anchor; _fc.SetDestination(_fc.Anchor + new Vector2(5f, 0f)); _dir.Step(0.1f);
        Assert.That((_fc.Anchor - before).magnitude, Is.EqualTo(0.3f).Within(1e-3f), "the center travels at the asset's speed");
        _fc.Halt();

        Assert.AreEqual(7f, _dir.UnitFireRange); Assert.AreEqual(6f, _dir.SquadEngageRange); Assert.AreEqual(1.5f, _dir.ReviveSeconds);
        Assert.AreEqual(90f, _fc.RotationSpeed); Assert.AreEqual(30f, _fc.RotationStepDegrees); Assert.AreEqual(0.7f, _fc.SeatSize);
        Assert.AreEqual(7f, _dir.Shots.FireRange); Assert.AreEqual(2f, _dir.Shots.ThreatRadius);
        Assert.AreEqual(50, _dir.Mobs.MaxAlive); Assert.AreEqual(9f, _dir.Mobs.SwitchDistance);
        Assert.AreEqual(1.1f, _dir.Revives.AppearRadius);

        _tuning.UnitFireRange = 3f; _tuning.RotationSpeed = 10f;      // edited mid-match
        _dir.Step(0.02f);
        Assert.AreEqual(3f, _dir.Shots.FireRange);
        Assert.AreEqual(10f, _fc.RotationSpeed);
    }

    [Test]
    public void WithoutATuningAsset_TheComponentsOwnFieldsApply()
    {
        _dir.UnitFireRange = 11f; _fc.RotationSpeed = 33f;
        _dir.Restart(); _dir.Step(0.02f);
        Assert.AreEqual(11f, _dir.Shots.FireRange);
        Assert.AreEqual(33f, _fc.RotationSpeed);
    }

    [Test]
    public void Camera_ReadsItsNumbersFromTheTuningAsset()
    {
        var go = new GameObject("cam"); go.transform.rotation = Quaternion.Euler(60, 180, 0);
        var cam = go.AddComponent<EdgeScrollCamera>(); cam.enabled = false;
        _tuning.CameraScrollSpeed = 3f; _tuning.CameraEdgeWidth = 40f; _tuning.CameraDistance = 20f;
        cam.Tuning = _tuning; cam.Apply();
        Assert.AreEqual(3f, cam.ScrollSpeed); Assert.AreEqual(40f, cam.EdgeWidth); Assert.AreEqual(20f, cam.Distance);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ProjectAssets_EveryStagePointsAtTheSameTuningAndArtSet()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/GameTuning.asset");
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        Assert.IsNotNull(tuning); Assert.IsNotNull(art);
        string tuningGuid = AssetDatabase.AssetPathToGUID("Assets/Data/GameTuning.asset");
        string artGuid = AssetDatabase.AssetPathToGUID("Assets/Data/Art/ArtSet.asset");
        foreach (var scene in new[] { "Test_Stage1", "Test_Stage2", "Test_Stage3", "Test_Stage4", "Arena" })
        {
            string text = System.IO.File.ReadAllText($"Assets/Scenes/{scene}.unity");
            StringAssert.Contains(tuningGuid, text, scene + " references the shared tuning asset");
            StringAssert.Contains(artGuid, text, scene + " references the shared art set");
        }
    }

    [UnityTest]
    public IEnumerator UnitVisual_PlaysStatesByName_AndSilentlySkipsMissingOnes()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        if (art == null || art.UnitPrefab == null) Assert.Ignore("no unit model in the art set");
        var go = SquadView.BuildUnit(1, art, 0.34f, 1.4f, null);
        var visual = go.AddComponent<UnitVisual>();
        visual.States = art.UnitStates;
        yield return null;

        Assert.IsNotNull(visual.Animator, "found the model's Animator");
        Assert.IsTrue(visual.Has(VisualState.Idle));
        Assert.IsTrue(visual.Has(VisualState.Run), "the run state name in the art set exists in the model");
        Assert.IsFalse(visual.Has(VisualState.Attack), "shooting is an always-on upper-body layer, not a state");
        Assert.IsFalse(visual.Has(VisualState.Die), "no death animation has been made yet");

        visual.SetState(VisualState.Run); yield return null;
        visual.PlayAttack(0.05f); yield return null;
        visual.SetState(VisualState.Die); yield return null;          // missing: must not throw or log an error
        visual.States = new AnimStates { Idle = "NoSuchState", Run = "", Attack = null, Die = "AlsoMissing" };
        visual.SetState(VisualState.Idle); visual.SetState(VisualState.Run); visual.PlayAttack(); visual.SetState(VisualState.Die);
        yield return null;
        LogAssert.NoUnexpectedReceived();
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator SquadView_UsesTheModels_LeaderFirst_AndTurnsThemToFaceTheirWay()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        if (art == null || art.UnitPrefab == null) Assert.Ignore("no unit model in the art set");
        _fc.HasLeader = true; _fc.Init();
        var viewGo = new GameObject("view"); viewGo.transform.SetParent(_fc.transform);
        var view = viewGo.AddComponent<SquadView>(); view.Squad = _fc; view.Art = art;
        yield return null; yield return null;

        Assert.AreEqual(25, viewGo.transform.childCount);
        Assert.IsNotNull(viewGo.transform.GetChild(0).GetComponent<UnitVisual>(), "models, not capsules");
        if (art.LeaderPrefab != null) StringAssert.StartsWith("Leader", viewGo.transform.GetChild(0).name);
        Assert.That(viewGo.transform.GetChild(3).position.y, Is.EqualTo(0f).Within(1e-3f), "models stand on the ground");

        _fc.SetDestination(_fc.Anchor + new Vector2(6f, 0f));         // walk toward +x
        for (int i = 0; i < 30; i++) { _fc.Tick(0.02f); yield return null; }
        var forward = viewGo.transform.GetChild(3).rotation * Quaternion.Euler(0, -art.ModelYawOffset, 0) * Vector3.forward;
        Assert.That(Vector3.Dot(forward, Vector3.right), Is.GreaterThan(0.9f), "turned to face the way it walks");
        Assert.AreEqual(VisualState.Run, viewGo.transform.GetChild(3).GetComponent<UnitVisual>().Current);

        _fc.Units[5].Dead = true;
        yield return null;
        Assert.IsFalse(viewGo.transform.GetChild(5).gameObject.activeSelf, "no death animation: the model just disappears");
    }

    [Test]
    public void WithoutAnArtSet_UnitsAreStillPlaceholderCapsules()
    {
        var go = SquadView.BuildUnit(3, null, 0.34f, 1.4f, null);
        Assert.IsNotNull(go.GetComponent<MeshFilter>());
        Assert.IsNull(go.GetComponent<UnitVisual>());
        Object.DestroyImmediate(go);
    }
}
