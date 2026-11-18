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
        var follow = Camera.main.GetComponent<BoneSquadKiting.Cameras.QuarterViewCamera>();
        Assert.IsNotNull(follow, "camera follows the squad");
        Assert.AreSame(director.Squad, follow.Squad);
        Assert.IsNotNull(Object.FindFirstObjectByType<BoneSquadKiting.UI.TestHud>(), "test HUD in scene");
        Capture("arena-0-menu.png");

        director.Restart();
        Assert.AreEqual(GamePhase.Play, director.Phase);

        // The boss collider must be pickable on the Enemy layer and resolve to the live boss.
        var bossRef = director.BossView.GetComponent<EnemyRef>();
        Assert.AreSame(director.Boss, bossRef.Enemy);
        Assert.AreEqual(LayerMask.NameToLayer("Enemy"), director.BossView.gameObject.layer);
        Assert.IsNotNull(director.BossView.GetComponent<Collider>());

        // Let auto-fire run, then wait for the first telegraph.
        float timeout = Time.time + GameConstants.FirstAttackDelay + 3f;
        while (director.Attacks.Current == null && Time.time < timeout) yield return null;
        Assert.IsNotNull(director.Attacks.Current, "first pattern telegraphed");
        Assert.That(director.Boss.Hp, Is.LessThan(GameConstants.BossMax), "squad auto-fire hit the boss");
        yield return new WaitForSeconds(0.6f);
        Capture("arena-1-telegraph-core.png");

        // Dodge the first pattern (core -> ring), then show the second (gate) in line formation.
        director.ChangeFormation("ring");
        float resolveBy = Time.time + 4f;
        while (director.Attacks.Current != null && Time.time < resolveBy) yield return null;
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount, "ring formation dodges the core blast");
        Capture("arena-2-ring.png");

        director.ChangeFormation("line");
        director.IssueOrder(SquadOrder.Move(new Vector2(-4f, 1f)));
        float nextBy = Time.time + 4f;
        while (director.Attacks.Current == null && Time.time < nextBy) yield return null;
        yield return new WaitForSeconds(1.0f);
        Capture("arena-3-gate-line.png");
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
