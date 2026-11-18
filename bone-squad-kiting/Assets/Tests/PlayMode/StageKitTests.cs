using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

/// <summary>A hand-made map becomes playable from markers alone: StageStart brings the game in at play time.</summary>
public class StageKitTests
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

    static IEnumerator Load(string path)
    {
        UnityEditor.ShaderUtil.allowAsyncCompilation = false;
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
    }

    [UnityTest]
    public IEnumerator GuideScene_GetsSquadCameraAndHud_AndTheOutlinesBoundTheSquad()
    {
        yield return Load("Assets/Scenes/StageSetupGuide.unity");

        var start = Object.FindFirstObjectByType<StageStart>();
        Assert.IsNotNull(start); Assert.IsNotNull(start.Kit, "the marker brought the kit in");
        var director = Object.FindFirstObjectByType<GameDirector>();
        Assert.IsNotNull(director);
        Assert.AreEqual(start.gameObject.scene, director.gameObject.scene);
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount);
        Assert.That(Vector2.Distance(director.Squad.Anchor, new Vector2(start.transform.position.x, start.transform.position.z)), Is.LessThan(1f), "squad starts on the marker");

        Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, "the map's preview camera is off while playing");
        Assert.AreSame(start.Kit.Cam.GetComponent<Camera>(), Camera.main);
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
        Assert.IsNotNull(Object.FindFirstObjectByType<BoneSquadKiting.UI.Hud>());
        var look = start.Kit.Cam.LookAt;
        Assert.That(Vector2.Distance(look, director.Squad.Anchor), Is.LessThan(0.5f), "camera starts on the squad");

        var grid = start.Kit.Setup.Grid;
        Assert.IsTrue(grid.IsWalkable(new Vector2(0f, 10f)), "on the road, inside the outline");
        Assert.IsTrue(grid.IsWalkable(new Vector2(-20f, -8f)), "the far arm of the outline");
        Assert.IsFalse(grid.IsWalkable(new Vector2(12f, 10f)), "ground, but outside the outline");
        Assert.IsFalse(grid.IsWalkable(new Vector2(-8f, -8f)), "the excluded rock");

        yield return null;
        Capture("stagekit-guide.png");
        director.Restart();
        director.IssueOrder(BoneSquadKiting.Combat.SquadOrder.Move(new Vector2(12f, 10f)));   // an order outside the area
        for (int i = 0; i < 400; i++) director.Step(0.02f);
        foreach (var u in director.Squad.Units) Assert.IsTrue(grid.IsWalkable(new Vector2(u.X, u.Z)), "no unit left the outline");

        Assert.IsTrue(start.Kit.Setup.EndPoint != null, "end point passed on");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ArtistScene_PlaysFromItsOwnMarkers_UnitsStandOnTheTerrain()
    {
        const string path = "Assets/Scenes/BackgroundTest.unity";
        if (!File.Exists(path)) Assert.Ignore("no artist scene in the project");
        yield return Load(path);
        yield return null;

        // The maps carry their own Stage Markers now. With several maps on at once, one marker brings the game in.
        StageStart start = null;
        foreach (var s in Object.FindObjectsByType<StageStart>(FindObjectsSortMode.None)) if (s.Kit != null) start = s;
        if (start == null) Assert.Ignore("the artist scene has no active Stage Start");
        Assert.AreEqual(1, Object.FindObjectsByType<GameDirector>(FindObjectsSortMode.None).Length, "one game, however many markers");
        var director = start.Kit.Director;
        Assert.That(Vector2.Distance(director.Squad.Anchor, new Vector2(start.transform.position.x, start.transform.position.z)), Is.LessThan(1.5f));
        Assert.IsTrue(start.Kit.Setup.Grid.IsWalkable(director.Squad.Anchor));
        yield return null;
        var unit = Object.FindFirstObjectByType<BoneSquadKiting.Squad.SquadView>().transform.GetChild(3);
        float groundY = GroundHeight.Y(unit.position.x, unit.position.z);
        Assert.That(unit.position.y, Is.EqualTo(groundY).Within(0.02f), "the model stands on the terrain");
        Capture("stagekit-artist-map.png");
    }

    [UnityTest]
    public IEnumerator MoveTuningScene_HasWallsADummyAndNoEnemiesThatFightBack()
    {
        yield return Load("Assets/Scenes/MoveTuning.unity");
        yield return null;
        var start = Object.FindFirstObjectByType<StageStart>();
        Assert.IsNotNull(start.Kit);
        var director = start.Kit.Director; var grid = start.Kit.Setup.Grid;
        Assert.IsFalse(grid.IsWalkable(new Vector2(-6f, 8f)), "inside the long wall");
        Assert.IsTrue(grid.IsWalkable(new Vector2(19.5f, -10f)), "the narrow passage is open");
        Assert.IsFalse(grid.IsWalkable(new Vector2(-34f, -24f)), "beyond the slanted edge");
        Assert.IsNull(director.Waves); Assert.IsFalse(director.HasBoss);

        var dummy = Object.FindFirstObjectByType<BoneSquadKiting.Combat.TrainingDummy>();
        director.Restart();
        yield return null; yield return null;
        CollectionAssert.Contains(director.Enemies.Enemies, dummy, "the dummy is a target");
        director.IssueOrder(BoneSquadKiting.Combat.SquadOrder.Attack(dummy));
        for (int i = 0; i < 900 && dummy.DamageTaken <= 0f; i++) director.Step(0.02f);
        Assert.That(dummy.DamageTaken, Is.GreaterThan(0f), "the squad walks into range and shoots it");
        for (int i = 0; i < 300; i++) director.Step(0.02f);
        Assert.IsTrue(dummy.Alive, "it never dies");
        Assert.AreEqual(GameConstants.SquadSize, director.AliveCount, "nothing hurts the squad here");
        Assert.AreEqual(GamePhase.Play, director.Phase);
        Capture("stagekit-movetuning.png");
        foreach (var path in new[] { "Assets/Scenes/MoveTuning.unity", "Assets/Scenes/StageSetupGuide.unity" })
            foreach (var b in UnityEditor.EditorBuildSettings.scenes) Assert.AreNotEqual(path, b.path, "test scenes stay out of the build");
    }

    [UnityTest]
    public IEnumerator StageMarkersPrefab_DroppedOnABareMap_MakesItPlayable()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Stage/Stage Markers.prefab");
        Assert.IsNotNull(prefab, "the one-piece marker set exists");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        var set = Object.Instantiate(prefab, new Vector3(5f, 0f, 3f), Quaternion.identity);
        yield return null; yield return null;

        var start = set.GetComponentInChildren<StageStart>();
        Assert.IsNotNull(start.Kit, "the game came in");
        Assert.IsNotNull(start.Kit.Setup.EndPoint, "the set's end point is wired");
        var grid = start.Kit.Setup.Grid;
        Assert.That(Vector2.Distance(start.Kit.Director.Squad.Anchor, new Vector2(5f, 11f)), Is.LessThan(1f), "squad on the set's start point");
        Assert.IsTrue(grid.IsWalkable(new Vector2(5f, -6f)), "inside the set's area");
        Assert.IsFalse(grid.IsWalkable(new Vector2(5f, 3f)), "the set's blocked square");
        Assert.IsFalse(grid.IsWalkable(new Vector2(20f, 3f)), "outside the set's area");
    }

    [Test]
    public void GroundHeight_IsZeroWithoutAMask_AndTheSurfaceWithOne()
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.position = new Vector3(100f, 2f, 100f); box.transform.localScale = new Vector3(4f, 2f, 4f);
        Physics.SyncTransforms();
        GroundHeight.Mask = 0;
        Assert.AreEqual(0f, GroundHeight.Y(100f, 100f));
        GroundHeight.Mask = 1;
        Assert.That(GroundHeight.Y(100f, 100f), Is.EqualTo(3f).Within(1e-3f));
        Assert.AreEqual(0f, GroundHeight.Y(500f, 500f), "nothing below: flat");
        Object.DestroyImmediate(box);
    }

    static void Capture(string fileName)
    {
        var cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        cam.targetTexture = null; RenderTexture.active = null;
        string dir = Path.Combine(Application.dataPath, "../Logs/Screenshots");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }
}
