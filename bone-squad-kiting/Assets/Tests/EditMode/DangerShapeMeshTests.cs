using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

/// <summary>The drawn telegraph must cover exactly what the evaluator calls dangerous.</summary>
public class DangerShapeMeshTests
{
    static readonly DangerShape[] Shapes =
    {
        DangerShape.Disc(3.10f), DangerShape.Annulus(1.73f, 6.05f),
        DangerShape.BandsX(0.83f, 6.4f, 7.1f), DangerShape.Quadrants(0.56f, 5.7f),
    };

    [Test]
    public void EveryTriangleLiesInsideTheDangerArea([ValueSource(nameof(Shapes))] DangerShape shape)
    {
        var mesh = DangerShapeMesh.Build(shape);
        var v = mesh.vertices; var t = mesh.triangles;
        Assert.That(t.Length, Is.GreaterThan(0));
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
            Assert.IsTrue(DangerShapeEval.InDanger(shape, 0, 0, c.x, c.z), $"triangle {i / 3} centroid {c} is outside");
        }
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void MeshBounds_MatchShapeExtent()
    {
        var disc = DangerShapeMesh.Build(DangerShape.Disc(3.10f));
        Assert.That(disc.bounds.extents.x, Is.EqualTo(3.10f).Within(0.01f));
        var bands = DangerShapeMesh.Build(DangerShape.BandsX(0.83f, 6.4f, 7.1f));
        Assert.That(bands.bounds.extents.x, Is.EqualTo(6.4f).Within(0.01f));
        Assert.That(bands.bounds.extents.z, Is.EqualTo(7.1f).Within(0.01f));
        Object.DestroyImmediate(disc); Object.DestroyImmediate(bands);
    }
}
