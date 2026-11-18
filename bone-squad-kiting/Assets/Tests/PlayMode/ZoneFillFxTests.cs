using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;

/// <summary>Shell and Quadrant fill their area with the mage effect when they go off.</summary>
public class ZoneFillFxTests
{
    [Test]
    public void ProjectPatterns_ShellAndQuadrantHaveTheFillEffect()
    {
        int checkedCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:PatternDefinition", new[] { "Assets/Data/Patterns" }))
        {
            var p = AssetDatabase.LoadAssetAtPath<PatternDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (p.Kind != PatternKind.Burst || (p.Id != PatternId.Shell && p.Id != PatternId.Quadrant)) continue;
            Assert.IsNotNull(p.FillEffect, p.name + " has a fill effect");
            Assert.AreEqual("Mege_FX", p.FillEffect.name, p.name);
            checkedCount++;
        }
        Assert.That(checkedCount, Is.GreaterThanOrEqualTo(2));
    }

    [UnityTest]
    public IEnumerator WhenAZoneGoesOff_TheAreaIsFilled_AndTheCopiesLeaveAfterTheirTime()
    {
        var p = AssetDatabase.LoadAssetAtPath<PatternDefinition>("Assets/Data/Patterns/02_Quadrant.asset");
        var copy = Object.Instantiate(p); copy.FillSeconds = 0.4f;
        int before = ZoneFillFx.LiveCount;
        ZoneFillFx.Play(copy, copy.Shape, 3f, -2f, 30f);
        int shown = ZoneFillFx.LiveCount - before;
        Assert.That(shown, Is.GreaterThanOrEqualTo(16));
        var host = Object.FindFirstObjectByType<ZoneFillFx>();
        int inside = 0;
        foreach (Transform t in host.transform)
            if (t.gameObject.activeSelf && DangerShapeEval.InDanger(copy.Shape, 3f, -2f, t.position.x, t.position.z, 30f)) inside++;
        Assert.AreEqual(shown, inside, "every copy stands inside the turned zone");
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(before, ZoneFillFx.LiveCount, "taken away after FillSeconds");
        Object.Destroy(copy); Object.Destroy(host.gameObject);
        LogAssert.NoUnexpectedReceived();
    }
}

/// <summary>The Gate pattern: two spears come in from the caster's side and stick in the two bands, almost lying down.</summary>
public class ZoneStrikeFxTests
{
    [UnityTest]
    public IEnumerator Gate_TwoSpearsComeFromTheCastersSide_AndStickAlmostFlatAlongEachBand()
    {
        var p = AssetDatabase.LoadAssetAtPath<PatternDefinition>("Assets/Data/Patterns/01_Gate.asset");
        Assert.IsNotNull(p.StrikeEffect, "01_Gate has a strike effect");
        Assert.AreEqual("Spearman_Spear_FX", p.StrikeEffect.name);
        var copy = Object.Instantiate(p); copy.StrikeStaySeconds = 0.3f; copy.StrikeFadeSeconds = 0.15f;
        Assert.IsNotNull(p.StrikeImpactEffect, "01_Gate has a debris effect");
        Assert.That(p.StrikeShake, Is.GreaterThan(0f), "and shakes the camera");
        float mid = (copy.Shape.Inner + copy.Shape.Outer) * 0.5f;
        // Caster above the zone (+Z): 12 o'clock to 6 o'clock. Below it: the other way. Distance does not matter.
        foreach (var (caster, heading) in new[] { (new Vector2(-9f, 30f), -1f), (new Vector2(40f, -6f), 1f) })
        {
            int before = ZoneStrikeFx.LiveCount, impactsBefore = ZoneStrikeFx.Impacts;
            ZoneStrikeFx.Play(copy, copy.Shape, 3f, -2f, 0f, caster, 0.3f);
            Assert.AreEqual(2, ZoneStrikeFx.LiveCount - before, "one spear per band");
            var host = Object.FindFirstObjectByType<ZoneStrikeFx>();
            var spears = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in host.transform) if (t.gameObject.activeSelf && t.name.StartsWith(p.StrikeEffect.name)) spears.Add(t);
            float startSize = spears[0].lossyScale.z;
            foreach (var s in spears)
                Assert.That((s.position.z + 2f) * heading, Is.LessThan(-5f), "appears on the caster's side of the zone");
            Assert.AreEqual(impactsBefore, ZoneStrikeFx.Impacts, "nothing lands while they fly");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(impactsBefore + 2, ZoneStrikeFx.Impacts, "each spear lands once: shake and debris");
            int debris = 0;
            foreach (Transform t in host.transform)
                if (t.name.StartsWith(p.StrikeImpactEffect.name))
                {
                    debris++;
                    Assert.That(t.lossyScale.x, Is.EqualTo(2f * copy.Shape.HalfZ * copy.StrikeImpactScale).Within(0.5f), "debris sized to the spear");
                    Assert.IsTrue(DangerShapeEval.InDanger(copy.Shape, 3f, -2f, t.position.x, t.position.z), "where the tip went in, inside the band");
                }
            Assert.That(debris, Is.GreaterThanOrEqualTo(2));
            bool left = false, right = false;
            foreach (var s in spears)
            {
                Assert.That(s.forward.z * heading, Is.GreaterThan(0.95f), "lies along the band, pointing away from the caster's side");
                Assert.That(s.forward.y, Is.LessThan(-0.05f).And.GreaterThan(-0.35f), "almost flat, tip slightly down");
                Assert.That(Mathf.Abs(s.position.x - 3f), Is.EqualTo(mid).Within(0.1f), "in the middle of its band");
                Assert.That(s.position.z, Is.EqualTo(-2f).Within(0.1f), "centred along the band");
                if (s.position.x > 3f) right = true; else left = true;
                Assert.That(s.lossyScale.z, Is.GreaterThan(startSize * 3f), "still full size: it fades out instead of shrinking");
                float length = s.lossyScale.z / startSize * 1.89f;
                Assert.That(length, Is.EqualTo(2f * copy.Shape.HalfZ).Within(1.5f), "grown to the band's length");
            }
            Assert.IsTrue(left && right, "one in each band");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(before, ZoneStrikeFx.LiveCount, "taken away after the stay");
        }
        Object.Destroy(copy); Object.Destroy(Object.FindFirstObjectByType<ZoneStrikeFx>().gameObject);
        LogAssert.NoUnexpectedReceived();
    }
}
