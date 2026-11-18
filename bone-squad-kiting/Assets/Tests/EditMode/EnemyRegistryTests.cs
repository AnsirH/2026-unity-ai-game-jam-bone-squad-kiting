using NUnit.Framework;
using UnityEngine;
using BoneSquadKiting.Combat;

public class EnemyRegistryTests
{
    class FakeEnemy : IEnemy
    {
        public Vector2 Pos { get; set; }
        public bool Alive { get; set; } = true;
    }

    [Test]
    public void NearestInRange_PicksClosestAlive()
    {
        var reg = new EnemyRegistry();
        var a = new FakeEnemy { Pos = new Vector2(2, 0) };
        var b = new FakeEnemy { Pos = new Vector2(5, 0) };
        reg.Register(a); reg.Register(b);

        Assert.AreSame(a, reg.NearestInRange(Vector2.zero, 10f));
        a.Alive = false;
        Assert.AreSame(b, reg.NearestInRange(Vector2.zero, 10f)); // dead enemies are skipped
        Assert.IsNull(reg.NearestInRange(Vector2.zero, 1f));      // nothing in range
    }

    [Test]
    public void Register_IgnoresDuplicates_AndUnregisterRemoves()
    {
        var reg = new EnemyRegistry();
        var a = new FakeEnemy { Pos = new Vector2(1, 0) };
        reg.Register(a); reg.Register(a);
        Assert.AreEqual(1, reg.Enemies.Count);
        reg.Unregister(a);
        Assert.IsNull(reg.NearestInRange(Vector2.zero, 10f));
    }
}
