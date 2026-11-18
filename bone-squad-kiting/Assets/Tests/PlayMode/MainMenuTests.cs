using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;
using BoneSquadKiting.UI;

/// <summary>The main menu scene: new game, continue (from the stage after the last one cleared), quit.</summary>
public class MainMenuTests
{
    [SetUp] public void NoSave() { GameProgress.Clear(); StageRun.RequestedStage = -1; }

    [UnityTearDown]
    public IEnumerator LeaveAnEmptyScene()
    {
        GameProgress.Clear(); StageRun.RequestedStage = -1;
        GroundHeight.Mask = 0;
        var loaded = SceneManager.GetActiveScene();
        var empty = SceneManager.CreateScene("Empty " + System.Guid.NewGuid().ToString("N").Substring(0, 6));
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(loaded);
    }

    static Vector2 Flat(Vector3 v) => new(v.x, v.z);

    static IEnumerator LoadMenu()
    {
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        yield return null; yield return null;
    }

    static IEnumerator WaitForScene(string name)
    {
        float until = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < until) yield return null;
        yield return null; yield return null;
        Assert.AreEqual(name, SceneManager.GetActiveScene().name);
    }

    [Test]
    public void TheMenuIsTheFirstSceneOfTheBuild()
    {
        Assert.AreEqual("MainMenu", System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)));
    }

    [UnityTest]
    public IEnumerator Menu_ShowsNoGame_NewGamePlaysStageOne_ClearingItLetsContinueStartStageTwo()
    {
        yield return LoadMenu();
        var menu = Object.FindFirstObjectByType<MainMenu>();
        Assert.IsNotNull(menu);

        // Nothing of the game is here: no maps, no squad, no game camera to move.
        Assert.IsNull(Object.FindFirstObjectByType<StageRun>(), "no stage maps in the menu scene");
        Assert.IsNull(Object.FindFirstObjectByType<GameDirector>(), "no game running behind the menu");
        Assert.IsNull(Object.FindFirstObjectByType<EdgeScrollCamera>(), "no game camera");
        Assert.IsNotNull(menu.Burst, "the commander's hand waits under the ground");
        var camBefore = Camera.main.transform.position;

        Assert.AreEqual("새 게임", menu.NewGameButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.AreEqual("게임 종료", menu.QuitButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.IsFalse(menu.ContinueButton.interactable, "nothing to continue yet");
        menu.ContinueButton.onClick.Invoke();
        yield return null;
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "continue without a save does nothing");

        // New game: the game scene comes up already playing stage 1 from its start point, only its map on.
        // The hand is under the ground until a game is started; then it bursts out before the game loads.
        Transform handBone = null; float highest = float.MinValue;
        foreach (var t in menu.Burst.GetComponentsInChildren<Transform>()) if (t.position.y > highest && t.GetComponent<Renderer>() == null && t.childCount == 0) { highest = t.position.y; handBone = t; }
        float buriedY = handBone.position.y;
        Assert.That(buriedY, Is.LessThan(0.05f), "nothing of the commander shows above the ground yet");
        menu.NewGameButton.onClick.Invoke();
        Assert.IsTrue(menu.Starting);
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "the game waits for the burst to play");
        Assert.That(handBone.position.y, Is.GreaterThan(buriedY + 0.2f), "the hand has come up out of the ground");
        Assert.IsFalse(menu.NewGameButton.gameObject.activeInHierarchy, "the buttons are gone during the burst");
        Assert.AreEqual(camBefore, Camera.main.transform.position, "the menu camera does not move");
        yield return WaitForScene("Game");
        var run = Object.FindFirstObjectByType<StageRun>();
        var director = run.Kit.Director;
        Assert.AreEqual(0, run.Current);
        Assert.AreEqual(GamePhase.Play, director.Phase, "no second title screen");
        Assert.IsTrue(run.Stages[0].Map.activeSelf); Assert.IsFalse(run.Stages[1].Map.activeSelf);
        var start1 = run.Stages[0].Map.GetComponentInChildren<StageStart>(true);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start1.transform.position)), Is.LessThan(1.5f));
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount);
        Assert.IsFalse(GameProgress.HasSave);

        // Clear stage 1 by standing on its end point: the save now points at stage 2.
        director.ClearRule = StageClearRule.ReachEnd;
        director.Squad.Anchor = Flat(start1.EndPoint.position);
        float until = Time.time + 3f;
        while (director.Phase != GamePhase.Win && Time.time < until) yield return null;
        Assert.AreEqual(GamePhase.Win, director.Phase);
        Assert.AreEqual(1, GameProgress.NextStage, "clearing stage 1 saves stage 2 as the next one");
        Assert.AreEqual("", GameProgress.NextScene);

        // Quit and come back: continue is offered, and goes to stage 2's start point with only its map on.
        yield return LoadMenu();
        menu = Object.FindFirstObjectByType<MainMenu>();
        Assert.IsTrue(menu.ContinueButton.interactable);
        Assert.AreEqual("이어하기", menu.ContinueLabel.text, "just the word, no stage name");
        menu.BurstSeconds = 0.2f;
        menu.ContinueButton.onClick.Invoke();
        yield return WaitForScene("Game");
        run = Object.FindFirstObjectByType<StageRun>();
        director = run.Kit.Director;
        Assert.AreEqual(1, run.Current, "continue went to stage 2");
        Assert.AreEqual(1, Object.FindObjectsByType<GameDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "one game, even when starting on a later map");
        Assert.IsTrue(run.Stages[1].Map.activeSelf); Assert.IsFalse(run.Stages[0].Map.activeSelf);
        Assert.AreEqual(GamePhase.Play, director.Phase);
        var start2 = run.Stages[1].Map.GetComponentInChildren<StageStart>(true);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start2.transform.position)), Is.LessThan(1.5f), "from stage 2's start point");
        Assert.IsTrue(director.Squad.Grid.IsWalkable(director.Squad.Anchor));
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount);
        Assert.AreEqual(1, GameProgress.NextStage, "the save stays until the next clear");
        Assert.AreEqual(-1, StageRun.RequestedStage, "the request is used once");

        // Clearing a later map moves the save on again; after the last map it names the scene that follows.
        director.ClearRule = StageClearRule.ReachEnd;
        director.Squad.Anchor = Flat(start2.EndPoint.position);
        until = Time.time + 3f;
        while (director.Phase != GamePhase.Win && Time.time < until) yield return null;
        Assert.AreEqual(GamePhase.Win, director.Phase);
        Assert.AreEqual(2, GameProgress.NextStage);
        Assert.AreEqual(run.Stages.Count > 2 ? "" : run.NextScene, GameProgress.NextScene, "the scene after the maps is saved only once the last map is cleared");

        // New game from the menu forgets the save and plays stage 1.
        yield return LoadMenu();
        menu = Object.FindFirstObjectByType<MainMenu>();
        menu.BurstSeconds = 0.2f;
        menu.NewGameButton.onClick.Invoke();
        yield return WaitForScene("Game");
        run = Object.FindFirstObjectByType<StageRun>();
        Assert.AreEqual(0, run.Current);
        Assert.AreEqual(GamePhase.Play, run.Kit.Director.Phase);
        Assert.IsFalse(GameProgress.HasSave, "new game starts over");
    }

    [UnityTest]
    public IEnumerator Continue_AfterTheLastMap_GoesStraightToTheBossScene()
    {
        GameProgress.Save(2, "Arena");
        yield return LoadMenu();
        var menu = Object.FindFirstObjectByType<MainMenu>();
        Assert.IsTrue(menu.ContinueButton.interactable);
        Assert.AreEqual("이어하기", menu.ContinueLabel.text);
        menu.BurstSeconds = 0.2f;
        menu.ContinueButton.onClick.Invoke();
        yield return WaitForScene("Arena");
        var arena = Object.FindFirstObjectByType<GameDirector>();
        Assert.AreEqual(GamePhase.Play, arena.Phase, "the boss stage starts at once");
    }
}
