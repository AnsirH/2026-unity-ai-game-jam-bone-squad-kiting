using System.Collections;
using BoneSquadKiting.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class EnemyHealthBarTests
{
    [UnityTest]
    public IEnumerator RapidHitsAccumulateAndReusingTheBarClearsTheFeedback()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/UI/EnemyHealthBar.prefab");
        var bar = Object.Instantiate(prefab).GetComponent<EnemyHealthBar>();
        var camera = new GameObject("Health bar test camera").AddComponent<Camera>();
        try
        {
            var fill = bar.transform.Find("Fill");
            var trail = bar.transform.Find("DamageTrail").GetComponent<SpriteRenderer>();
            var impact = bar.transform.Find("Impact").GetComponent<SpriteRenderer>();
            bar.Draw(Vector3.zero, camera, 100, 100);
            bar.Draw(Vector3.zero, camera, 80, 100);
            bar.Draw(Vector3.zero, camera, 60, 100);
            Assert.AreEqual(0.6f, fill.localScale.x);
            Assert.AreEqual(1f, trail.transform.localScale.x);
            Assert.IsTrue(trail.enabled && impact.enabled);

            yield return new WaitForSeconds(0.5f);
            bar.Draw(Vector3.zero, camera, 60, 100);
            Assert.IsFalse(trail.enabled || impact.enabled);

            bar.Draw(Vector3.zero, camera, 59, 100);
            Assert.IsTrue(impact.enabled);
            bar.gameObject.SetActive(false);
            bar.Draw(Vector3.one, camera, 30, 100);
            Assert.AreEqual(0.3f, fill.localScale.x);
            Assert.IsFalse(trail.enabled || impact.enabled);
        }
        finally
        {
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(bar.gameObject);
        }
    }
}
