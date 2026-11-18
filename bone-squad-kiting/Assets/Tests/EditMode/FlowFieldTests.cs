using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Core;

public class FlowFieldTests
{
    // Build a grid from rows of text, top row = highest z. '#' is blocked, anything else is open.
    static WalkGrid Grid(params string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var g = new WalkGrid(Vector2.zero, 1f, w, h);
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
                g.Walkable[g.Index(x, h - 1 - r)] = rows[r][x] != '#';
        return g;
    }

    static FlowField Field(WalkGrid g, int tx, int tz) { var f = new FlowField(g); f.Build(new Vector2Int(tx, tz)); return f; }

    [Test]
    public void EmptyGrid_EveryDirectionPointsAtTheTarget()
    {
        var g = Grid(".....", ".....", ".....", ".....", ".....");
        var f = Field(g, 2, 2);
        Assert.AreEqual(Vector2.zero, f.Direction[g.Index(2, 2)], "nothing to do at the target");
        for (int z = 0; z < 5; z++)
            for (int x = 0; x < 5; x++)
            {
                if (x == 2 && z == 2) continue;
                var toTarget = new Vector2(2 - x, 2 - z);
                var dir = f.Direction[g.Index(x, z)];
                Assert.That(dir.magnitude, Is.EqualTo(1f).Within(1e-4f), $"cell {x},{z}");
                Assert.That(Vector2.Dot(dir, toTarget.normalized), Is.GreaterThan(0.7f), $"cell {x},{z} heads toward the target");
            }
        Assert.AreEqual(2, f.Distance[g.Index(0, 0)], "diagonals count as one step");
    }

    [Test]
    public void Wall_IsWalkedAround()
    {
        // Target on the right, start on the left, a wall between them with a gap at the bottom.
        var g = Grid(
            "..#..",
            "..#..",
            "..#..",
            ".....");
        var f = Field(g, 4, 3);
        Assert.AreNotEqual(FlowField.Unreachable, f.Distance[g.Index(0, 3)]);
        Assert.That(f.Direction[g.Index(1, 3)].y, Is.LessThan(0f), "next to the wall: head down toward the gap, not into the wall");
        Assert.That(f.Distance[g.Index(0, 3)], Is.GreaterThan(4), "the route is longer than the straight line");

        // Following the field from the start reaches the target without entering a blocked cell.
        var cell = new Vector2Int(0, 3);
        for (int step = 0; step < 20 && cell != f.Target; step++)
        {
            var d = f.Direction[g.Index(cell.x, cell.y)];
            cell += new Vector2Int(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y));
            Assert.IsTrue(g.IsWalkable(cell.x, cell.y), $"stepped into a wall at {cell}");
        }
        Assert.AreEqual(f.Target, cell);
    }

    [Test]
    public void UnreachableCells_HaveNoDirection()
    {
        var g = Grid(
            ".#...",
            ".#...",
            ".#...");
        var f = Field(g, 4, 1);
        Assert.AreEqual(FlowField.Unreachable, f.Distance[g.Index(0, 1)]);
        Assert.AreEqual(Vector2.zero, f.Direction[g.Index(0, 1)]);
        Assert.AreEqual(Vector2.zero, f.Direction[g.Index(1, 1)], "blocked cells have no direction either");
        Assert.AreEqual(Vector2.zero, f.DirectionAt(new Vector2(-5, -5)), "off the grid");
    }

    [Test]
    public void Diagonals_DoNotCutWallCorners()
    {
        // The only diagonal from (0,0) to (1,1) squeezes between two blocked cells: it must not be used.
        var g = Grid(
            "#.",
            ".#");
        var f = Field(g, 1, 1);
        Assert.AreEqual(FlowField.Unreachable, f.Distance[g.Index(0, 0)]);

        // One open side is not enough either: going from (0,0) to (1,1) must step orthogonally first.
        var g2 = Grid(
            "..",
            ".#");
        var f2 = Field(g2, 1, 1);
        Assert.AreEqual(2, f2.Distance[g2.Index(0, 0)]);
        Assert.AreEqual(new Vector2(0, 1), f2.Direction[g2.Index(0, 0)]);
    }

    [Test]
    public void BlockedTarget_LeavesTheWholeFieldEmpty()
    {
        var g = Grid("..#");
        var f = Field(g, 2, 0);
        Assert.AreEqual(FlowField.Unreachable, f.Distance[g.Index(0, 0)]);
        Assert.AreEqual(Vector2.zero, f.Direction[g.Index(0, 0)]);
    }

    [Test]
    public void Retarget_RebuildsOnlyWhenTheTargetChangesCell()
    {
        var g = Grid(".....", ".....");
        var f = new FlowField(g);
        Assert.IsTrue(f.Retarget(new Vector2(0.2f, 0.2f)));
        Assert.IsFalse(f.Retarget(new Vector2(0.8f, 0.6f)), "same cell");
        Assert.IsTrue(f.Retarget(new Vector2(3.5f, 0.5f)));
        Assert.That(f.DirectionAt(new Vector2(0.5f, 0.5f)).x, Is.GreaterThan(0f));
    }
}
