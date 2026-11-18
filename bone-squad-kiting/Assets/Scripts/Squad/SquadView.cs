using System.Collections.Generic;
using UnityEngine;

namespace BoneSquadKiting.Squad
{
    /// <summary>Temporary visuals: one capsule per unit, positioned from the unit data each frame.</summary>
    public class SquadView : MonoBehaviour
    {
        public FormationController Squad;
        public Material UnitMaterial;
        public float UnitScale = 0.34f;

        readonly List<Transform> _views = new();

        void Awake()
        {
            // Units already placed under this object (so they show in the editor) are reused in order.
            foreach (Transform child in transform) _views.Add(child);
        }

        void LateUpdate()
        {
            if (Squad == null) return;
            while (_views.Count < Squad.Units.Count) _views.Add(CreateView(_views.Count));
            for (int i = 0; i < _views.Count; i++)
            {
                bool alive = i < Squad.Units.Count && !Squad.Units[i].Dead;
                if (_views[i].gameObject.activeSelf != alive) _views[i].gameObject.SetActive(alive);
                if (!alive) continue;
                var u = Squad.Units[i];
                _views[i].position = new Vector3(u.X, UnitScale, u.Z);
            }
        }

        Transform CreateView(int index)
        {
            var go = BuildUnit(index, UnitScale, UnitMaterial);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>One placeholder unit. Also used by the scene builder to pre-place the squad.</summary>
        public static GameObject BuildUnit(int index, float scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Unit " + index;
            var col = go.GetComponent<Collider>(); // units are not pickable or physical
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            go.transform.localScale = Vector3.one * scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
