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
        foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        Assert.That(b.min.y, Is.EqualTo(0f).Within(0.08f), "wheels rest on the ground");
        Assert.That(b.size.y, Is.GreaterThan(1.5f), "big enough to read as a siege engine");

        // Aimed: the model throws toward its local -X, which must point at the squad.
        var toSquad = new Vector3(_fc.Anchor.x - e.X, 0f, _fc.Anchor.y - e.Z).normalized;
        Assert.That(Vector3.Dot(-model.right, toSquad), Is.GreaterThan(0.98f), "throwing side faces the squad");

        // Fires: when the cast starts, the Animator goes to the Fire state.
        var animator = model.GetComponentInChildren<Animator>();
        Assert.IsTrue(animator.HasState(0, Animator.StringToHash(def.States.Attack)), "the model has the attack state named in the data");
        bool fired = false, cast = false; string seen = "";
        for (float t = 0f; t < def.FirstAttackDelay + def.AttackGap + def.AttackGapRandom + 3f && !fired; t += Time.deltaTime)
        {
            yield return null;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            if (e.Casting) { cast = true; seen = "phase " + dir.Phase + " speed " + animator.speed + " enabled " + animator.isActiveAndEnabled; }
            if (e.Casting && (info.IsName(def.States.Attack) || next.IsName(def.States.Attack))) fired = true;
        }
        Assert.IsTrue(fired, "the fire animation played during a cast (cast seen: " + cast + ", " + seen + ", alive " + e.Alive + " hp " + e.Health + ", next " + e.Attacks.NextAttack + ")");
        LogAssert.NoUnexpectedReceived();
    }
}
