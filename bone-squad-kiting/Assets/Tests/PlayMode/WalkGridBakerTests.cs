using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Stage;

public class WalkGridBakerTests
{
    GameObject _root;

    [TearDown] public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

    [Test]
    public void Bake_MarksGroundWalkable_ObstaclesAndGapsBlocked()
    {
        // Layers 20 / 21 stand in for Ground / Obstacle so the test does not depend on project layer names.
        const int ground = 20, obstacle = 21;
        _root = new GameObject("stage");
        _root.transform.position = new Vector3(100f, 0f, 100f); // away from anything else in the scene

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);   // 6 x 4 floor, top at y = 0
        floor.transform.SetParent(_root.transform, false);
        floor.transform.localPosition = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(6, 1, 4);
        floor.layer = ground;

        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);  // blocks the cell around local (1.5, 0.5)
        pillar.transform.SetParent(_root.transform, false);
        pillar.transform.localPosition = new Vector3(1.5f, 1f, 0.5f);
        pillar.transform.localScale = new Vector3(0.9f, 2f, 0.9f);
        pillar.layer = obstacle;

        var baker = _root.AddComponent<WalkGridBaker>();
        baker.Size = new Vector2(8, 4);        // wider than the floor: the outer columns hang over nothing
        baker.CellSize = 1f; baker.Clearance = 0.2f;
        baker.GroundMask = 1 << ground; baker.ObstacleMask = 1 << obstacle;
        var grid = baker.Bake();

        Assert.AreEqual(8, grid.Width); Assert.AreEqual(4, grid.Height);
        Vector2 P(float x, float z) => new(100f + x, 100f + z);
        Assert.IsTrue(grid.IsWalkable(P(-0.5f, 0.5f)), "open floor");
        Assert.IsTrue(grid.IsWalkable(P(2.5f, -1.5f)), "floor corner");
        Assert.IsFalse(grid.IsWalkable(P(1.5f, 0.5f)), "under the pillar");
        Assert.IsTrue(grid.IsWalkable(P(0.5f, 0.5f)), "next to the pillar");
        Assert.IsFalse(grid.IsWalkable(P(-3.5f, 0.5f)), "no floor under this cell");
        Assert.IsFalse(grid.IsWalkable(P(3.5f, 0.5f)), "no floor under this cell");
    }
}
