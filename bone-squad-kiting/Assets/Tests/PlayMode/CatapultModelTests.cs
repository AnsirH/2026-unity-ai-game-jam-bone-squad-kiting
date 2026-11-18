using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;
using BoneSquadKiting.Stage;

/// <summary>The catapult mini boss is drawn as the trebuchet model: on the ground, aimed at the squad, firing when it casts.</summary>
public class CatapultModelTests
{
    FormationController _fc;
    [TearDown] public void TearDown() { if (_fc != null) Object.DestroyImmediate(_fc.gameObject); }

    [UnityTest]
    public IEnumerator Catapult_StandsOnTheGround_AimsAtTheSquad_AndPlaysFireWhenItCasts()
    {
        var def = AssetDatabase.LoadAssetAtPath<EliteDefinition>("Assets/Data/Elites/Catapult.asset");
        Assert.IsNotNull(def); Assert.IsNotNull(def.Prefab, "catapult has its model");

        _fc = SquadTestRig.MakeFc();
        if (Object.FindFirstObjectByType<AudioListener>() == null) _fc.gameObject.AddComponent<AudioListener>();
        var dir = _fc.gameObject.AddComponent<GameDirector>();
        dir.HasBoss = false; dir.Squad = _fc; dir.ClearRule = StageClearRule.ReachEnd;
        dir.UnitFireRange = 0f; dir.SquadEngageRange = 0f;   // the squad holds fire, so the catapult lives to throw
        var view = new GameObject("elite view").AddComponent<EliteView>(); view.Director = dir;
        view.transform.SetParent(_fc.transform);
        yield return null;
        dir.Restart();
        var e = dir.Elites.Spawn(def, _fc.Anchor + new Vector2(0f, 10f), dir.Enemies);   // straight "north" of the squad
        yield return null; yield return null;

        Assert.AreEqual(1, view.DrawnCount);
        var model = view.transform.GetChild(0).GetChild(0);
        Assert.AreEqual(def.Prefab.name, model.name);

        // On the ground: the lowest point of the model is at ground level, not below it.
        var b = new Bounds(); bool first = true;
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        Assert.That(b.min.y, Is.EqualTo(0f).Within(0.08f), "wheels rest on the ground");
        Assert.That(b.size.y, Is.GreaterThan(1.5f), "big enough to read as a siege engine");

        // Aimed: the model throws toward its local -X, which must point at the squad.
        var toSquad = new Vector3(_fc.Anchor.x - e.X, 0f, _fc.Anchor.y - e.Z).normalized;
        Assert.That(Vector3.Dot(-model.right, toSquad), Is.GreaterThan(0.98f), "throwing side faces the squad");

        // Fires: when the cast starts, the Animator goes to the Fire state.
        var animator = model.GetComponentInChildren<Animator>();
        Assert.IsTrue(animator.HasState(0, Animator.StringToHash(def.States.Attack)), "the model has the attack state named in the data");
        bool fired = false, sawCast = false; string seen = "";
        for (float t = 0f; t < def.FirstAttackDelay + def.AttackGap + def.AttackGapRandom + 3f && !fired; t += Time.deltaTime)
        {
            yield return null;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            if (e.Casting) { sawCast = true; seen = "phase " + dir.Phase + " speed " + animator.speed + " enabled " + animator.isActiveAndEnabled; }
            if (e.Casting && (info.IsName(def.States.Attack) || next.IsName(def.States.Attack))) fired = true;
        }
        Assert.IsTrue(fired, "the fire animation played during a cast (cast seen: " + sawCast + ", " + seen + ", alive " + e.Alive + " hp " + e.Health + ", next " + e.Attacks.NextAttack + ")");
        var shot = model.GetComponent<CatapultThrow>();
        Assert.AreSame(shot.Socket, shot.Stone.transform.parent, "stone rides the arm during windup");
        Assert.IsFalse(shot.Trail.emitting);
        dir.TogglePause(); // drive this cast explicitly while the view keeps drawing frames
        var cast = e.Attacks.Current.Value;
        e.Attacks.Tick(_fc, null, shot.ReleaseSeconds - cast.Elapsed - 0.01f);
        yield return null; yield return null;
        Assert.AreSame(shot.Socket, shot.Stone.transform.parent, "stone stays seated through the extended windup");
        Vector3 beforeRelease = shot.Stone.transform.position;
        e.Attacks.Tick(_fc, null, shot.ReleaseSeconds - e.Attacks.Current.Value.Elapsed);
        yield return null; yield return null;
        Vector3 release = shot.Stone.transform.position;
        e.Attacks.Tick(_fc, null, 0.02f);
        yield return null; yield return null;
        Assert.AreNotSame(shot.Socket, shot.Stone.transform.parent, "stone left the arm");
        Assert.IsTrue(shot.Trail.emitting);
        Vector3 start = shot.Stone.transform.position;
        float departureAngle = Vector3.Angle(release - beforeRelease, start - release);
        Assert.That((release - beforeRelease).magnitude, Is.GreaterThan(0.01f));
        Assert.That((start - release).magnitude, Is.GreaterThan(0.01f));
        Assert.Greater((start - release).normalized.y, 0.55f, "stone leaves during the upward swing");
        Assert.That(departureAngle, Is.LessThan(2f), "flight continues along the animated swing");
        e.Attacks.Tick(_fc, null, e.Attacks.Current.Value.Remaining * 0.5f);
        yield return null; yield return null;
        Assert.Greater(shot.Trail.positionCount, 1, "trail has visible flight segments");
        var ground = new Vector3(cast.Cx, GroundHeight.Y(cast.Cx, cast.Cz), cast.Cz);
        Assert.Greater(shot.Stone.transform.position.y, release.y + 0.6f, "stone climbs above the arm before descending");
        Assert.Less(Vector3.Distance(shot.Stone.transform.position, ground), Vector3.Distance(start, ground));
        e.Attacks.Tick(_fc, null, e.Attacks.Current.Value.Remaining + 0.01f);
        var impact = Object.FindFirstObjectByType<CatapultImpact>();
        Assert.IsNotNull(impact);
        Assert.That(Vector3.Distance(impact.transform.position, ground), Is.LessThan(0.001f));
        Assert.IsTrue(impact.Chips.isPlaying);
        yield return null; yield return null;
        Assert.IsFalse(shot.Trail.emitting);
        Assert.AreSame(shot.Socket, shot.Stone.transform.parent, "next round reloaded");
        yield return new WaitForSeconds(0.37f);
        var edge = impact.Shockwave.GetPosition(0);
        Assert.That(Vector2.Distance(new Vector2(edge.x, edge.z), new Vector2(cast.Cx, cast.Cz)),
            Is.EqualTo(cast.Shape.RMax).Within(0.01f), "shockwave reaches the resolved damage radius");
        Object.Destroy(impact.gameObject);
        LogAssert.NoUnexpectedReceived();
    }
}
