using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

/// <summary>The game scene: several stage maps in one scene, switched by a StageRun.</summary>
public class StageRunTests
{
    [UnityTearDown]
    public IEnumerator LeaveAnEmptyScene()
    {
        GroundHeight.Mask = 0;
        var loaded = SceneManager.GetActiveScene();
        var empty = SceneManager.CreateScene("Empty " + System.Guid.NewGuid().ToString("N").Substring(0, 6));
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(loaded);
    }

    static Vector2 Flat(Vector3 v) => new(v.x, v.z);

    [Test]
    public void BuildOrder_IsTheGameSceneThenTheArena()
    {
        Assert.AreEqual("Game", System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)));
        Assert.AreEqual("Arena", System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(1)));
        foreach (var scene in UnityEditor.EditorBuildSettings.scenes) StringAssert.DoesNotContain("Test_", scene.path, "test scenes are not built");
    }

    [UnityTest]
    public IEnumerator Game_PlaysStageOne_FadesToStageTwo_ThenTheArena_ThenBackToTheTitle()
    {
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
        yield return null;

        var run = Object.FindFirstObjectByType<StageRun>();
        Assert.IsNotNull(run); Assert.IsNotNull(run.Kit, "the run brought the game kit in");
        Assert.That(run.Stages.Count, Is.GreaterThanOrEqualTo(2));
        Assert.AreEqual(1, Object.FindObjectsByType<GameDirector>(FindObjectsSortMode.None).Length, "one game for all stages");
        var director = run.Kit.Director;
        var hud = Object.FindFirstObjectByType<BoneSquadKiting.UI.Hud>();

        // Stage 1 is up, on its title screen.
        Assert.AreEqual(0, run.Current);
        Assert.IsTrue(run.Stages[0].Map.activeSelf); Assert.IsFalse(run.Stages[1].Map.activeSelf);
        Assert.AreEqual(GamePhase.Menu, director.Phase);
        Assert.AreEqual("스테이지 1", director.StageTitle);
        var start1 = run.Stages[0].Map.GetComponentInChildren<StageStart>(true);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start1.transform.position)), Is.LessThan(1.5f), "squad on stage 1's start point");
        Assert.IsTrue(director.Squad.Grid.IsWalkable(director.Squad.Anchor));
        Assert.AreSame(start1.EndPoint, director.Stage.EndPoint);
        Assert.IsTrue(director.Squad.Grid.IsWalkable(Flat(start1.EndPoint.position)), "stage 1's end point is inside its walk area");
        Assert.IsNotNull(director.Waves, "stage 1 has waves");
        Assert.AreSame(run.Stages[0].WavePlan, director.Waves.Plan, "from the stage list");
        var grid1 = director.Squad.Grid;

        hud.OverlayButton.onClick.Invoke();                     // start
        yield return null;
        Assert.AreEqual(GamePhase.Play, director.Phase);
        float until = Time.time + 12f;
        while (director.Mobs.AliveCount == 0 && Time.time < until) yield return null;
        Assert.That(director.Mobs.AliveCount, Is.GreaterThan(0), "waves arrive on stage 1");

        // Clear it (skip the walk) with a casualty, and go on.
        director.Squad.Units[3].Dead = true;
        director.Phase = GamePhase.Win;
        yield return null;
        StringAssert.Contains("클리어", hud.OverlayTitle.text);
        Assert.AreEqual("다음 스테이지 (Enter)", hud.OverlayButtonText.text);

        float darkWhenReady = -1f; bool mapTwoUpWhenReady = false;
        run.StageEntered += i =>
        {
            var fade = run.transform.Find("Stage Fade");
            darkWhenReady = fade != null ? fade.GetComponent<CanvasGroup>().alpha : 0f;
            mapTwoUpWhenReady = run.Stages[1].Map.activeSelf;
        };
        hud.OverlayButton.onClick.Invoke();                     // next stage
        Assert.IsTrue(run.Switching);
        Assert.AreEqual(0, run.Current, "the maps do not swap until the screen is dark");
        Assert.IsTrue(run.Stages[0].Map.activeSelf);
        director.Proceed(); director.Proceed();                 // impatient Enter presses change nothing
        until = Time.realtimeSinceStartup + 10f;
        while (run.Switching && Time.realtimeSinceStartup < until) yield return null;
        yield return null;

        Assert.IsFalse(run.Switching);
        Assert.AreEqual(1f, darkWhenReady, "the next stage was set up under a fully dark screen");
        Assert.IsTrue(mapTwoUpWhenReady);
        Assert.AreEqual(1, run.Current);
        Assert.IsFalse(run.Stages[0].Map.activeSelf); Assert.IsTrue(run.Stages[1].Map.activeSelf);
        Assert.AreSame(director, run.Kit.Director, "the same game carries on");
        Assert.AreEqual(GamePhase.Play, director.Phase, "no title screen between stages");
        Assert.IsFalse(director.Frozen, "and it is running again");
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount, "every stage starts with the full squad");
        Assert.AreEqual(0, director.Mobs.AliveCount, "the last stage's mobs are gone");
        Assert.AreEqual("스테이지 2", director.StageTitle);
        var start2 = run.Stages[1].Map.GetComponentInChildren<StageStart>(true);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start2.transform.position)), Is.LessThan(1.5f), "squad on stage 2's start point");
        Assert.AreNotSame(grid1, director.Squad.Grid, "the walk area was rebuilt for stage 2");
        Assert.IsTrue(director.Squad.Grid.IsWalkable(director.Squad.Anchor));
        Assert.IsTrue(director.Squad.Grid.IsWalkable(Flat(start2.EndPoint.position)), "stage 2's end point is inside its walk area");
        Assert.AreSame(start2.EndPoint, director.Stage.EndPoint);
        Assert.AreSame(run.Stages[1].WavePlan, director.Waves.Plan);
        Assert.That(Vector2.Distance(run.Kit.Cam.LookAt, director.Squad.Anchor), Is.LessThan(3f), "camera on the squad");
        var unit = Object.FindFirstObjectByType<BoneSquadKiting.Squad.SquadView>().transform.GetChild(3);
        Assert.That(unit.position.y, Is.EqualTo(GroundHeight.Y(unit.position.x, unit.position.z)).Within(0.05f), "units stand on stage 2's ground");
        // The art's visibility fade: stage 1's mask is off, stage 2's is on.
        yield return new WaitForEndOfFrame();
        Assert.AreEqual(0f, Shader.GetGlobalFloat("_StageTreeDarkness"), "stage 1's fade mask is no longer applied");
        Assert.That(Shader.GetGlobalFloat("_StageTreeDarkness2"), Is.GreaterThan(0f), "stage 2's is");

        until = Time.time + 12f;
        while (director.Mobs.AliveCount == 0 && Time.time < until) yield return null;
        Assert.That(director.Mobs.AliveCount, Is.GreaterThan(0), "waves arrive on stage 2");
        foreach (var m in director.Mobs.Mobs) Assert.IsTrue(director.Squad.Grid.IsWalkable(m.Pos), "on stage 2's ground");

        // Losing restarts the stage being played, not the first one.
        director.Phase = GamePhase.Lose;
        director.Restart();
        Assert.AreEqual(1, run.Current);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start2.transform.position)), Is.LessThan(1.5f));

        // The last map leads on to the arena scene.
        Assert.IsFalse(director.IsFinalStage);
        director.Phase = GamePhase.Win;
        director.Proceed();
        until = Time.realtimeSinceStartup + 15f;
        while (SceneManager.GetActiveScene().name != "Arena" && Time.realtimeSinceStartup < until) yield return null;
        yield return null; yield return null;
        Assert.AreEqual("Arena", SceneManager.GetActiveScene().name);
        var arena = Object.FindFirstObjectByType<GameDirector>();
        Assert.AreEqual(GamePhase.Play, arena.Phase, "the boss stage starts at once");
        Assert.IsTrue(arena.IsFinalStage);

        // Winning the arena goes back to the game scene's title screen, on stage 1.
        arena.Phase = GamePhase.Win;
        arena.Proceed();
        until = Time.realtimeSinceStartup + 15f;
        while (SceneManager.GetActiveScene().name != "Game" && Time.realtimeSinceStartup < until) yield return null;
        yield return null; yield return null;
        Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
        var again = Object.FindFirstObjectByType<StageRun>();
        Assert.AreEqual(0, again.Current);
        Assert.AreEqual(GamePhase.Menu, again.Kit.Director.Phase, "a new run starts from the title screen");
    }

    [UnityTest]
    public IEnumerator StartStage_BeginsOnALaterMap()
    {
        // Built in memory: two flat maps, each with its own markers.
        GameObject Map(string name, float x)
        {
            var map = new GameObject(name);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(map.transform); ground.transform.position = new Vector3(x, 0f, 0f); ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var end = new GameObject("End"); end.transform.SetParent(map.transform); end.transform.position = new Vector3(x, 0f, -12f);
            var start = new GameObject("Start"); start.transform.SetParent(map.transform); start.transform.position = new Vector3(x, 0f, 12f);
            start.AddComponent<StageStart>().EndPoint = end.transform;
            var outline = new GameObject("Area"); outline.transform.SetParent(map.transform); outline.AddComponent<WalkOutline>();
            foreach (var c in new[] { new Vector2(-15, -15), new Vector2(15, -15), new Vector2(15, 15), new Vector2(-15, 15) })
            { var p = new GameObject("P"); p.transform.SetParent(outline.transform); p.transform.position = new Vector3(x + c.x, 0f, c.y); }
            return map;
        }
        var holder = new GameObject("run"); holder.SetActive(false);
        var a = Map("A", 0f); var b = Map("B", 200f);
        var run = holder.AddComponent<StageRun>();
        run.Stages.Add(new StageRun.Entry { Map = a, Title = "A" });
        run.Stages.Add(new StageRun.Entry { Map = b, Title = "B" });
        run.StartStage = 1; run.FadeSeconds = 0.05f;
        holder.SetActive(true);
        yield return null; yield return null;

        Assert.AreEqual(1, run.Current);
        Assert.IsFalse(a.activeSelf); Assert.IsTrue(b.activeSelf);
        var director = run.Kit.Director;
        Assert.AreEqual("B", director.StageTitle);
        Assert.That(Vector2.Distance(director.Squad.Anchor, new Vector2(200f, 12f)), Is.LessThan(1f));
        Assert.IsNull(director.Waves, "a stage without a wave plan has no waves");
        Assert.IsTrue(director.IsFinalStage, "last map, and no scene after it");
        Assert.IsTrue(director.Squad.Grid.IsWalkable(new Vector2(200f, 0f)));
        Assert.IsFalse(director.Squad.Grid.IsWalkable(new Vector2(0f, 0f)), "the other map's area is not part of this stage");

        run.Advance();
        Assert.IsFalse(run.Switching, "nothing after the last stage");
        run.Enter(0);
        Assert.IsTrue(a.activeSelf); Assert.IsFalse(b.activeSelf);
        Assert.That(Vector2.Distance(director.Squad.Anchor, new Vector2(0f, 12f)), Is.LessThan(1f));
        Assert.IsFalse(director.IsFinalStage);
    }
}
