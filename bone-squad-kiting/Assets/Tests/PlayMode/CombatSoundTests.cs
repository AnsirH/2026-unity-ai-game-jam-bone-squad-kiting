using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoneSquadKiting.Audio;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Mobs;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;
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

    internal static bool Playing(string id) => PlayingVoices(id).Any();

    static IEnumerable<AudioSource> PlayingVoices(string id)
    {
        var db = AssetDatabase.LoadAssetAtPath<SoundDatabaseSO>("Assets/Data/Audio/SoundDatabase.asset");
        var clip = db.FindSfx(id).Clip;
        return SoundManager.Current.GetComponent<SfxPool>().GetComponents<AudioSource>()
            .Where(source => source.isPlaying && source.clip == clip);
    }

    [UnityTest]
    public IEnumerator MobGroupsPlayOneSpawnSoundWhenEnemiesActuallyAppear()
    {
        const string cue = "stage.wave.start";
        var grunt = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Grunt.asset");
        director.Mobs.MaxAlive = 0;
        Assert.IsNull(director.Mobs.Spawn(grunt, squad.Anchor, director.Enemies));
        Assert.IsFalse(Playing(cue));

        director.Mobs.MaxAlive = 6;
        for (int i = 0; i < 6; i++) director.Mobs.Spawn(grunt, squad.Anchor, director.Enemies);
        PlayingVoices(cue).Single().Stop();

        yield return new WaitForSecondsRealtime(.08f);
        var waves = squad.gameObject.AddComponent<WaveSpawner>();
        waves.Mob = grunt;
        waves.Count = AnimationCurve.Constant(0f, 1f, 6f);
        director.Waves = waves;
        director.Restart();
        waves.SpawnNow();
        director.Step(.01f);
        Assert.AreEqual(6, waves.MobsSpawned);
        Assert.AreEqual(1, PlayingVoices(cue).Count());
    }

    [UnityTest]
    public IEnumerator VolleyVolumeFollowsReleasedBoltsAndResetsForTheNextShot()
    {
        squad.HasLeader = true;
        director.Restart();
        var db = AssetDatabase.LoadAssetAtPath<SoundDatabaseSO>("Assets/Data/Audio/SoundDatabase.asset");
        var clip = db.FindSfx("squad.crossbow.fire").Clip;
        var pool = SoundManager.Current.GetComponent<SfxPool>();
        var target = new Mob { Def = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Grunt.asset"), Health = 1000f };
        var soldiers = squad.Units.Where(unit => !unit.IsLeader).ToArray();
        void Fire(SquadUnit unit)
        {
            unit.PendingTarget = target;
            director.Shots.ReleasePending(unit);
        }
        AudioSource Crossbow() => pool.GetComponents<AudioSource>().Single(s => s.isPlaying && s.clip == clip);

        Fire(soldiers[0]);
        var voice = Crossbow();
        float solo = voice.volume;
        Assert.AreEqual(db.FindSfx("squad.crossbow.fire").Volume * .30f, solo, .001f);
        Assert.That(voice.pitch, Is.InRange(.9f, 1.1f));
        for (int i = 1; i < 5; i++) Fire(soldiers[i]);
        float five = Crossbow().volume;
        for (int i = 5; i < soldiers.Length; i++) Fire(soldiers[i]);
        Fire(squad.Units.Single(u => u.IsLeader));
        Assert.AreSame(voice, Crossbow(), "one crossbow voice for the whole volley");
        Assert.That(five, Is.GreaterThan(solo));
        Assert.That(voice.volume, Is.GreaterThan(five).And.LessThanOrEqualTo(1f));
        Assert.IsTrue(Playing("squad.leader.throw"));
        yield return new WaitForSecondsRealtime(.16f);
        Fire(soldiers[0]);
        Assert.AreSame(voice, Crossbow());
        Assert.AreEqual(solo, voice.volume, .001f);
        Assert.That(voice.pitch, Is.InRange(.9f, 1.1f));
        Fire(soldiers[1]);
        director.Restart();
        Assert.IsFalse(Playing("squad.crossbow.fire"));
        Fire(squad.Units.First(u => !u.IsLeader));
        Assert.AreEqual(solo, Crossbow().volume, .001f, "a new stage starts a fresh volley");
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
        PlayingVoices(view.VanishSound).Single().Stop();
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
        PlayingVoices(view.VanishSound).Single().Stop();
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
        fight.Tick(arena.Tuning.LethalHoldSeconds);
        Assert.AreEqual(FrostKnightState.Vanishing, fight.State);
        PlayingVoices(view.VanishSound).Single().Stop();
        fight.Tick(arena.Tuning.VanishSeconds);
        Assert.IsFalse(Playing(view.VanishSound), "the end of an audible fade is silent");

        Assert.IsFalse(Playing(view.GroggySound));
        fight.Tick(arena.Tuning.AttackGap);
        Assert.AreEqual(FrostKnightState.Groggy, fight.State);
        PlayingVoices(view.GroggySound).Single().Stop();
        fight.Tick(arena.Tuning.GroggySeconds * .5f);
        Assert.IsFalse(Playing(view.GroggySound), "one cue on entering the state");
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

    [Test]
    public void WaveMeleeAndPurpleOrbUseTheirOwnClipsOnSquadDamage()
    {
        var grunt = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Grunt.asset");
        var caster = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Caster.asset");
        var thrower = AssetDatabase.LoadAssetAtPath<MobDefinition>("Assets/Data/Mobs/Thrower.asset");
        var db = AssetDatabase.LoadAssetAtPath<SoundDatabaseSO>("Assets/Data/Audio/SoundDatabase.asset");
        Assert.AreEqual("쫄몹근접공격 피격소리", db.FindSfx(grunt.SquadHitSound).Clip.name.Normalize());
        Assert.AreEqual("원거리쫄몹 에너지볼 피격소리", db.FindSfx(caster.SquadHitSound).Clip.name.Normalize());
        Assert.IsTrue(string.IsNullOrEmpty(thrower.SquadHitSound));

        var soldier = squad.Units.OrderByDescending(u => u.X).First();
        var pos = new Vector2(soldier.X, soldier.Z);
        var mob = director.Mobs.Spawn(grunt, pos + Vector2.right * .3f, director.Enemies);
        mob.AttackCd = 0f;
        director.Mobs.Tick(squad, director.Enemies, null, null, .01f);
        Assert.Less(soldier.Hp, BoneSquadKiting.Core.GameConstants.UnitMax);
        Assert.IsTrue(Playing(grunt.SquadHitSound));
        Assert.IsFalse(Playing(caster.SquadHitSound));
        director.Mobs.Clear(director.Enemies);
        director.Mobs.Shots.Add(new MobShot { Def = caster, X = pos.x + 30f, Z = pos.y, Dir = Vector2.right, Left = .001f });
        director.Mobs.Tick(squad, director.Enemies, null, null, .01f);
        Assert.IsFalse(Playing(caster.SquadHitSound));
        var hp = soldier.Hp;
        void Hit() {
            director.Mobs.Shots.Add(new MobShot { Def = caster, X = soldier.X, Z = soldier.Z, Left = 1f });
            director.Mobs.Tick(squad, director.Enemies, null, null, .001f);
        }
        bool oldInvincible = SquadDamage.Invincible;
        try
        {
            SquadDamage.Invincible = true;
            Hit();
            Assert.IsFalse(Playing(caster.SquadHitSound));
            Assert.AreEqual(hp, soldier.Hp);
            SquadDamage.Invincible = false;
            Hit();
            Assert.IsTrue(Playing(caster.SquadHitSound));
            Assert.Less(soldier.Hp, hp);
        }
        finally { SquadDamage.Invincible = oldInvincible; }
    }

    [Test]
    public void FrostKnightPlaysTheCatapultImpactOnlyOnDamagingBolts()
    {
        var room = new GameObject("Boss arena");
        room.transform.SetParent(squad.transform);
        room.AddComponent<BossArena>();
        director.Restart();
        var knight = director.Fight.Boss;
        var soldier = squad.Units.First(u => !u.IsLeader);
        knight.Position = new Vector2(soldier.X + 4f, soldier.Z);
        const string armor = "squad.arrow.hit.armor";
        void Fire()
        {
            soldier.PendingTarget = knight;
            soldier.PendingReserve = 12f;
            director.Shots.ReleasePending(soldier);
            Assert.IsFalse(Playing(armor));
            director.Shots.Tick(squad.Units, director.Enemies, squad.Order, .5f);
        }
        float before = knight.Hp;
        knight.HpFloor = before;
        Fire();
        Assert.AreEqual(before, knight.Hp);
        Assert.IsFalse(Playing(armor), "a bolt at the phase health floor");
        knight.HpFloor = 0;
        Fire();
        Assert.Less(knight.Hp, before);
        Assert.IsTrue(Playing(armor));
        Assert.IsFalse(Playing("squad.arrow.hit.humanoid"));
    }
}
