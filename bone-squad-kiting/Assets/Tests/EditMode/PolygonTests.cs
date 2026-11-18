using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

public class PolygonTests
{
    static readonly Vector2[] Square = { new(0, 0), new(4, 0), new(4, 4), new(0, 4) };
    // An L: the top-right quarter of the square is cut away.
    static readonly Vector2[] LShape = { new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4) };

    [Test]
    public void Contains_InsideAndOutsideASquare()
    {
        Assert.IsTrue(Polygon.Contains(Square, new Vector2(2, 2)));
        Assert.IsTrue(Polygon.Contains(Square, new Vector2(0.1f, 3.9f)));
        Assert.IsFalse(Polygon.Contains(Square, new Vector2(5, 2)));
        Assert.IsFalse(Polygon.Contains(Square, new Vector2(2, -0.1f)));
    }

    [Test]
    public void Contains_HandlesConcaveShapes()
    {
        Assert.IsTrue(Polygon.Contains(LShape, new Vector2(1, 3)));
        Assert.IsTrue(Polygon.Contains(LShape, new Vector2(3, 1)));
        Assert.IsFalse(Polygon.Contains(LShape, new Vector2(3, 3)), "the cut-away corner");
    }

    [Test]
    public void Contains_DoesNotDependOnPointOrder()
    {
        var reversed = (Vector2[])LShape.Clone();
        System.Array.Reverse(reversed);
        foreach (var p in new[] { new Vector2(1, 3), new Vector2(3, 1), new Vector2(3, 3), new Vector2(9, 9) })
            Assert.AreEqual(Polygon.Contains(LShape, p), Polygon.Contains(reversed, p), p.ToString());
    }

    [Test]
    public void FewerThanThreePoints_ContainNothing()
    {
        Assert.IsFalse(Polygon.Contains(new[] { new Vector2(0, 0), new Vector2(4, 4) }, new Vector2(2, 2)));
        Assert.IsFalse(Polygon.Contains(null, Vector2.zero));
    }
}
