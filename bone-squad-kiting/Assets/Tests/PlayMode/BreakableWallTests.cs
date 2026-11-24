using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

/// <summary>Stage 2's gate: a target that blocks the way to the end point until it is destroyed.</summary>
public class BreakableWallTests
{
    [UnityTest]
    public IEnumerator StageTwo_TheGateBlocksTheWay_TakesDamage_AndBreaksOpenToTheEndPoint()
    {
        StageRun.RequestedStage = 1;
        GameDirector.LoadStageScene("Game");
        float until = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != "Game" && Time.realtimeSinceStartup < until) yield return null;
        yield return null; yield return null;
        var run = Object.FindFirstObjectByType<StageRun>();
        var director = run.Kit.Director;
        Assert.AreEqual(1, run.Current); Assert.AreEqual(GamePhase.Play, director.Phase);
        var wall = run.Stages[1].Map.GetComponentInChildren<BreakableWall>();
        Assert.IsNotNull(wall, "stage 2 has the gate");
        yield return null;

        Assert.IsTrue(wall.EndsReinforcements);
        Assert.IsFalse(wall.ExitGuide.activeInHierarchy);
        var arrowRest = wall.ExitGuide.transform.localPosition;
        var hud = Object.FindFirstObjectByType<BoneSquadKiting.UI.Hud>();
        hud.Refresh();
        Assert.IsFalse(hud.BossBar.activeSelf, "the stage starts outside the gate encounter");
        var grid = director.Squad.Grid;
        var gate = wall.Pos; var inside = gate + new Vector2(0f, 10f); var before = gate + new Vector2(0f, -4.5f);
        Assert.Contains(wall, director.Enemies.Enemies, "the squad can shoot it");
        Assert.IsTrue(wall.Alive); Assert.AreEqual(wall.MaxHp, wall.Health);
        Assert.IsTrue(grid.IsWalkable(before), "the road up to the gate");
        Assert.IsFalse(grid.IsWalkable(gate), "the gate itself is shut");
        Assert.IsTrue(grid.IsWalkable(inside), "the yard behind it is ground, but cut off");
        Assert.IsFalse(grid.LineOpen(before, inside));
        var end = run.Kit.Setup.EndPoint;
        Assert.That(end.position.z, Is.GreaterThan(wall.transform.position.z + 5f), "the end point is inside the walls");
        foreach (var r in wall.Renderers) Assert.AreEqual("Bone Squad/Mob PBR", r.sharedMaterial.shader.name, "outline material, like a mini boss");
        Assert.IsNull(BreakableWall.Shown, "far away and unhurt: the bar is not the gate's yet");

        wall.ApplyDamage(wall.MaxHp * 0.25f);
        yield return null;
        Assert.That(wall.Flash, Is.GreaterThan(0.3f), "it flashes when hit");
        Assert.IsNull(BreakableWall.Shown, "a distant hit cannot reveal the gate");
        director.Squad.Anchor = gate - Vector2.up * (wall.ShowBarDistance - 1f);
        director.Squad.Destination = director.Squad.Anchor;
        yield return null; yield return null;
        hud.Refresh();
        Assert.AreSame(wall, BreakableWall.Shown);
        Assert.IsTrue(hud.BossBar.activeSelf);
        StringAssert.Contains(wall.DisplayName, hud.BossText.text);
        Assert.AreEqual(0.75f, wall.HpFrac, 0.001f);
        foreach (var go in wall.BreakObjects) Assert.IsTrue(go.activeSelf);

        wall.ApplyDamage(wall.MaxHp);
        yield return null;
        Assert.IsTrue(wall.Broken); Assert.IsFalse(wall.Alive);
        CollectionAssert.DoesNotContain(director.Enemies.Enemies, wall);
        foreach (var go in wall.BreakObjects) Assert.IsFalse(go.activeSelf, "the gate is gone");
        Assert.IsNull(BreakableWall.Shown);
        Assert.IsTrue(wall.ExitGuide.activeInHierarchy);
        hud.Refresh();
        Assert.IsFalse(hud.BossBar.activeSelf, "the broken gate and its fallen guard leave no encounter bar");
        var motion = wall.ExitGuide.GetComponent<Animation>();
        Assert.IsTrue(motion.IsPlaying(motion.clip.name));
        var state = motion[motion.clip.name];
        state.normalizedTime = .5f;
        motion.Sample();
        var top = wall.ExitGuide.transform.localPosition;
        Assert.Greater(top.z, arrowRest.z + .5f, "the arrows travel toward the warp");
        Assert.Greater(top.y, arrowRest.y + .1f, "the arrows lift visibly");
        state.normalizedTime = 1f;
        motion.Sample();
        Assert.Less(Vector3.Distance(arrowRest, wall.ExitGuide.transform.localPosition), .02f, "the loop returns to its starting point");
        Assert.IsTrue(director.Waves.Stopped);
        director.Waves.SpawnNow();
        Assert.AreEqual(0, director.Waves.Tick(director.Squad, director.Mobs, director.Enemies, 15f));
        Assert.AreEqual(0, director.Mobs.AliveCount);
        Assert.IsTrue(grid.IsWalkable(gate), "and the way through is open");
        Assert.IsTrue(grid.LineOpen(before, inside));

        Assert.AreEqual(GamePhase.Play, director.Phase);
        director.Squad.Anchor = new Vector2(end.position.x, end.position.z);
        until = Time.time + 3f;
        while (director.Phase != GamePhase.Win && Time.time < until) yield return null;
        Assert.AreEqual(GamePhase.Win, director.Phase, "reaching the end point inside clears the stage");
        director.Restart();
        yield return null;
        Assert.IsTrue(wall.Alive);
        Assert.IsFalse(wall.ExitGuide.activeInHierarchy);
        Assert.IsFalse(director.Waves.Stopped);
        LogAssert.NoUnexpectedReceived();
    }
}
