using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Game;
using BoneSquadKiting.Squad;

namespace BoneSquadKiting.UI
{
    public sealed class HitCounterHud : MonoBehaviour
    {
        Hud _hud;
        ShotSystem _source;
        CanvasGroup _group;
        Text _number, _status, _speed;
        int _serial = -1;
        float _pulse;

        void Start()
        {
            _hud = GetComponent<Hud>();
            var root = new GameObject("Hit Counter", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            var r = (RectTransform)root.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(112f, -12f);
            r.sizeDelta = new Vector2(190f, 142f);
            _group = root.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = _group.interactable = false;
            _group.alpha = 0f;
            var art = Resources.Load<HudArtwork>("HudArtwork");
            Font font = art != null && art.TitleFont != null ? art.TitleFont : _hud.AliveText.font;
            _number = Label(root.transform, "Count", "0", font, 48, new Vector2(0f, 18f));
            Label(root.transform, "Caption", "H I T S", font, 15, new Vector2(0f, -20f));
            _speed = Label(root.transform, "Attack Speed", "ATK SPD x1.00", font, 14, new Vector2(0f, -46f));
            _status = Label(root.transform, "Status", "", font, 11, new Vector2(0f, -68f));
        }

        static Text Label(Transform parent, string name, string content, Font font, int size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content; t.font = font; t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(.86f, .86f, .83f);
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(190f, size + 18f);
            t.rectTransform.anchoredPosition = position;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .8f);
            return t;
        }

        void Update()
        {
            if (_group == null || _hud == null) return;
            var director = _hud.Director;
            var shots = director != null ? director.Shots : null;
            if (shots == null || director.Phase != GamePhase.Play)
            { _group.alpha = 0f; return; }
            if (_source != shots) { _source = shots; _serial = -1; _pulse = 0f; }
            if (_serial != shots.HitSerial)
            {
                _serial = shots.HitSerial;
                if (shots.ConsecutiveHits > 0) _pulse = 1f;
                _number.text = shots.ConsecutiveHits.ToString();
            }
            _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime / .18f);
            _number.rectTransform.localScale = Vector3.one * (1f + .15f * Mathf.Sin(_pulse * Mathf.PI * .5f));
            _number.color = Color.Lerp(new Color(.8f, .8f, .77f), Color.white, _pulse);
            int maxed = 0;
            float speed = 1f;
            foreach (var unit in director.Squad.Units)
            {
                if (unit.Dead) continue;
                speed = Mathf.Max(speed, unit.AttackSpeedMultiplier);
                if (unit.AtAttackSpeedCap) maxed++;
            }
            _number.text = shots.ConsecutiveHits.ToString();
            _group.alpha = speed > 1.0001f ? 1f :
                (shots.ConsecutiveHits > 0 ? Mathf.Clamp01(shots.HitCounterRemaining / .35f) : 0f);
            _speed.text = $"ATK SPD x{speed:F2}";
            _speed.color = maxed > 0 ? new Color(1f, .25f, .2f) : new Color(.86f, .86f, .83f);
            _status.text = maxed > 0 ? $"{maxed} AT MAX SPEED" : "HIGHEST UNIT SPEED";
        }
    }
}
