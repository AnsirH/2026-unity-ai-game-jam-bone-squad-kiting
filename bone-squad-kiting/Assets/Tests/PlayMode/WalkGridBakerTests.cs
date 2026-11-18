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

    WalkOutline Outline(WalkOutlineMode mode, params Vector2[] localPoints)
    {
        var go = new GameObject("outline " + mode);
        go.transform.SetParent(_root.transform, false);
        var outline = go.AddComponent<WalkOutline>();
        outline.Mode = mode;
        foreach (var p in localPoints)
        {
            var point = new GameObject("point");
            point.transform.SetParent(go.transform, false);
            point.transform.localPosition = new Vector3(p.x, 0, p.y);
        }
        return outline;
    }

    WalkGridBaker BigFloor(int ground, int obstacle)
    {
        _root = new GameObject("stage");
        _root.transform.position = new Vector3(200f, 0f, 200f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);   // 10 x 10 floor: one big slab, like a terrain
        floor.transform.SetParent(_root.transform, false);
        floor.transform.localPosition = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(10, 1, 10);
        floor.layer = ground;
        var baker = _root.AddComponent<WalkGridBaker>();
        baker.Size = new Vector2(10, 10); baker.CellSize = 1f; baker.Clearance = 0.2f;
        baker.GroundMask = 1 << ground; baker.ObstacleMask = 1 << obstacle;
        return baker;
    }

    [Test]
    public void Outlines_LimitTheAreaToIncludeShapes_AndCutExcludeHoles()
    {
        var baker = BigFloor(20, 21);
        Vector2 P(float x, float z) => new(200f + x, 200f + z);

        Assert.IsTrue(baker.Bake().IsWalkable(P(4.5f, 4.5f)), "no outlines: the whole slab is walkable");

        Outline(WalkOutlineMode.Include, new Vector2(-3, -3), new Vector2(3, -3), new Vector2(3, 3), new Vector2(-3, 3));
        Outline(WalkOutlineMode.Exclude, new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1));
        var grid = baker.Bake();

        Assert.IsTrue(grid.IsWalkable(P(2.5f, 2.5f)), "inside the include outline");
        Assert.IsTrue(grid.IsWalkable(P(-2.5f, 0.5f)));
        Assert.IsFalse(grid.IsWalkable(P(4.5f, 4.5f)), "on the slab but outside the outline");
        Assert.IsFalse(grid.IsWalkable(P(0.5f, 0.5f)), "inside the exclude hole");
        Assert.IsFalse(grid.IsWalkable(P(-0.5f, -0.5f)));
    }

    [Test]
    public void Outlines_StillRespectObstacles_AndIgnoreUnfinishedShapes()
    {
        var baker = BigFloor(20, 21);
        Vector2 P(float x, float z) => new(200f + x, 200f + z);
        Outline(WalkOutlineMode.Include, new Vector2(-3, -3), new Vector2(3, -3), new Vector2(3, 3), new Vector2(-3, 3));
        Outline(WalkOutlineMode.Exclude, new Vector2(-2, -2), new Vector2(2, 2));   // two points: not a shape

        var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.transform.SetParent(_root.transform, false);
        rock.transform.localPosition = new Vector3(2.5f, 1f, 2.5f);
        rock.transform.localScale = new Vector3(0.9f, 2f, 0.9f);
        rock.layer = 21;

        var grid = baker.Bake();
        Assert.IsFalse(grid.IsWalkable(P(2.5f, 2.5f)), "obstacle inside the outline still blocks");
        Assert.IsTrue(grid.IsWalkable(P(0.5f, 0.5f)), "the two-point exclude outline is ignored");
    }

    [Test]
    public void FlatTerrain_CountsAsGround()
    {
        _root = new GameObject("stage");
        _root.transform.position = new Vector3(300f, 0f, 300f);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 10, 8) };
        var terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.SetParent(_root.transform, false);
        terrain.transform.localPosition = new Vector3(-4, 0, -4);   // a terrain's origin is its corner
        terrain.layer = 20;

        var baker = _root.AddComponent<WalkGridBaker>();
        baker.Size = new Vector2(12, 8); baker.CellSize = 1f;
        baker.GroundMask = 1 << 20; baker.ObstacleMask = 1 << 21;
        var grid = baker.Bake();

        Assert.IsTrue(grid.IsWalkable(new Vector2(300.5f, 300.5f)), "terrain collider is hit by the ground ray");
        Assert.IsTrue(grid.IsWalkable(new Vector2(296.5f, 303.5f)), "terrain corner");
        Assert.IsFalse(grid.IsWalkable(new Vector2(305.5f, 300.5f)), "past the terrain's edge");
        Object.DestroyImmediate(data);
    }

    [Test]
    public void BumpyTerrain_StillCountsAsGround()
    {
        _root = new GameObject("stage");
        _root.transform.position = new Vector3(400f, 0f, 400f);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 6, 8) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
                heights[z, x] = 0.2f + 0.2f * Mathf.Sin(x * 0.7f) * Mathf.Cos(z * 0.5f);   // rolling bumps, up to 2.4 units high
        data.SetHeights(0, 0, heights);
        var terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.SetParent(_root.transform, false);
        terrain.transform.localPosition = new Vector3(-4, 0, -4);
        terrain.layer = 20;

        var baker = _root.AddComponent<WalkGridBaker>();
        baker.Size = new Vector2(8, 8); baker.CellSize = 1f;
        baker.GroundMask = 1 << 20; baker.ObstacleMask = 1 << 21;
        var grid = baker.Bake();

        for (int z = 0; z < grid.Height; z++)
            for (int x = 0; x < grid.Width; x++)
                Assert.IsTrue(grid.Walkable[grid.Index(x, z)], $"cell {x},{z} on bumpy terrain");
        Object.DestroyImmediate(data);
    }
}
