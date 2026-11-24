using System.Collections;
using System.Linq;
using BoneSquadKiting.Audio;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatSoundTests
{
    FormationController squad;
    GameDirector director;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        squad = SquadTestRig.MakeFc();
        if (Object.FindFirstObjectByType<AudioListener>() == null) squad.gameObject.AddComponent<AudioListener>();
        director = squad.gameObject.AddComponent<GameDirector>();
        director.Squad = squad;
        director.Restart();
        director.enabled = false;
        foreach (var voice in SoundManager.Current.GetComponent<SfxPool>().GetComponents<AudioSource>())
            voice.Stop();
        yield return new WaitForSecondsRealtime(0.07f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var voice in SoundManager.Current.GetComponentsInChildren<AudioSource>()) voice.Stop();
        Object.DestroyImmediate(squad.gameObject);
    }

    internal static bool Playing(string id)
    {
        var db = AssetDatabase.LoadAssetAtPath<SoundDatabaseSO>("Assets/Data/Audio/SoundDatabase.asset");
        var clip = db.FindSfx(id).Clip;
        return SoundManager.Current.GetComponent<SfxPool>().GetComponents<AudioSource>()
            .Any(source => source.isPlaying && source.clip == clip);
    }

    [Test]
    public void RegisteredEffectsStartAtTheirMeasuredAudioOffsets()
    {
        var db = AssetDatabase.LoadAssetAtPath<SoundDatabaseSO>("Assets/Data/Audio/SoundDatabase.asset");
        var pool = SoundManager.Current.GetComponent<SfxPool>();
        foreach (var entry in db.Sfx.Where(e => e.Clip != null))
        {
            Assert.IsTrue(SoundManager.Current.PlaySfx(entry.Id));
            var voice = pool.GetComponents<AudioSource>().Single(s => s.isPlaying && s.clip == entry.Clip);
            int firstSample = Mathf.RoundToInt(entry.StartOffset * entry.Clip.frequency);
            Assert.GreaterOrEqual(voice.timeSamples, firstSample - 1, entry.Id);
        }
    }

    [UnityTest]
    public IEnumerator FrostKnightSoundsFollowTheBladeAndStopWhenItLands()
    {
        var room = new GameObject("Boss room");
        room.transform.SetParent(squad.transform);
        var arena = room.AddComponent<BossArena>();
        arena.Tuning.AttackOrder = "CL";
        var model = new GameObject("Frost Knight");
        model.transform.SetParent(room.transform);
        var view = model.AddComponent<FrostKnightView>();
        view.Director = director;
        director.Restart();
        var fight = director.Fight;
        squad.Anchor = arena.Center;
        yield return null;
        yield return null;
        fight.Tick(0f);
        Assert.IsTrue(Playing(view.WakeSound));

        var random = Random.state;
        try
        {
            Random.InitState(1); // select the circular throne attack
            fight.Tick(arena.Tuning.FirstAttackDelay + .01f);
        }
        finally { Random.state = random; }
        Assert.AreEqual(FrostKnightAttack.Circle, fight.Attack);
        Assert.IsFalse(Playing(view.CastSound));
        fight.Tick(fight.Telegraphs[0].Remaining - .34f);
        yield return null;
        Assert.IsTrue(Playing(view.CastSound));
        Assert.IsFalse(Playing(view.CircleFallSound));
        fight.Tick(.24f);
        yield return null;
        Assert.IsTrue(Playing(view.CircleFallSound));
        Assert.IsFalse(Playing(view.HitSound));
        fight.Tick(.11f);
        Assert.IsFalse(Playing(view.CircleFallSound));
        Assert.IsTrue(Playing(view.HitSound));

        fight.Boss.ApplyDamage(fight.Boss.MaxHp);
        fight.Tick(0f);
        Assert.IsTrue(Playing(view.StunSound));
        fight.Tick(arena.Tuning.MageDelay);
        fight.Tick(0f); // the test arena has no mages
        Assert.IsTrue(Playing(view.RiseSound));
        fight.Tick(arena.Tuning.StandUpSeconds);
        fight.Tick(arena.Tuning.AttackGap);
        Assert.AreEqual(FrostKnightAttack.Charge, fight.Attack);
        yield return null; // the view enters ChargedSlash
        Assert.IsTrue(Playing(view.PhaseTwoWindupSound));
        Assert.IsFalse(Playing(view.ChargeReleaseSound));
        fight.Tick(fight.Telegraphs[0].Remaining - .1f);
        yield return null;
        Assert.IsFalse(Playing(view.ChargeReleaseSound));
        fight.Tick(.11f);
        Assert.IsTrue(Playing(view.ChargeReleaseSound));
        Assert.IsFalse(Playing(view.PhaseTwoWindupSound));
        Assert.IsFalse(Playing(view.ParallelFlightSound));

        fight.Tick(1f); // finish the slash and hide
        fight.Tick(arena.Tuning.AttackGap);
        Assert.AreEqual(FrostKnightState.Slamming, fight.State);
        Assert.IsFalse(Playing(view.PhaseTwoWindupSound), "only the floor warning is visible");
        fight.Tick(fight.Telegraphs[0].Remaining - .5f);
        yield return null; // the boss reappears and raises the sword
        Assert.AreEqual("SwordPlant", fight.Anim);
        Assert.IsTrue(Playing(view.PhaseTwoWindupSound));
        Assert.IsFalse(Playing(view.LethalHitSound));
        fight.Tick(.51f);
        Assert.IsTrue(Playing(view.LethalHitSound));
        Assert.IsFalse(Playing(view.PhaseTwoWindupSound));
        fight.Kill();
        Assert.IsTrue(Playing(view.DeathSound));
    }

    [Test]
    public void SquadOrdersDeathsAndActualBoltContactsPlayTheirOwnSounds()
    {
        Assert.IsFalse(Playing("squad.formation.command"));
        director.ChangeFormation("line");
        Assert.IsTrue(Playing("squad.formation.command"));
        var soldier = squad.Units[1];
        var target = new Mob { Def = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Grunt.asset"), Health = 100 };
        soldier.PendingTarget = target;
        director.Shots.ReleasePending(soldier);
        Assert.IsTrue(Playing("squad.crossbow.fire"));
        Assert.IsFalse(Playing("squad.arrow.hit.humanoid"));
        director.Shots.Tick(squad.Units, director.Enemies, squad.Order, 0.5f);
        Assert.IsTrue(Playing("squad.arrow.hit.humanoid"));
        Assert.IsFalse(Playing("squad.arrow.hit.armor"));
        var cannon = new Elite { Def = Elite("Catapult"), Health = 100, X = soldier.X, Z = soldier.Z };
        soldier.PendingTarget = cannon;
        director.Shots.ReleasePending(soldier);
        director.Shots.Tick(squad.Units, director.Enemies, squad.Order, 0.05f);
        Assert.IsTrue(Playing("squad.arrow.hit.armor"));
        SquadDamage.Apply(squad.Units, soldier, soldier.Hp + 1);
        Assert.IsTrue(Playing("squad.hit.bones"));
        Assert.IsTrue(director.TryRevive());
        Assert.IsTrue(Playing("squad.revive.magic"));
        Assert.IsFalse(Playing("squad.revive"));
        director.Revives.Tick(squad.Anchor, director.Revives.Duration * 0.8f);
        Assert.IsFalse(Playing("squad.revive"));
        director.Revives.Tick(squad.Anchor, director.Revives.Duration * 0.05f);
        Assert.IsTrue(Playing("squad.revive"));
        Assert.IsTrue(soldier.Dead, "bones start converging before the soldier reappears");
    }

    static EliteDefinition Elite(string name) =>
        AssetDatabase.LoadAssetAtPath<EliteDefinition>("Assets/Data/Elites/" + name + ".asset");

    [UnityTest]
    public IEnumerator SoldierBoltsAndThrownSpearsLeaveWithTheirAnimationEvents()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Data/Art/ArtSet.asset");
        var squadView = new GameObject("Soldiers").AddComponent<SquadView>();
        squadView.transform.SetParent(squad.transform);
        squadView.Squad = squad; squadView.Art = art;
        var mobView = new GameObject("Throwers").AddComponent<MobView>();
        mobView.transform.SetParent(squad.transform);
        mobView.Director = director; mobView.Art = art;
        var def = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Thrower.asset");
        var thrower = director.Mobs.Spawn(def, squad.Anchor + Vector2.right * 3f, director.Enemies);
        yield return null; yield return null;
        var soldier = squad.Units[1];
        foreach (var unit in squad.Units) unit.Cd = 100f;
        soldier.Cd = thrower.AttackCd = 0f;
        director.Shots.Tick(squad.Units, director.Enemies, squad.Order, 0.001f);
        director.Mobs.Tick(squad, director.Enemies, null, null, 0.001f);
        Assert.IsNotNull(soldier.PendingTarget);
        Assert.IsNotNull(thrower.PendingJavelin);
        Assert.IsEmpty(director.Shots.Shots);
        Assert.IsEmpty(director.Mobs.Shots);
        // The simulation clock stays still; only actual animation events can release either shot.
        float until = Time.realtimeSinceStartup + 2f;
        while ((soldier.PendingTarget != null || thrower.PendingJavelin != null) && Time.realtimeSinceStartup < until)
            yield return null;
        Assert.AreEqual(1, director.Shots.Shots.Count);
        Assert.AreEqual(1, director.Mobs.Shots.Count);
        Assert.IsTrue(Playing("squad.crossbow.fire"));
        Assert.IsTrue(Playing("enemy.javelin.shout"));
        Assert.IsTrue(Playing("enemy.javelin.flight"));
    }

    [TestCase("Character/EnemySpearThrower/Prefabs/EnemySpearThrower", "Thrrow", 8f / 30f, "enemy.javelin.shout")]
    public void AnimatedWeaponsDispatchFromTheirReleaseOrContactFrame(string path, string state, float time, string id)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/" + path + ".prefab");
        var animator = Object.Instantiate(prefab, squad.transform).GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Play(state, 0, 0f);
        animator.Update(0f);
        animator.Update(time - 0.015f);
        Assert.IsFalse(Playing(id));
        animator.Update(0.03f);
        Assert.IsTrue(Playing(id));
    }

    [Test]
    public void FireAndForkedSpearFollowTheirCasterClocksEvenWhenTheAttackMisses()
    {
        foreach (var name in new[] { "DarkFlameMage", "SpearThrower" })
        {
            var attacks = director.Elites.Spawn(Elite(name), Vector2.zero, null).Attacks;
            attacks.NextAttack = 0f;
            attacks.Tick(squad, null, 0f);
            bool mage = name == "DarkFlameMage";
            Assert.IsTrue(Playing(mage ? "enemy.firemage.scorch" : "enemy.javelin.shout"));
            string impact = mage ? "enemy.firemage.eruption" : "enemy.javelin.impact";
            if (!mage)
            {
                Assert.IsTrue(Playing("enemy.javelin.flight"));
                Assert.IsFalse(Playing("enemy.javelin.split"));
            }
            squad.Anchor += Vector2.one * 50f;
            foreach (var unit in squad.Units) { unit.X += 50; unit.Z += 50; }
            attacks.Tick(squad, null, attacks.Current.Value.Remaining - 0.2f);
            if (!mage) Assert.IsTrue(Playing("enemy.javelin.split"));
            Assert.IsFalse(Playing(impact));
            attacks.Tick(squad, null, 0.21f);
            Assert.IsTrue(Playing(impact));
        }
    }
}
