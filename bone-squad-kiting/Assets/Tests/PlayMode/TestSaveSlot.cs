using NUnit.Framework;
using BoneSquadKiting.Game;

/// <summary>Every play mode test uses its own save slot, so running tests never touches a real save.</summary>
[SetUpFixture]
public class TestSaveSlot
{
    string _realKey;

    [OneTimeSetUp]
    public void UseATestSlot()
    {
        _realKey = GameProgress.Key;
        GameProgress.Key = "bsk.test.nextStage";
        GameProgress.Clear();
    }

    [OneTimeTearDown]
    public void GiveTheRealSlotBack()
    {
        GameProgress.Clear();
        GameProgress.Key = _realKey;
    }
}
