using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

public class WalkGridTests
{
    [Test]
    public void Open_CoversTheRectangle_AndMapsPointsToCells()
    {
        var g = WalkGrid.Open(new Vector2(-2, -1), new Vector2(2, 1), 0.5f);
        Assert.AreEqual(8, g.Width); Assert.AreEqual(4, g.Height);
        Assert.AreEqual(new Vector2Int(0, 0), g.Cell(new Vector2(-1.9f, -0.9f)));
        Assert.AreEqual(new Vector2Int(7, 3), g.Cell(new Vector2(1.9f, 0.9f)));
        Assert.That(g.CellCenter(0, 0), Is.EqualTo(new Vector2(-1.75f, -0.75f)));
        Assert.IsTrue(g.IsWalkable(Vector2.zero));
        Assert.IsFalse(g.IsWalkable(new Vector2(5, 0)), "outside the grid is never walkable");
    }

    [Test]
    public void Slide_StopsAtWalls_AndSlidesAlongThem()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(4, 4), 1f);
        g.Walkable[g.Index(2, 1)] = false;            // a pillar at cell (2,1)
        var from = new Vector2(1.5f, 1.5f);

        Assert.AreEqual(new Vector2(1.5f, 2.5f), g.Slide(from, new Vector2(1.5f, 2.5f)), "free move");
        Assert.AreEqual(new Vector2(1.5f, 1.8f), g.Slide(from, new Vector2(2.5f, 1.8f)), "x blocked: keeps the z part");
        Assert.AreEqual(from, g.Slide(from, new Vector2(2.5f, 1.5f)), "fully blocked: stays put");
        Assert.AreEqual(new Vector2(1.5f, 0.5f), g.Slide(from, new Vector2(-3f, 0.5f)), "leaving the grid is blocked like a wall");
    }

    [Test]
    public void NearestWalkable_ReturnsThePointOrTheClosestOpenCell()
    {
        var g = WalkGrid.Open(Vector2.zero, new Vector2(3, 1), 1f);
        g.Walkable[g.Index(1, 0)] = false;
        Assert.AreEqual(new Vector2(0.2f, 0.3f), g.NearestWalkable(new Vector2(0.2f, 0.3f)));
        Assert.AreEqual(new Vector2(0.5f, 0.5f), g.NearestWalkable(new Vector2(1.2f, 0.5f)), "blocked cell: nearest open center");
        Assert.AreEqual(new Vector2(2.5f, 0.5f), g.NearestWalkable(new Vector2(9f, 0.5f)), "off the grid: nearest open center");
    }
}
