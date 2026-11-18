using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

public class DangerShapeFillTests
{
    static List<Vector2> Fill(DangerShape s, float spacing) { var l = new List<Vector2>(); DangerShapeFill.Points(s, spacing, l); return l; }

    [Test]
    public void FillPoints_CoverQuadrantsAndTheShellRing_AndAllLieInsideTheShape()
    {
        var quadrant = DangerShape.Quadrants(0.56f, 5.7f);
        var shell = new DangerShape { Type = DangerShapeType.Annulus, RMin = 1.73f, ROut = 6.05f };
        foreach (var s in new[] { quadrant, shell })
        {
            var pts = Fill(s, 2f);
            Assert.That(pts.Count, Is.GreaterThanOrEqualTo(16), s.Type + ": enough copies to fill the area");
            Assert.That(pts.Count, Is.LessThanOrEqualTo(60), s.Type + ": but not a flood of them");
            foreach (var p in pts) Assert.IsTrue(DangerShapeEval.InDanger(s, 0f, 0f, p.x, p.y), s.Type + " point " + p + " is inside");
        }
        var q = Fill(quadrant, 2f);
        int[] perCorner = new int[4];
        foreach (var p in q) perCorner[(p.x > 0 ? 1 : 0) + (p.y > 0 ? 2 : 0)]++;
        foreach (int n in perCorner) Assert.AreEqual(q.Count / 4, n, "every quadrant gets the same share");
        Assert.That(Fill(quadrant, 1f).Count, Is.GreaterThan(q.Count), "smaller spacing = more copies");
    }
}
