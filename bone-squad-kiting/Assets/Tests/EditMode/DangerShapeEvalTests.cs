using NUnit.Framework;
using BoneSquadKiting.Core;

public class DangerShapeEvalTests
{
    [Test]
    public void Disc_InsideRadiusIsDanger()
    {
        var s = DangerShape.Disc(3.10f);
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, 2.0f, 0));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 3.2f, 0));
    }

    [Test]
    public void BandsX_CenterCorridorIsSafe()
    {
        var s = DangerShape.BandsX(0.83f, 6.4f, 7.1f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 0.5f, 0));  // inside the corridor
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, 2.0f, 0));
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, -2.0f, 0));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 7.0f, 0));  // beyond the outer edge
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 2.0f, 7.5f)); // beyond the z extent
    }

    [Test]
    public void Quadrants_CrossCorridorSafe()
    {
        var s = DangerShape.Quadrants(0.56f, 5.7f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 0.0f, 3.0f)); // on an axis
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 3.0f, 0.0f));
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, 3.0f, 3.0f));  // diagonal
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, -3.0f, 3.0f));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 6.0f, 3.0f)); // outside the box
    }

    [Test]
    public void Annulus_CenterSafeOuterDanger()
    {
        var s = DangerShape.Annulus(1.73f, 6.05f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 1.0f, 0));
        Assert.IsTrue(DangerShapeEval.InDanger(s, 0, 0, 4.0f, 0));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0, 0, 7.0f, 0));
    }

    [Test]
    public void Shape_IsEvaluatedRelativeToCenter()
    {
        var s = DangerShape.Disc(3.10f);
        Assert.IsTrue(DangerShapeEval.InDanger(s, 5f, -2f, 6f, -2f));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 5f, -2f, 0f, 0f));
    }
}
