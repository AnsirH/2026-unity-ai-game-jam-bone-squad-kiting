using UnityEngine;
using BoneSquadKiting.Data;

namespace BoneSquadKiting.Core
{
    public static class FormationSlots
    {
        /// <summary>
        /// Slot offsets from the squad anchor as Vector2(x, z), always <see cref="GameConstants.SquadSize"/> entries.
        /// Slot order matches the prototype so unit-to-slot assignment stays stable across formations.
        /// </summary>
        public static Vector2[] Generate(FormationDefinition d)
        {
            const int n = GameConstants.SquadSize;
            var o = new Vector2[n];
            switch (d.Mode)
            {
                case FormationMode.Grid: // 5x5, centered
                    for (int i = 0; i < n; i++)
                        o[i] = new Vector2((i % 5 - 2) * d.Spacing, (i / 5 - 2) * d.Spacing);
                    break;
                case FormationMode.Line: // staggered column along z
                    for (int i = 0; i < n; i++)
                        o[i] = new Vector2((i % 3 - 1) * d.Stagger, (i - 12) * d.Spacing);
                    break;
                case FormationMode.Ring:
                    for (int i = 0; i < n; i++)
                    {
                        float a = Mathf.PI * 2f * i / n;
                        o[i] = new Vector2(Mathf.Cos(a) * d.Radius, Mathf.Sin(a) * d.Radius);
                    }
                    break;
                case FormationMode.Cross: // center + 6 per arm, ordered -x, +x, -z, +z
                    o[0] = Vector2.zero;
                    for (int i = 1; i <= 6; i++)
                    {
                        int b = (i - 1) * 4;
                        o[b + 1] = new Vector2(-i * d.Spacing, 0);
                        o[b + 2] = new Vector2(i * d.Spacing, 0);
                        o[b + 3] = new Vector2(0, -i * d.Spacing);
                        o[b + 4] = new Vector2(0, i * d.Spacing);
                    }
                    break;
            }
            return o;
        }

        /// <summary>
        /// Rotate slot offsets around the anchor. Positive degrees turn clockwise as seen from above
        /// (a slot at +z moves toward +x). Returns a new array.
        /// </summary>
        public static Vector2[] Rotate(Vector2[] slots, float clockwiseDegrees)
        {
            float rad = clockwiseDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            var o = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                o[i] = new Vector2(slots[i].x * cos + slots[i].y * sin, -slots[i].x * sin + slots[i].y * cos);
            return o;
        }
    }
}
