using System.Collections;
using System.Collections.Generic;
using BoneSquadKiting.Cameras;
using BoneSquadKiting.Fx;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PresentationManagerTests
{
    const int VolumeLayer = 28;
    const int VolumeMask = 1 << VolumeLayer;

    readonly List<ScriptableObject> _assets = new();
    Scene _scene, _previousScene;
    PresentationManager _manager;
    EdgeScrollCamera _camera;
    VolumeStack _stack;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _previousScene = SceneManager.GetActiveScene();
        _scene = SceneManager.CreateScene("Presentation " + System.Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(_scene);
        var view = new GameObject("Test camera");
        view.AddComponent<Camera>().GetUniversalAdditionalCameraData().volumeLayerMask = VolumeMask;
        _camera = view.AddComponent<EdgeScrollCamera>();

        var baseline = new GameObject("Scene atmosphere") { layer = VolumeLayer }.AddComponent<Volume>();
        baseline.isGlobal = true;
        baseline.sharedProfile = Profile(0.1f);

        var db = ScriptableObject.CreateInstance<PresentationDatabase>();
        _assets.Add(db);
        db.Moods = new[]
        {
            new PresentationMood { Id = "day", Profile = Profile(0.2f), TransitionSeconds = 0f },
            new PresentationMood { Id = "night", Profile = Profile(0.3f), TransitionSeconds = 0.5f }
        };
        db.Effects = new[]
        {
            new PresentationEffect { Id = "impact", Profile = Profile(0.6f), HoldSeconds = 4f,
                ReturnSeconds = 0.25f, ShakeStrength = 2f, ShakeSeconds = 1f },
            new PresentationEffect { Id = "hit", Profile = Profile(0.8f), HoldSeconds = 1f, ReturnSeconds = 0.25f },
            new PresentationEffect { Id = "shake", ShakeStrength = 0.2f, ShakeSeconds = 2f }
        };
        var owner = new GameObject("Scene presentation");
        owner.SetActive(false);
        _manager = owner.AddComponent<PresentationManager>();
        _manager.Database = db;
        _manager.Cam = _camera;
        owner.SetActive(true);
        yield return null;
        _stack = VolumeManager.instance.CreateStack();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(_previousScene);
        if (_scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_stack != null) VolumeManager.instance.DestroyStack(_stack);
        foreach (var asset in _assets) Object.Destroy(asset);
        _assets.Clear();
        yield return null;
    }

    VolumeProfile Profile(float intensity)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(intensity);
        _assets.Add(profile);
        _assets.Add(vignette);
        return profile;
    }

    float VignetteIntensity()
    {
        VolumeManager.instance.Update(_stack, null, VolumeMask);
        return _stack.GetComponent<Vignette>().intensity.value;
    }

    [Test]
    public void ChangingMoodDuringImpact_RevealsLatestMoodThenSceneBaseline()
    {
        Assert.AreSame(_manager, PresentationManager.Current);
        Assert.That(VignetteIntensity(), Is.EqualTo(0.1f).Within(0.001f));
        Assert.IsTrue(_manager.SetMood("day"));
        Assert.That(VignetteIntensity(), Is.EqualTo(0.2f).Within(0.001f));
        Assert.IsTrue(_manager.Play("impact"));
        Assert.That(VignetteIntensity(), Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(_camera.TickShake(0f), Is.EqualTo(2f));
        Assert.IsTrue(_manager.Play("hit"));
        Assert.IsTrue(_manager.SetMood("night"));
        Assert.IsTrue(_manager.Play("shake"));
        Assert.IsFalse(_manager.Play("missing"));
        Assert.IsFalse(_manager.SetMood("missing"));
        _manager.Tick(0.5f);
        Assert.That(VignetteIntensity(), Is.EqualTo(0.8f).Within(0.001f));
        _manager.Tick(0.625f);
        Assert.That(VignetteIntensity(), Is.InRange(0.31f, 0.79f));
        _manager.Tick(0.125f);
        Assert.That(VignetteIntensity(), Is.EqualTo(0.3f).Within(0.001f));
        _manager.ClearMood();
        _manager.Tick(0.25f);
        Assert.That(VignetteIntensity(), Is.InRange(0.11f, 0.29f));
        _manager.Tick(0.25f);
        Assert.That(VignetteIntensity(), Is.EqualTo(0.1f).Within(0.001f));
    }

    [Test]
    public void WeakerLongerShake_CannotCancelImpactAndSettlesWithoutDisplacement()
    {
        _manager.Play("impact");
        _manager.Play("shake");
        Assert.That(_camera.TickShake(0f), Is.EqualTo(2f));
        Assert.That(_camera.TickShake(0.5f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(_camera.TickShake(0.5f), Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(_camera.TickShake(1f), Is.Zero);
        Assert.AreEqual(Vector3.zero, _camera.ShakeOffset);
    }

    [UnityTest]
    public IEnumerator SceneUnload_RemovesManagerAndItsVolumeOverrides()
    {
        _manager.SetMood("day");
        _manager.Play("impact");
        Assert.That(VignetteIntensity(), Is.EqualTo(0.6f).Within(0.001f));
        SceneManager.SetActiveScene(_previousScene);
        Assert.That(VignetteIntensity(), Is.EqualTo(0.1f).Within(0.001f));
        yield return SceneManager.UnloadSceneAsync(_scene);
        Assert.IsTrue(_manager == null);
        Assert.AreNotSame(_manager, PresentationManager.Current);
        VolumeManager.instance.Update(_stack, null, 0);
        float projectDefault = _stack.GetComponent<Vignette>().intensity.value;
        Assert.That(VignetteIntensity(), Is.EqualTo(projectDefault).Within(0.001f));
    }
}
