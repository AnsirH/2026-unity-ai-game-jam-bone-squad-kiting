using BoneSquadKiting.Stage;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoneSquadKiting.Fx
{
    public sealed class CommandPulse : MonoBehaviour
    {
        const float Duration = 0.42f;
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static readonly int Tint = Shader.PropertyToID("_Tint");
        static readonly Color MoveColor = new(0.27f, 1f, 0.48f);
        static readonly Color AttackColor = new(1f, 0.48f, 0.12f);

        Material _material;
        float _start;

        public static CommandPulse Create(Transform owner)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Command Pulse";
            quad.transform.SetParent(owner, true);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 1.44f;
            var collider = quad.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            var pulse = quad.AddComponent<CommandPulse>();
            pulse._material = new Material(Resources.Load<Shader>("CommandPulse"));
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = pulse._material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return pulse;
        }

        public void Show(Vector2 point, bool attack)
        {
            transform.position = new Vector3(point.x, GroundHeight.Y(point.x, point.y) + 0.12f, point.y);
            _material.SetColor(Tint, attack ? AttackColor : MoveColor);
            _material.SetFloat(Progress, 0f);
            _start = Time.unscaledTime;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float t = (Time.unscaledTime - _start) / Duration;
            if (t >= 1f) gameObject.SetActive(false);
            else _material.SetFloat(Progress, t);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}