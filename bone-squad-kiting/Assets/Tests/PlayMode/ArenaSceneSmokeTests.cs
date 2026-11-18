using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;

/// <summary>
/// Loads the real Arena scene and plays it through the director (no mouse input), checking that the
/// scene wiring works end to end. Also writes screenshots to Logs/ for a visual check.
/// </summary>
public class ArenaSceneSmokeTests
{
    [UnityTest]
    public IEnumerator Arena_PlaysAndRenders()
    {
#if UNITY_EDITOR
        // Otherwise the first screenshots show the Editor's cyan "still compiling" placeholder shader.
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
        yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
        yield return null;

        var director = Object.FindFirstObjectByType<GameDirector>();
        Assert.IsNotNull(director, "GameDirector in scene");
        Assert.IsNotNull(director.Squad, "squad wired");
        Assert.IsNotNull(director.Sequence, "sequence wired");
        Assert.IsNotNull(director.BossView, "boss view wired");
        Assert.AreEqual(4, director.Squad.Formations.Count);
        Assert.AreEqual(GamePhase.Menu, director.Phase);
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount, "squad stands on the field before the match starts");
        Assert.IsFalse(Camera.main.orthographic, "quarter view uses a perspective camera");
        Assert.IsNotNull(Camera.main.GetComponent<BoneSquadKiting.Cameras.EdgeScrollCamera>(), "edge-scroll camera in scene");
        var camStart = Camera.main.transform.position;
        Assert.IsNotNull(Object.FindFirstObjectByType<BoneSquadKiting.UI.TestHud>(), "test HUD in scene");
        var stage = Object.FindFirstObjectByType<BoneSquadKiting.Stage.StageSetup>();
        Assert.IsNotNull(stage, "stage setup in scene");
        Assert.IsNotNull(stage.Grid, "walkable grid baked at load");
        Assert.AreSame(stage.Grid, director.Squad.Grid, "squad moves on the stage grid");
        Assert.IsTrue(stage.Grid.IsWalkable(director.Squad.Anchor), "squad starts on walkable ground");
        Assert.IsFalse(stage.Grid.IsWalkable(new Vector2(GameConstants.BossX, GameConstants.BossZ)), "the boss's spot is cut out");
        Assert.IsFalse(stage.Grid.IsWalkable(new Vector2(30f, 0f)), "off the arena floor");
        var gizmos = Object.FindFirstObjectByType<RangeGizmos>();
        Assert.IsNotNull(gizmos, "range gizmos in scene");
        Assert.AreSame(director, gizmos.Director);
        var hud = Object.FindFirstObjectByType<BoneSquadKiting.UI.Hud>();
        Assert.IsNotNull(hud, "player HUD in scene");
        Assert.IsTrue(hud.Overlay.activeSelf, "start overlay is up before the match");
        Assert.AreEqual(4, hud.FormationButtons.Length);
        Assert.AreEqual("Q  밀집", hud.FormationLabels[0].text);
        Assert.IsNotNull(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(), "event system for HUD clicks");
        var fx = Object.FindFirstObjectByType<BoneSquadKiting.Fx.JuiceFx>();
        Assert.IsNotNull(fx, "effects in scene");
        Assert.IsNotNull(fx.Cam); Assert.IsNotNull(fx.FlashImage); Assert.IsNotNull(fx.HitParticles); Assert.IsNotNull(fx.Ring);

        hud.OverlayButton.onClick.Invoke();   // the start button starts the match
        Assert.AreEqual(GamePhase.Play, director.Phase);
        yield return null;
        Assert.IsFalse(hud.Overlay.activeSelf, "overlay closes once playing");
        StringAssert.Contains("BOSS  100%", hud.BossText.text);
        StringAssert.Contains("25 / 25", hud.AliveText.text);
        Capture("arena-0-menu.png");

        // The boss collider must be pickable on the Enemy layer and resolve to the live boss.
        var bossRef = director.BossView.GetComponent<EnemyRef>();
        Assert.AreSame(director.Boss, bossRef.Enemy);
        Assert.AreEqual(LayerMask.NameToLayer("Enemy"), director.BossView.gameObject.layer);
        Assert.IsNotNull(director.BossView.GetComponent<Collider>());

        // The scene's ranges are short, so the squad has to be ordered onto the boss before it can fire.
        Assert.That(director.UnitFireRange, Is.LessThan(10f), "scene uses the hand-tuned short ranges");
        director.IssueOrder(SquadOrder.Attack(director.Boss));
        Assert.IsTrue(director.Squad.IsMoving, "boss is outside squad range at the start");

        // Let it close in and fire, then wait for the first telegraph.
        float timeout = Time.time + GameConstants.FirstAttackDelay + 3f;
        while (director.Attacks.Current == null && Time.time < timeout) yield return null;
        Assert.IsNotNull(director.Attacks.Current, "first pattern telegraphed");
        Assert.That(director.Boss.Hp, Is.LessThan(GameConstants.BossMax), "the squad closed in and hit the boss");
        yield return null; // the HUD may have refreshed before the director started the pattern this frame
        Assert.IsTrue(hud.WarningPanel.activeSelf, "pattern warning shows during a telegraph");
        StringAssert.Contains("중앙 핵폭발", hud.WarningTitle.text);
        yield return new WaitForSeconds(0.6f);
        Capture("arena-1-telegraph-core.png");

        // Dodge the first pattern (core -> ring), then show the second (gate) in line formation.
        director.ChangeFormation("ring");
        float resolveBy = Time.time + 4f;
        while (director.Attacks.Current != null && Time.time < resolveBy) yield return null;
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount, "ring formation dodges the core blast");
        yield return null;
        Assert.AreEqual("PERFECT DODGE!", hud.Toast.text);
        Assert.AreEqual(0f, fx.Shake, "no shake on a clean dodge");
        Capture("arena-2-ring.png");

        director.ChangeFormation("line");
        director.IssueOrder(SquadOrder.Move(new Vector2(-4f, 1f)));
        float nextBy = Time.time + 4f;
        while (director.Attacks.Current == null && Time.time < nextBy) yield return null;
        yield return new WaitForSeconds(1.0f);
        Capture("arena-3-gate-line.png");
        Assert.AreEqual(camStart, Camera.main.transform.position, "the camera does not follow the squad");
        Assert.AreEqual(GamePhase.Play, director.Phase);
    }

    static void Capture(string fileName)
    {
        var cam = Camera.main;
        Assert.IsNotNull(cam, "main camera");
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        cam.targetTexture = prev;
        RenderTexture.active = null;
        string dir = Path.Combine(Application.dataPath, "../Logs/Screenshots");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }
}
