using System.Collections.Generic;

namespace BoneSquadKiting.Core
{
    public enum PatternId { Core, Gate, Quadrant, Shell }

    public readonly struct AttackPattern
    {
        public readonly PatternId Id;
        public readonly string Name, Advice, Detail;
        public readonly string Recommend; // "dense" | "line" | "ring" | "cross"
        public readonly float Time;

        public AttackPattern(PatternId id, string name, string advice, string recommend, string detail, float time)
        {
            Id = id; Name = name; Advice = advice; Recommend = recommend; Detail = detail; Time = time;
        }
    }

    /// <summary>Boss attack sequence, order and cast times copied 1:1 from prototype ATTACKS[].</summary>
    public static class AttackTable
    {
        public static readonly IReadOnlyList<AttackPattern> Sequence = new List<AttackPattern>
        {
            new(PatternId.Core,     "중앙 핵폭발", "3 · 원형",   "ring",  "중앙이 폭발합니다. 원형으로 흩어지세요!", 2.15f),
            new(PatternId.Gate,     "쌍벽 압착",   "2 · 일자",   "line",  "좌우가 휩쓸립니다. 일자로 줄이세요!",      2.05f),
            new(PatternId.Quadrant, "사분면 폭격", "4 · 십자가", "cross", "십자 통로만 안전합니다!",                2.0f),
            new(PatternId.Shell,    "외곽 소거",   "1 · 밀집",   "dense", "바깥부터 폭발합니다. 중심으로 모이세요!",  2.1f),
            new(PatternId.Core,     "중앙 핵폭발", "3 · 원형",   "ring",  "중앙이 폭발합니다. 원형으로 흩어지세요!", 1.85f),
            new(PatternId.Quadrant, "사분면 폭격", "4 · 십자가", "cross", "십자 통로만 안전합니다!",                1.8f),
            new(PatternId.Gate,     "쌍벽 압착",   "2 · 일자",   "line",  "좌우가 휩쓸립니다. 일자로 줄이세요!",      1.8f),
            new(PatternId.Shell,    "외곽 소거",   "1 · 밀집",   "dense", "바깥부터 폭발합니다. 중심으로 모이세요!",  1.8f),
        };
    }
}
