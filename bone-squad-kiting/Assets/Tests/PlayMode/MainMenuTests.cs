using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;
using BoneSquadKiting.UI;

/// <summary>The game scene's main menu: new game, continue (from the stage after the last one cleared), quit.</summary>
public class MainMenuTests
{
    [SetUp] public void NoSave() => GameProgress.Clear();

    [UnityTearDown]
    public IEnumerator LeaveAnEmptyScene()
    {
        GameProgress.Clear();
        GroundHeight.Mask = 0;
        var loaded = SceneManager.GetActiveScene();
        var empty = SceneManager.CreateScene("Empty " + System.Guid.NewGuid().ToString("N").Substring(0, 6));
        SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(loaded);
    }

    static Vector2 Flat(Vector3 v) => new(v.x, v.z);

    static IEnumerator LoadGame()
    {
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
        yield return null; yield return null;
    }

    [UnityTest]
    public IEnumerator NewGame_ClearingAStage_ThenContinue_StartsTheNextStageFromItsStartPoint()
    {
        yield return LoadGame();
        var run = Object.FindFirstObjectByType<StageRun>();
        var menu = Object.FindFirstObjectByType<MainMenu>();
        var hud = Object.FindFirstObjectByType<Hud>();
        var director = run.Kit.Director;
        Assert.IsNotNull(menu, "the game scene has the main menu");

        // Title screen: the three buttons, no save yet, and the HUD's own start overlay stays out of the way.
        Assert.IsTrue(menu.Showing);
        Assert.AreEqual(GamePhase.Menu, director.Phase);
        Assert.IsNotNull(menu.NewGameButton); Assert.IsNotNull(menu.ContinueButton); Assert.IsNotNull(menu.QuitButton);
        Assert.AreEqual("새 게임", menu.NewGameButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.AreEqual("게임 종료", menu.QuitButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.IsFalse(menu.ContinueButton.interactable, "nothing to continue yet");
        Assert.IsFalse(hud.Overlay.activeSelf, "the menu is the title screen");
        menu.ContinueButton.onClick.Invoke();
        Assert.AreEqual(GamePhase.Menu, director.Phase, "continue without a save does nothing");

        // New game: stage 1, from its start point, full squad.
        menu.NewGameButton.onClick.Invoke();
        yield return null;
        Assert.AreEqual(GamePhase.Play, director.Phase);
        Assert.AreEqual(0, run.Current);
        Assert.IsFalse(menu.Showing, "the menu is gone while playing");
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

        // Quit and come back: the game scene loads on its menu again, and continue is offered.
        yield return LoadGame();
        run = Object.FindFirstObjectByType<StageRun>();
        menu = Object.FindFirstObjectByType<MainMenu>();
        director = run.Kit.Director;
        menu.Refresh();
        Assert.IsTrue(menu.Showing);
        Assert.AreEqual(0, run.Current, "the title shows stage 1's map");
        Assert.IsTrue(menu.ContinueButton.interactable);
        StringAssert.Contains(run.Stages[1].Title, menu.ContinueLabel.text, "it says where continue goes");

        menu.ContinueButton.onClick.Invoke();
        until = Time.realtimeSinceStartup + 10f;
        while ((run.Switching || run.Current != 1) && Time.realtimeSinceStartup < until) yield return null;
        yield return null;
        Assert.AreEqual(1, run.Current, "continue went to stage 2");
        Assert.IsTrue(run.Stages[1].Map.activeSelf); Assert.IsFalse(run.Stages[0].Map.activeSelf);
        Assert.AreEqual(GamePhase.Play, director.Phase);
        Assert.IsFalse(director.Frozen);
        Assert.IsFalse(menu.Showing);
        var start2 = run.Stages[1].Map.GetComponentInChildren<StageStart>(true);
        Assert.That(Vector2.Distance(director.Squad.Anchor, Flat(start2.transform.position)), Is.LessThan(1.5f), "from stage 2's start point");
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount);
        Assert.AreEqual(1, GameProgress.NextStage, "the save stays until the next clear");

        // New game from the menu forgets the save and plays stage 1.
        yield return LoadGame();
        run = Object.FindFirstObjectByType<StageRun>();
        menu = Object.FindFirstObjectByType<MainMenu>();
        menu.NewGameButton.onClick.Invoke();
        yield return null;
        Assert.AreEqual(0, run.Current);
        Assert.AreEqual(GamePhase.Play, run.Kit.Director.Phase);
        Assert.IsFalse(GameProgress.HasSave, "new game starts over");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Continue_PastTheLastMap_GoesStraightToTheBossScene()
    {
        yield return LoadGame();
        var run = Object.FindFirstObjectByType<StageRun>();
        if (string.IsNullOrEmpty(run.NextScene)) Assert.Ignore("no scene after the maps");
        GameProgress.Save(run.Stages.Count);
        var menu = Object.FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        Assert.IsTrue(menu.ContinueButton.interactable);
        menu.ContinueButton.onClick.Invoke();
        float until = Time.realtimeSinceStartup + 15f;
        while (SceneManager.GetActiveScene().name != run.NextScene && Time.realtimeSinceStartup < until) yield return null;
        yield return null; yield return null;
        var arena = Object.FindFirstObjectByType<GameDirector>();
        Assert.AreEqual(GamePhase.Play, arena.Phase, "the boss stage starts at once");
        Assert.IsFalse(arena.HasMainMenu);
    }
}
