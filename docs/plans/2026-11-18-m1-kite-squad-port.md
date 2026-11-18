# KITE SQUAD 25 — M1 프로토타입 1:1 이식 Implementation Plan

> **실행 방법:** 태스크를 위에서부터 하나씩(TDD: 실패 테스트 → 구현 → 통과 → 커밋) 진행한다. 각 스텝은 체크박스(`- [ ]`)로 추적한다. Unity 작업은 `unity:unity-cli`·`unity:unity-package-management`·`unity:ui-ugui`·`unity:urp-postprocessing` 스킬을 해당 태스크에서 따른다.

**Goal:** HTML 프로토타입 `KITE SQUAD 25`의 게임플레이를 Unity 6000.3.28f1 · URP로 수치까지 동일하게 1:1 재현한다(임시 기하도형).

**Architecture:** 게임플레이는 프로토타입의 (x, z) 월드를 Unity **XZ 평면**에 직접 매핑하고, 2D 투영 수식 대신 **직교 쿼터뷰 카메라**로 렌더링한다. 결정론적 로직(진형 오프셋, 장판 판정, 수치)은 MonoBehaviour와 분리된 순수 C# 클래스로 두고 EditMode 테스트로 고정한다. 이동·평타·패턴·HUD는 PlayMode에서 통합 검증한다. 모든 상수는 원본과 동일.

**Tech Stack:** Unity 6000.3.28f1, URP, Unity Test Framework(EditMode/PlayMode), uGUI(HUD), TextMeshPro, unity-cli(헤드리스 빌드/테스트).

**좌표 매핑 규약(전 태스크 공통):** 원본 `world.x → Unity x`, `world.z → Unity z`, 높이 `h → Unity y`. 즉 지면은 XZ 평면(y=0). 원본의 모든 속도/거리/반경 상수를 Unity 월드 유닛으로 그대로 사용한다. 화면→월드 변환(`unproject`)은 카메라 레이를 y=0 평면과 교차시켜 구한다.

**작업 규칙:** `main/` 샌드박스 안에서만 작업한다. git push는 사용자가 명시적으로 요청할 때만. 커밋은 각 태스크 끝에서.

---

## File Structure

프로젝트 루트: `main/KiteSquad/`(Unity 프로젝트). 스크립트 루트: `Assets/Scripts/`.

| 파일 | 책임 |
| --- | --- |
| `Assets/Scripts/Core/GameConstants.cs` | 원본 상수 전부(수치 단일 출처) |
| `Assets/Scripts/Core/AttackPattern.cs` | 패턴 데이터(id,name,time,recommend,advice,detail)와 ATTACKS 시퀀스 |
| `Assets/Scripts/Core/Formations.cs` | `SlotsFor(formation)` — 진형별 오프셋(순수 함수) |
| `Assets/Scripts/Core/DangerZones.cs` | `InDanger(id, cx,cz, x,z)` — 장판 피격 판정(순수 함수) |
| `Assets/Scripts/Core/Combat.cs` | DPS 윈도 계산·페이즈 임계 등 순수 수치 로직 |
| `Assets/Scripts/Squad/FormationController.cs` | 중심점(anchor) 이동, 진형 전환, 유닛에 슬롯 배분 |
| `Assets/Scripts/Squad/SquadUnit.cs` | 유닛 추종(center+offset, SmoothDamp), 평타 쿨/발사, hp, 사망 |
| `Assets/Scripts/Squad/ShotSystem.cs` | 투사체 풀·이동·보스 명중·피해 |
| `Assets/Scripts/Boss/Boss.cs` | 보스 hp/phase/flash 상태 |
| `Assets/Scripts/Boss/BossAttackSystem.cs` | 패턴 스케줄·텔레그래프·resolve(장판 판정→유닛 피해) |
| `Assets/Scripts/Boss/TelegraphView.cs` | 장판 시각화(임시 반투명 메시/디캘) |
| `Assets/Scripts/Game/GameDirector.cs` | 상태머신(menu/play/win/lose), 일시정지, 승패, 시스템 오케스트레이션 |
| `Assets/Scripts/Input/SquadInputController.cs` | 우클릭/드래그 이동, 1~4 진형, P 일시정지, Enter 재시작 |
| `Assets/Scripts/UI/Hud.cs` | 보스바·생존·DPS·진형패널·경고·토스트·오버레이 바인딩 |
| `Assets/Scripts/Fx/JuiceFx.cs` | 플래시·파티클·스크린셰이크·링(임시) |
| `Assets/Tests/EditMode/*` | 순수 로직 테스트 |
| `Assets/Tests/PlayMode/*` | 통합 테스트 |

---

## Task 0: Unity 프로젝트 스캐폴딩

**Files:**
- Create: `main/KiteSquad/` (Unity 프로젝트 전체)
- Modify: `main/.gitignore` (Unity 무시 규칙 추가)

- [ ] **Step 1: Editor 6000.3.28f1 설치 확인**

`unity:unity-cli` 스킬을 따른다. 설치 여부 확인:
Run: `unity editors -i` (설치 목록) → 6000.3.28f1 없으면 `unity install --version 6000.3.28f1 --module windows-il2cpp`
Expected: 목록에 `6000.3.28f1`.

- [ ] **Step 2: URP 템플릿으로 프로젝트 생성**

Run: `unity create --path "main/KiteSquad" --version 6000.3.28f1 --template urp` (정확한 플래그는 unity-cli 스킬의 `create` 섹션 확인)
Expected: `main/KiteSquad/Assets`, `ProjectSettings/ProjectVersion.txt`에 `6000.3.28f1`.

- [ ] **Step 3: Unity용 .gitignore 추가**

`main/.gitignore` 끝에 표준 Unity 무시 규칙을 추가한다(`KiteSquad/[Ll]ibrary/`, `[Tt]emp/`, `[Oo]bj/`, `[Bb]uild/`, `[Ll]ogs/`, `[Uu]serSettings/`, `*.csproj`, `*.sln` 등). 규칙은 GitHub `Unity.gitignore` 표준을 사용하되 경로 앞에 `KiteSquad/`를 붙인다.

- [ ] **Step 4: 테스트 프레임워크 어셈블리 준비**

`Assets/Tests/EditMode/EditMode.asmdef`(참조: 프로젝트 런타임 asmdef + `UnityEngine.TestRunner`,`UnityEditor.TestRunner`, 플랫폼 EditMode), `Assets/Tests/PlayMode/PlayMode.asmdef` 생성. 런타임 코드용 `Assets/Scripts/KiteSquad.asmdef`도 생성해 테스트에서 참조. (ui-ugui/unity-cli 스킬에 asmdef 예시 있음.)

- [ ] **Step 5: Commit**

```bash
cd main && git add KiteSquad .gitignore && git commit -m "chore: scaffold Unity 6000.3.28f1 URP project for KiteSquad"
```

---

## Task 1: 아레나 씬 + 쿼터뷰 직교 카메라 + URP 포스트

**Files:**
- Create: `main/KiteSquad/Assets/Scenes/Arena.unity`
- Create: `main/KiteSquad/Assets/Settings/ArenaVolume.asset` (URP Volume Profile)

- [ ] **Step 1: 아레나 지면 생성**

빈 씬 `Arena`에 Plane을 놓고 XZ 범위 `x∈[-12.3,12.3]`, `z∈[-10.2,10.2]`를 덮도록 스케일(Plane 기본 10x10m이므로 scale ≈ (2.46, 1, 2.04), center 0). 어두운 머티리얼(URP/Lit, base color `#0d2434`).

- [ ] **Step 2: 직교 쿼터뷰 카메라**

Main Camera: `Projection = Orthographic`, `Orthographic Size ≈ 11`(아레나 z 반경+여유), rotation `(50, 0, 0)`(쿼터뷰 하향각), position를 아레나 중심이 화면 중앙에 오도록 배치(예: `(0, 18, -15)` 후 사이즈/각도로 조정). 배경색 `#050d17`.
Expected: 플레이 시 아레나 전체가 쿼터뷰로 보임.

- [ ] **Step 3: 라이팅 + URP 포스트(Bloom/Vignette)**

`unity:urp-postprocessing` 스킬을 따라 Global Volume + Volume Profile(`ArenaVolume`)에 **Bloom**(threshold~0.9, intensity~0.8)과 **Vignette**(intensity~0.35) 추가. 카메라 `Post Processing` 체크. Directional Light 1개(약한 상단광). 원본의 글로·비네트 분위기 재현.

- [ ] **Step 4: 검증(스크린샷)**

`unity:unity-cli` 스킬의 스크린샷/플레이모드 기능으로 씬을 캡처해 쿼터뷰·빛 분위기 확인.
Expected: 어두운 아레나 + 비네트 + (이후 오브젝트에서) 블룸.

- [ ] **Step 5: Commit**

```bash
cd main && git add KiteSquad/Assets && git commit -m "feat: add arena scene with quarter-view ortho camera and URP post"
```

---

## Task 2: 상수 + 패턴 데이터

**Files:**
- Create: `Assets/Scripts/Core/GameConstants.cs`
- Create: `Assets/Scripts/Core/AttackPattern.cs`

- [ ] **Step 1: 상수 작성(원본과 1:1)**

```csharp
namespace KiteSquad.Core
{
    public static class GameConstants
    {
        public const float BossMax = 24000f;
        public const float UnitMax = 100f;
        public const float UnitDamage = 33f;
        public const int SquadSize = 25;

        // Boss arena position (prototype BOSS)
        public const float BossX = 0f;
        public const float BossZ = -7.4f;

        // Hard arena limits (prototype LIMIT)
        public const float LimitXMin = -12.3f, LimitXMax = 12.3f;
        public const float LimitZMin = -10.2f, LimitZMax = 10.2f;

        // Destination clamp for the commander (prototype moveToPoint)
        public const float MoveClampX = 8.0f;
        public const float MoveClampZMin = -3.85f, MoveClampZMax = 4.2f;

        // Movement
        public const float AnchorSpeed = 7.4f;
        public const float UnitBaseSpeed = 9.7f;   // + min(9, dist*2.4)
        public const float UnitSpeedDistGain = 2.4f, UnitSpeedDistCap = 9f;
        public const float AnchorArrive = 0.024f, UnitArrive = 0.015f;

        // Combat
        public const float FireRange = 15.9f;
        public const float ShotSpeed = 22f;
        public const float ShotLife = 1.35f;
        public const float ShotHitRadius = 1.15f;
        public const float UnitCdMin = 0.76f, UnitCdRand = 0.16f;
        public const float PatternUnitDamage = 55f;

        // DPS window
        public const float DpsWindow = 2.4f;

        // Boss phase 2
        public const float Phase2HpFrac = 0.54f;
        public const float NextAttackPhase1 = 1.9f, NextAttackPhase2 = 1.45f;
        public const float FirstAttackDelay = 3.1f;

        // Telegraph lock: last 0.38s the zone stops tracking the commander
        public const float CastTrackLockTime = 0.38f;
    }
}
```

- [ ] **Step 2: 패턴 데이터 작성(원본 ATTACKS 순서 동일)**

```csharp
using System.Collections.Generic;
namespace KiteSquad.Core
{
    public enum PatternId { Core, Gate, Quadrant, Shell }

    public readonly struct AttackPattern
    {
        public readonly PatternId Id;
        public readonly string Name, Advice, Detail;
        public readonly string Recommend; // "dense"|"line"|"ring"|"cross"
        public readonly float Time;
        public AttackPattern(PatternId id, string name, string advice, string recommend, string detail, float time)
        { Id = id; Name = name; Advice = advice; Recommend = recommend; Detail = detail; Time = time; }
    }

    public static class AttackTable
    {
        // Order & times copied 1:1 from prototype ATTACKS[].
        public static readonly IReadOnlyList<AttackPattern> Sequence = new List<AttackPattern>
        {
            new(PatternId.Core,     "중앙 핵폭발", "3 · 원형",  "ring",  "중앙이 폭발합니다. 원형으로 흩어지세요!", 2.15f),
            new(PatternId.Gate,     "쌍벽 압착",   "2 · 일자",  "line",  "좌우가 휩쓸립니다. 일자로 줄이세요!",     2.05f),
            new(PatternId.Quadrant, "사분면 폭격", "4 · 십자가", "cross", "십자 통로만 안전합니다!",               2.0f),
            new(PatternId.Shell,    "외곽 소거",   "1 · 밀집",  "dense", "바깥부터 폭발합니다. 중심으로 모이세요!", 2.1f),
            new(PatternId.Core,     "중앙 핵폭발", "3 · 원형",  "ring",  "중앙이 폭발합니다. 원형으로 흩어지세요!", 1.85f),
            new(PatternId.Quadrant, "사분면 폭격", "4 · 십자가", "cross", "십자 통로만 안전합니다!",               1.8f),
            new(PatternId.Gate,     "쌍벽 압착",   "2 · 일자",  "line",  "좌우가 휩쓸립니다. 일자로 줄이세요!",     1.8f),
            new(PatternId.Shell,    "외곽 소거",   "1 · 밀집",  "dense", "바깥부터 폭발합니다. 중심으로 모이세요!", 1.8f),
        };
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Run: `unity:unity-cli` 스킬의 컴파일/로그 확인 기능으로 에러 0 확인.
Expected: 컴파일 에러 없음.

- [ ] **Step 4: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Core && git commit -m "feat: add game constants and boss attack table (1:1 from prototype)"
```

---

## Task 3: 진형 오프셋 (Formations) — 테스트 우선

**Files:**
- Create: `Assets/Scripts/Core/Formations.cs`
- Test: `Assets/Tests/EditMode/FormationsTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

원본 `slotsFor`: dense=5×5(간격0.53), line=25개 `x=(i%3-1)*0.12, z=(i-12)*0.49`, ring=반경3.7 25개, cross=중심1+4방향×6=25.

```csharp
using NUnit.Framework;
using UnityEngine;
using KiteSquad.Core;

public class FormationsTests
{
    [TestCase("dense")][TestCase("line")][TestCase("ring")][TestCase("cross")]
    public void SlotCount_Is25(string f) => Assert.AreEqual(25, Formations.SlotsFor(f).Length);

    [Test] public void Dense_IsCentered5x5()
    {
        var s = Formations.SlotsFor("dense");
        Assert.AreEqual(new Vector2(0,0), s[12]); // center index (iz=2,ix=2)
        Assert.That(s[0], Is.EqualTo(new Vector2(-1.06f,-1.06f)).Using(V2Cmp(1e-4f)));
    }

    [Test] public void Ring_RadiusIs3_7()
    {
        foreach (var p in Formations.SlotsFor("ring"))
            Assert.That(p.magnitude, Is.EqualTo(3.7f).Within(1e-3f));
    }

    [Test] public void Cross_HasCenterAndArms()
    {
        var s = Formations.SlotsFor("cross");
        Assert.AreEqual(new Vector2(0,0), s[0]);
        Assert.That(s[^1], Is.EqualTo(new Vector2(0, 6*0.61f)).Using(V2Cmp(1e-4f)));
    }

    static System.Collections.Generic.IComparer<Vector2> V2Cmp(float eps) => new Cmp(eps);
    class Cmp : System.Collections.Generic.IComparer<Vector2>
    { float e; public Cmp(float e){this.e=e;} public int Compare(Vector2 a, Vector2 b)
      => (Mathf.Abs(a.x-b.x)<e && Mathf.Abs(a.y-b.y)<e) ? 0 : 1; }
}
```

- [ ] **Step 2: 실패 확인**

Run: `unity:unity-cli` 스킬의 EditMode 테스트 실행(`unity run-tests --mode EditMode --project main/KiteSquad` 류 — 정확 커맨드는 스킬 참조).
Expected: FAIL (`Formations` 미정의).

- [ ] **Step 3: 구현(원본 slotsFor 포팅)**

오프셋은 `Vector2(x, z)`로 반환(x→Unity x, y필드→Unity z).

```csharp
using UnityEngine;
namespace KiteSquad.Core
{
    public static class Formations
    {
        public static Vector2[] SlotsFor(string type)
        {
            var outp = new System.Collections.Generic.List<Vector2>(25);
            switch (type)
            {
                case "dense":
                    for (int iz = 0; iz < 5; iz++)
                        for (int ix = 0; ix < 5; ix++)
                            outp.Add(new Vector2((ix-2)*0.53f, (iz-2)*0.53f));
                    break;
                case "line":
                    for (int i = 0; i < 25; i++)
                        outp.Add(new Vector2((i%3-1)*0.12f, (i-12)*0.49f));
                    break;
                case "ring":
                    for (int i = 0; i < 25; i++)
                    { float a = Mathf.PI*2f*i/25f; outp.Add(new Vector2(Mathf.Cos(a)*3.7f, Mathf.Sin(a)*3.7f)); }
                    break;
                case "cross":
                    outp.Add(Vector2.zero);
                    for (int i = 1; i <= 6; i++)
                    { outp.Add(new Vector2(-i*0.61f,0)); outp.Add(new Vector2(i*0.61f,0));
                      outp.Add(new Vector2(0,-i*0.61f)); outp.Add(new Vector2(0,i*0.61f)); }
                    break;
            }
            return outp.ToArray();
        }
    }
}
```

> 주의: cross는 중심1 + 6×4=24 = 25. 인덱스 순서가 원본과 동일(`[-x,+x,-z,+z]` 반복)해야 유닛 슬롯 배분이 일관된다.

- [ ] **Step 4: 통과 확인**

Run: EditMode 테스트.
Expected: PASS (4 케이스 + 4 테스트).

- [ ] **Step 5: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Core/Formations.cs KiteSquad/Assets/Tests/EditMode/FormationsTests.cs && git commit -m "feat: add formation slot offsets with tests"
```

---

## Task 4: 장판 판정 (DangerZones) — 테스트 우선

**Files:**
- Create: `Assets/Scripts/Core/DangerZones.cs`
- Test: `Assets/Tests/EditMode/DangerZonesTests.cs`

- [ ] **Step 1: 실패 테스트(원본 inDanger 진리표)**

원본: core `r<3.10`; gate `|dx|>0.83 && |dx|<6.4 && |dz|<7.1`; quadrant `|dx|>0.56 && |dz|>0.56 && |dx|<5.7 && |dz|<5.7`; shell `r>1.73 && r<6.05`. (dx=x-cx, dz=z-cz)

```csharp
using NUnit.Framework;
using KiteSquad.Core;

public class DangerZonesTests
{
    [Test] public void Core_InsideRadiusIsDanger()
    {
        Assert.IsTrue(DangerZones.InDanger(PatternId.Core, 0,0, 2.0f,0));
        Assert.IsFalse(DangerZones.InDanger(PatternId.Core, 0,0, 3.2f,0));
    }
    [Test] public void Gate_CenterCorridorIsSafe()
    {
        Assert.IsFalse(DangerZones.InDanger(PatternId.Gate, 0,0, 0.5f,0));   // |dx|<0.83 safe
        Assert.IsTrue (DangerZones.InDanger(PatternId.Gate, 0,0, 2.0f,0));
    }
    [Test] public void Quadrant_CrossCorridorSafe()
    {
        Assert.IsFalse(DangerZones.InDanger(PatternId.Quadrant, 0,0, 0.0f, 3.0f)); // on axis safe
        Assert.IsTrue (DangerZones.InDanger(PatternId.Quadrant, 0,0, 3.0f, 3.0f)); // diagonal danger
    }
    [Test] public void Shell_CenterSafeOuterDanger()
    {
        Assert.IsFalse(DangerZones.InDanger(PatternId.Shell, 0,0, 1.0f,0)); // r<1.73 safe
        Assert.IsTrue (DangerZones.InDanger(PatternId.Shell, 0,0, 4.0f,0));
        Assert.IsFalse(DangerZones.InDanger(PatternId.Shell, 0,0, 7.0f,0)); // r>6.05 safe
    }
}
```

- [ ] **Step 2: 실패 확인** — Run EditMode 테스트. Expected: FAIL (`DangerZones` 미정의).

- [ ] **Step 3: 구현(원본 포팅)**

```csharp
using UnityEngine;
namespace KiteSquad.Core
{
    public static class DangerZones
    {
        public static bool InDanger(PatternId id, float cx, float cz, float x, float z)
        {
            float dx = x - cx, dz = z - cz, r = Mathf.Sqrt(dx*dx + dz*dz);
            switch (id)
            {
                case PatternId.Core:     return r < 3.10f;
                case PatternId.Gate:     return Mathf.Abs(dx) > 0.83f && Mathf.Abs(dx) < 6.4f && Mathf.Abs(dz) < 7.1f;
                case PatternId.Quadrant: return Mathf.Abs(dx) > 0.56f && Mathf.Abs(dz) > 0.56f && Mathf.Abs(dx) < 5.7f && Mathf.Abs(dz) < 5.7f;
                case PatternId.Shell:    return r > 1.73f && r < 6.05f;
                default: return false;
            }
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run EditMode 테스트. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Core/DangerZones.cs KiteSquad/Assets/Tests/EditMode/DangerZonesTests.cs && git commit -m "feat: add danger-zone hit tests for 4 boss patterns"
```

---

## Task 5: 분대 이동 — 중심점 + 유닛 추종

**Files:**
- Create: `Assets/Scripts/Squad/FormationController.cs`
- Create: `Assets/Scripts/Squad/SquadUnit.cs`
- Test: `Assets/Tests/PlayMode/SquadMovementTests.cs`

- [ ] **Step 1: 실패 PlayMode 테스트**

목적지를 주면 중심이 그쪽으로 이동하고, 유닛들이 `중심+슬롯` 근처로 수렴한다.

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KiteSquad.Squad;

public class SquadMovementTests
{
    [UnityTest] public IEnumerator Anchor_MovesTowardDestination()
    {
        var go = new GameObject("fc"); var fc = go.AddComponent<FormationController>();
        fc.Init(); fc.SetFormation("dense", silent:true);
        fc.SetDestination(new Vector2(5f, 2f));
        float t = 0; while (t < 2f) { fc.Tick(0.016f); t += 0.016f; yield return null; }
        Assert.That(Vector2.Distance(fc.Anchor, new Vector2(5f,2f)), Is.LessThan(0.2f));
    }

    [UnityTest] public IEnumerator Units_ConvergeToSlots()
    {
        var go = new GameObject("fc"); var fc = go.AddComponent<FormationController>();
        fc.Init(); fc.SetFormation("dense", silent:true);
        float t = 0; while (t < 2f) { fc.Tick(0.016f); t += 0.016f; yield return null; }
        var u0 = fc.Units[0];
        Vector2 want = fc.Anchor + u0.Slot;
        Assert.That(Vector2.Distance(new Vector2(u0.X,u0.Z), want), Is.LessThan(0.1f));
    }
}
```

- [ ] **Step 2: 실패 확인** — Run PlayMode 테스트. Expected: FAIL (타입 미정의).

- [ ] **Step 3: SquadUnit 구현(데이터+이동, 원본 update 포팅)**

중심 이동: `d>0.024`면 `min(d, 7.4*dt)`. 유닛: 목표 `clamp(anchor+slot, LIMIT)`, 거리>0.015면 `min(dist, (9.7+min(9,dist*2.4))*dt)`. (원본과 동일. SmoothDamp은 스펙의 대안이나 M1은 원본 수식 유지해 1:1 보장.)

```csharp
using UnityEngine;
using KiteSquad.Core;
namespace KiteSquad.Squad
{
    public class SquadUnit
    {
        public int Id; public float X, Z; public Vector2 Slot;
        public float Hp = GameConstants.UnitMax; public bool Dead;
        public float Cd, Flash, Anim; public int ShotCount;

        public void MoveToward(Vector2 anchor, float dt)
        {
            if (Dead) return;
            float tx = Mathf.Clamp(anchor.x + Slot.x, GameConstants.LimitXMin, GameConstants.LimitXMax);
            float tz = Mathf.Clamp(anchor.y + Slot.y, GameConstants.LimitZMin, GameConstants.LimitZMax);
            float ux = tx - X, uz = tz - Z, dist = Mathf.Sqrt(ux*ux + uz*uz);
            if (dist > GameConstants.UnitArrive)
            {
                float speed = GameConstants.UnitBaseSpeed + Mathf.Min(GameConstants.UnitSpeedDistCap, dist*GameConstants.UnitSpeedDistGain);
                float step = Mathf.Min(dist, speed*dt);
                X += ux/dist*step; Z += uz/dist*step;
            }
            Anim += dt*5f; Flash = Mathf.Max(0, Flash - dt*3f); Cd -= dt;
        }
    }
}
```

- [ ] **Step 4: FormationController 구현**

중심 이동 + 25유닛 생성/슬롯 배분 + 틱. `Init()`은 원본 reset의 유닛 배치(시작 `z=3.4` dense). `Tick(dt)`는 anchor 이동 후 각 유닛 `MoveToward`. (MonoBehaviour지만 로직은 `Tick(dt)`로 분리해 테스트가 수동 구동.)

```csharp
using UnityEngine;
using System.Collections.Generic;
using KiteSquad.Core;
namespace KiteSquad.Squad
{
    public class FormationController : MonoBehaviour
    {
        public Vector2 Anchor = new(0, 3.4f);
        public Vector2 Destination = new(0, 3.4f);
        public string Formation = "dense";
        public readonly List<SquadUnit> Units = new();

        public void Init()
        {
            Units.Clear();
            var slots = Formations.SlotsFor("dense");
            for (int i = 0; i < GameConstants.SquadSize; i++)
                Units.Add(new SquadUnit { Id = i, Slot = slots[i], X = slots[i].x, Z = 3.4f + slots[i].y,
                                          Cd = Random.value*0.65f, Anim = Random.value*6.28f });
        }

        public void SetFormation(string id, bool silent = false)
        {
            Formation = id; var slots = Formations.SlotsFor(id);
            for (int i = 0; i < Units.Count; i++) Units[i].Slot = slots[i];
            // silent/toast/vfx handled by caller in play mode
        }

        public void SetDestination(Vector2 d)
        {
            Destination = new Vector2(
                Mathf.Clamp(d.x, -GameConstants.MoveClampX, GameConstants.MoveClampX),
                Mathf.Clamp(d.y, GameConstants.MoveClampZMin, GameConstants.MoveClampZMax));
        }

        public void Tick(float dt)
        {
            Vector2 diff = Destination - Anchor; float dd = diff.magnitude;
            if (dd > GameConstants.AnchorArrive)
            { float step = Mathf.Min(dd, GameConstants.AnchorSpeed*dt); Anchor += diff/dd*step; }
            foreach (var u in Units) u.MoveToward(Anchor, dt);
        }
    }
}
```

- [ ] **Step 5: 통과 확인** — Run PlayMode 테스트. Expected: PASS.

- [ ] **Step 6: 유닛 비주얼(임시 도형) 바인딩**

각 `SquadUnit`에 작은 Capsule/Cylinder 프리미티브를 대응시키는 `SquadView`(MonoBehaviour)를 만들어 매 프레임 `transform.position = new Vector3(u.X, 0, u.Z)`. 색: 청록 머티리얼. (렌더만, 로직 아님.)

- [ ] **Step 7: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Squad KiteSquad/Assets/Tests/PlayMode/SquadMovementTests.cs && git commit -m "feat: squad formation movement (anchor + unit follow) with playmode tests"
```

---

## Task 6: 입력 — 우클릭 이동 / 진형 / 일시정지 / 재시작

**Files:**
- Create: `Assets/Scripts/Input/SquadInputController.cs`

- [ ] **Step 1: 구현(원본 이벤트 포팅, 신규 Input System 또는 레거시)**

우클릭/드래그 → 카메라 레이를 y=0 평면과 교차 → `FormationController.SetDestination(new Vector2(hit.x, hit.z))`. 키 `1/q→dense,2/w→line,3/e→ring,4/r→cross`, `p→pause`, `enter→restart`, `space→(예약)`. 우클릭 컨텍스트 메뉴 방지는 Unity에선 불필요.

```csharp
using UnityEngine;
using KiteSquad.Squad; using KiteSquad.Game;
namespace KiteSquad.Input
{
    public class SquadInputController : MonoBehaviour
    {
        public Camera Cam; public FormationController Squad; public GameDirector Director;
        static readonly Plane Ground = new(Vector3.up, Vector3.zero);

        void Update()
        {
            if (Director.Phase == Phase.Play && !Director.Paused)
            {
                // RMB held = move (click or drag), mirrors prototype draggingRight
                if (UnityEngine.Input.GetMouseButton(1))
                {
                    var ray = Cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
                    if (Ground.Raycast(ray, out float e)) { var p = ray.GetPoint(e); Squad.SetDestination(new Vector2(p.x, p.z)); }
                }
                HandleFormationKeys();
                if (Key(KeyCode.P)) Director.TogglePause();
            }
            if (Key(KeyCode.Return) && Director.Phase is Phase.Menu or Phase.Win or Phase.Lose) Director.Restart();
        }

        void HandleFormationKeys()
        {
            if (Key(KeyCode.Alpha1)||Key(KeyCode.Q)) Director.ChangeFormation("dense");
            if (Key(KeyCode.Alpha2)||Key(KeyCode.W)) Director.ChangeFormation("line");
            if (Key(KeyCode.Alpha3)||Key(KeyCode.E)) Director.ChangeFormation("ring");
            if (Key(KeyCode.Alpha4)||Key(KeyCode.R)) Director.ChangeFormation("cross");
        }
        static bool Key(KeyCode k) => UnityEngine.Input.GetKeyDown(k);
    }
}
```

> 프로젝트가 新 Input System 전용이면 `UnityEngine.Input` 대신 `Mouse.current`/`Keyboard.current`로 바꾼다(Task 0 템플릿 설정에 맞춤, unity-cli로 확인).

- [ ] **Step 2: 수동 검증** — PlayMode에서 우클릭 이동·1~4 진형 전환이 되는지 확인(스크린샷/플레이).

- [ ] **Step 3: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Input && git commit -m "feat: squad input (rmb move, formation keys, pause, restart)"
```

---

## Task 7: 자동 평타 + 투사체 + 보스 HP

**Files:**
- Create: `Assets/Scripts/Boss/Boss.cs`
- Create: `Assets/Scripts/Squad/ShotSystem.cs`
- Test: `Assets/Tests/PlayMode/CombatTests.cs`

- [ ] **Step 1: 실패 테스트(보스가 시간 경과로 피해 입음)**

```csharp
using System.Collections; using NUnit.Framework; using UnityEngine; using UnityEngine.TestTools;
using KiteSquad.Squad; using KiteSquad.Boss; using KiteSquad.Core;

public class CombatTests
{
    [UnityTest] public IEnumerator Boss_TakesDamageOverTime()
    {
        var fcGo = new GameObject("fc"); var fc = fcGo.AddComponent<FormationController>();
        fc.Init(); fc.Anchor = new Vector2(0, 3.4f);
        var boss = new Boss();
        var shots = new ShotSystem();
        float start = boss.Hp, t = 0;
        while (t < 3f) { fc.Tick(0.016f); shots.Tick(fc.Units, boss, 0.016f); t += 0.016f; yield return null; }
        Assert.That(boss.Hp, Is.LessThan(start));
    }
}
```

- [ ] **Step 2: 실패 확인** — Run PlayMode. Expected: FAIL.

- [ ] **Step 3: Boss 구현**

```csharp
using KiteSquad.Core;
namespace KiteSquad.Boss
{
    public class Boss
    {
        public float Hp = GameConstants.BossMax; public int Phase = 1; public float Flash;
        public float HpFrac => Hp / GameConstants.BossMax;
    }
}
```

- [ ] **Step 4: ShotSystem 구현(원본 발사/명중 포팅)**

유닛 평타: 보스 거리<15.9 & cd<=0 → 쿨 `0.76+rand*0.16`, 투사체 생성(보스 방향, 속도22). 투사체: 보스 반경1.15 명중 시 33 피해, totalDamage·hitEvents 기록. 반환으로 명중 여부/피해를 Director에 전달(DPS·플래시용).

```csharp
using System.Collections.Generic; using UnityEngine; using KiteSquad.Core; using KiteSquad.Squad;
namespace KiteSquad.Boss { /* placed here for brevity; or KiteSquad.Squad */ }
namespace KiteSquad.Squad
{
    public struct Shot { public float X, Z, Vx, Vz, Life; public int Owner; }
    public class ShotSystem
    {
        public readonly List<Shot> Shots = new();
        public System.Action<float> OnBossHit; // damage dealt

        public void Tick(IList<SquadUnit> units, KiteSquad.Boss.Boss boss, float dt)
        {
            float bx = GameConstants.BossX, bz = GameConstants.BossZ;
            foreach (var u in units)
            {
                if (u.Dead) continue;
                float db = Mathf.Sqrt((u.X-bx)*(u.X-bx) + (u.Z-bz)*(u.Z-bz));
                if (db < GameConstants.FireRange && u.Cd <= 0)
                {
                    u.Cd = GameConstants.UnitCdMin + Random.value*GameConstants.UnitCdRand;
                    float ang = Mathf.Atan2(bz-u.Z, bx-u.X);
                    Shots.Add(new Shot { X = u.X+Mathf.Cos(ang)*0.29f, Z = u.Z+Mathf.Sin(ang)*0.29f,
                                         Vx = Mathf.Cos(ang)*GameConstants.ShotSpeed, Vz = Mathf.Sin(ang)*GameConstants.ShotSpeed,
                                         Life = GameConstants.ShotLife, Owner = u.Id });
                    u.ShotCount++;
                }
            }
            for (int i = Shots.Count-1; i >= 0; i--)
            {
                var b = Shots[i]; b.X += b.Vx*dt; b.Z += b.Vz*dt; b.Life -= dt;
                if (Mathf.Sqrt((b.X-bx)*(b.X-bx)+(b.Z-bz)*(b.Z-bz)) < GameConstants.ShotHitRadius)
                {
                    boss.Hp = Mathf.Max(0, boss.Hp - GameConstants.UnitDamage); boss.Flash = 0.8f;
                    OnBossHit?.Invoke(GameConstants.UnitDamage); Shots.RemoveAt(i);
                }
                else if (b.Life <= 0 || Mathf.Abs(b.X) > 15 || Mathf.Abs(b.Z) > 13) Shots.RemoveAt(i);
                else Shots[i] = b;
            }
        }
    }
}
```

- [ ] **Step 5: 통과 확인** — Run PlayMode. Expected: PASS.

- [ ] **Step 6: 보스/투사체 비주얼(임시)** — 보스 큰 Cylinder(붉은 Emissive), 투사체 작은 Sphere/TrailRenderer.

- [ ] **Step 7: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Boss/Boss.cs KiteSquad/Assets/Scripts/Squad/ShotSystem.cs KiteSquad/Assets/Tests/PlayMode/CombatTests.cs && git commit -m "feat: auto-attack shots and boss hp with playmode test"
```

---

## Task 8: 보스 패턴 시스템 (스케줄·텔레그래프·resolve)

**Files:**
- Create: `Assets/Scripts/Boss/BossAttackSystem.cs`
- Create: `Assets/Scripts/Boss/TelegraphView.cs`
- Test: `Assets/Tests/PlayMode/PatternResolveTests.cs`

- [ ] **Step 1: 실패 테스트(장판 안 유닛만 피해)**

core 패턴 resolve 시 중심 반경<3.10 유닛이 55 피해.

```csharp
using System.Collections; using NUnit.Framework; using UnityEngine; using UnityEngine.TestTools;
using KiteSquad.Squad; using KiteSquad.Boss; using KiteSquad.Core;

public class PatternResolveTests
{
    [UnityTest] public IEnumerator Core_DamagesUnitsInRadius()
    {
        var fcGo = new GameObject("fc"); var fc = fcGo.AddComponent<FormationController>();
        fc.Init(); fc.Anchor = Vector2.zero;
        foreach (var u in fc.Units) { u.X = 0; u.Z = 0; } // all at center → all in core danger
        var sys = new BossAttackSystem();
        sys.ForceResolve(PatternId.Core, 0, 0, fc.Units);
        Assert.That(fc.Units[0].Hp, Is.EqualTo(GameConstants.UnitMax - GameConstants.PatternUnitDamage).Within(0.01f));
        yield break;
    }
}
```

- [ ] **Step 2: 실패 확인** — Run PlayMode. Expected: FAIL.

- [ ] **Step 3: BossAttackSystem 구현**

상태: 현재 패턴/elapsed/remaining, nextAttack 타이머, attackIndex. `Tick`에서 패턴 없으면 nextAttack 감소→StartPattern, 있으면 remaining 감소→막판 0.38s 전까지 장판 중심이 anchor 추종(lerp 11), remaining<=0이면 Resolve. `Resolve`: 각 유닛 `DangerZones.InDanger`면 55 피해·사망 처리, 결과(피격/사망 수) 반환. nextAttack = HpFrac<0.55 ? 1.45 : 1.9. `ForceResolve`는 테스트/디버그용.

```csharp
using System.Collections.Generic; using UnityEngine; using KiteSquad.Core; using KiteSquad.Squad;
namespace KiteSquad.Boss
{
    public class BossAttackSystem
    {
        public struct Active { public AttackPattern P; public float Cx, Cz, Elapsed, Remaining; }
        public Active? Current; public int Index; public float NextAttack = GameConstants.FirstAttackDelay;

        public System.Action<AttackPattern> OnStart;
        public System.Action<int,int> OnResolved; // hitCount, lost

        public void Tick(FormationController squad, Boss boss, float dt)
        {
            if (Current is Active a)
            {
                a.Elapsed += dt; a.Remaining = Mathf.Max(0, a.P.Time - a.Elapsed);
                if (a.Remaining > GameConstants.CastTrackLockTime)
                { a.Cx = Mathf.Lerp(a.Cx, squad.Anchor.x, Mathf.Min(1, dt*11)); a.Cz = Mathf.Lerp(a.Cz, squad.Anchor.y, Mathf.Min(1, dt*11)); }
                Current = a;
                if (a.Remaining <= 0) Resolve(a.P.Id, a.Cx, a.Cz, squad.Units, boss);
            }
            else { NextAttack -= dt; if (NextAttack <= 0) StartPattern(squad, boss); }
        }

        void StartPattern(FormationController squad, Boss boss)
        {
            var p = AttackTable.Sequence[Index % AttackTable.Sequence.Count]; Index++;
            Current = new Active { P = p, Cx = squad.Anchor.x, Cz = squad.Anchor.y, Elapsed = 0, Remaining = p.Time };
            OnStart?.Invoke(p);
        }

        public void ForceResolve(PatternId id, float cx, float cz, IList<SquadUnit> units)
            => Resolve(id, cx, cz, units, null);

        void Resolve(PatternId id, float cx, float cz, IList<SquadUnit> units, Boss boss)
        {
            int hit = 0, lost = 0;
            foreach (var u in units)
            {
                if (u.Dead) continue;
                if (DangerZones.InDanger(id, cx, cz, u.X, u.Z))
                { hit++; u.Hp -= GameConstants.PatternUnitDamage; u.Flash = 1;
                  if (u.Hp <= 0) { u.Dead = true; lost++; } }
            }
            Current = null;
            NextAttack = (boss != null && boss.HpFrac < 0.55f) ? GameConstants.NextAttackPhase2 : GameConstants.NextAttackPhase1;
            OnResolved?.Invoke(hit, lost);
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — Run PlayMode. Expected: PASS.

- [ ] **Step 5: TelegraphView(임시 시각화)**

현재 패턴의 안전/위험 영역을 지면 위 반투명 메시로 표시: core=원, gate=좌우 사각, quadrant=4모서리 사각, shell=도넛. M1은 간단히 반투명 Quad/절차 메시 + 붉은/청록 색. 중심은 `Current.Cx,Cz` 추종. 카운트다운은 Task 9 HUD에서.

- [ ] **Step 6: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Boss KiteSquad/Assets/Tests/PlayMode/PatternResolveTests.cs && git commit -m "feat: boss attack scheduling, telegraph tracking, and pattern resolve"
```

---

## Task 9: GameDirector (상태머신·페이즈·DPS·승패)

**Files:**
- Create: `Assets/Scripts/Game/GameDirector.cs`
- Create: `Assets/Scripts/Core/Combat.cs`
- Test: `Assets/Tests/EditMode/CombatMathTests.cs`

- [ ] **Step 1: 실패 테스트(DPS 윈도 & 페이즈 임계)**

```csharp
using NUnit.Framework; using System.Collections.Generic; using KiteSquad.Core;
public class CombatMathTests
{
    [Test] public void Dps_IsSumOverWindow()
    {
        var ev = new List<(float t,float d)> { (0.0f,33), (1.0f,33), (2.0f,33) };
        Assert.That(Combat.Dps(ev, now:2.0f), Is.EqualTo(99f/GameConstants.DpsWindow).Within(0.01f));
    }
    [Test] public void Phase2_TriggersBelow54Percent()
    {
        Assert.IsFalse(Combat.IsPhase2(0.55f));
        Assert.IsTrue (Combat.IsPhase2(0.53f));
    }
}
```

- [ ] **Step 2: 실패 확인** — Run EditMode. Expected: FAIL.

- [ ] **Step 3: Combat 구현**

```csharp
using System.Collections.Generic;
namespace KiteSquad.Core
{
    public static class Combat
    {
        public static float Dps(IList<(float t, float d)> events, float now)
        {
            float sum = 0; foreach (var e in events) if (now - e.t < GameConstants.DpsWindow) sum += e.d;
            return sum / GameConstants.DpsWindow;
        }
        public static bool IsPhase2(float hpFrac) => hpFrac < GameConstants.Phase2HpFrac;
    }
}
```

- [ ] **Step 4: 통과 확인** — Run EditMode. Expected: PASS.

- [ ] **Step 5: GameDirector 구현(원본 game 상태 + update 루프 결선)**

`Phase {Menu,Play,Win,Lose}`. 필드: elapsed, paused, hitEvents, lastDps, bossPhase. `Update(dt)`에서 play일 때: squad.Tick → shots.Tick(+hitEvent 기록) → attack.Tick → DPS 갱신 → 페이즈 전환 체크(HpFrac<0.54 & phase1→phase2) → 전멸 시 `Lose`, 보스 0 시 `Win`. `ChangeFormation/TogglePause/Restart/Begin`. hitEvent는 shots.OnBossHit에서 `(elapsed, dmg)` 추가, 2.4s 지난 건 제거. 시스템 인스턴스(FormationController, ShotSystem, BossAttackSystem, Boss)를 보유·구동.

```csharp
using System.Collections.Generic; using UnityEngine;
using KiteSquad.Core; using KiteSquad.Squad; using KiteSquad.Boss;
namespace KiteSquad.Game
{
    public enum Phase { Menu, Play, Win, Lose }
    public class GameDirector : MonoBehaviour
    {
        public FormationController Squad;           // assigned in scene
        public Phase Phase = Phase.Menu; public bool Paused;
        public float Elapsed, LastDps; public int BossPhase = 1;
        public Boss Boss { get; private set; }
        public BossAttackSystem Attacks { get; private set; }
        public ShotSystem Shots { get; private set; }
        readonly List<(float t, float d)> _hits = new();
        public float TotalDamage;

        public void Restart()
        {
            Boss = new Boss(); Attacks = new BossAttackSystem(); Shots = new ShotSystem();
            _hits.Clear(); TotalDamage = 0; Elapsed = 0; BossPhase = 1; Paused = false;
            Shots.OnBossHit += d => { _hits.Add((Elapsed, d)); TotalDamage += d; };
            Squad.Init(); Squad.SetFormation("dense", true);
            Squad.Anchor = new Vector2(0, 3.4f); Squad.Destination = Squad.Anchor;
            Phase = Phase.Play;
        }

        public void TogglePause() { if (Phase == Phase.Play) Paused = !Paused; }
        public void ChangeFormation(string id) { if (Phase == Phase.Play) Squad.SetFormation(id); }

        void Update()
        {
            if (Phase != Phase.Play || Paused) return;
            float dt = Mathf.Min(0.042f, Time.deltaTime); Elapsed += dt;
            Squad.Tick(dt);
            Shots.Tick(Squad.Units, Boss, dt);
            Attacks.Tick(Squad, Boss, dt);
            _hits.RemoveAll(e => Elapsed - e.t >= GameConstants.DpsWindow);
            LastDps = Combat.Dps(_hits, Elapsed);
            if (Combat.IsPhase2(Boss.HpFrac) && BossPhase == 1) { BossPhase = 2; Boss.Phase = 2; }
            bool allDead = true; foreach (var u in Squad.Units) if (!u.Dead) { allDead = false; break; }
            if (allDead) Phase = Phase.Lose;
            else if (Boss.Hp <= 0) Phase = Phase.Win;
        }
    }
}
```

- [ ] **Step 6: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Game KiteSquad/Assets/Scripts/Core/Combat.cs KiteSquad/Assets/Tests/EditMode/CombatMathTests.cs && git commit -m "feat: game director state machine with dps and phase logic"
```

---

## Task 10: HUD (uGUI)

**Files:**
- Create: `Assets/Scripts/UI/Hud.cs`
- Create: `main/KiteSquad/Assets/Prefabs/HudCanvas.prefab` (또는 씬 내 Canvas)

- [ ] **Step 1: Canvas 구성**

`unity:ui-ugui` 스킬을 따라 Screen Space - Overlay Canvas에 배치: 상단 보스 HP 바(Image fill)+퍼센트+수치(TMP), 생존수 TMP, DPS TMP, 하단 진형 패널(버튼 4개 dense/line/ring/cross, 선택/추천 하이라이트), 중앙 상단 패턴 경고(이름+카운트다운+진행바), 중앙 토스트 TMP, 시작/승패 오버레이 패널(제목/설명/시작 버튼). 원본 HUD 레이아웃·문구 참고.

- [ ] **Step 2: Hud.cs 바인딩**

`GameDirector`를 참조해 매 프레임 갱신: 보스 fill=`Boss.HpFrac`, 퍼센트=`Ceil(frac*100)`, 생존=`Units.Count(!Dead)`(10 미만 붉게), DPS=`Round(LastDps)`. `Attacks.OnStart`→경고 표시(이름/추천 버튼 하이라이트), `Tick` 중 `Current.Remaining`→카운트다운/진행바, `OnResolved(hit,lost)`→토스트("PERFECT DODGE!" 또는 "피격 n명 · 전사 m명"). 진형 버튼 클릭→`Director.ChangeFormation`. 오버레이: Phase Menu/Win/Lose에 표시, 시작 버튼→`Director.Restart`.

- [ ] **Step 3: 수동 검증** — 플레이로 보스바 감소·생존수·DPS·경고·토스트·진형 하이라이트 동작 확인(스크린샷).

- [ ] **Step 4: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/UI KiteSquad/Assets/Prefabs KiteSquad/Assets/Scenes && git commit -m "feat: ugui hud (boss bar, alive, dps, formation panel, warnings, overlay)"
```

---

## Task 11: 연출(임시) — 플래시·파티클·셰이크·링

**Files:**
- Create: `Assets/Scripts/Fx/JuiceFx.cs`

- [ ] **Step 1: 구현**

원본 효과를 임시로: 패턴 resolve/페이즈 전환 시 스크린셰이크(카메라 흔들림, 원본 `shake` 감쇠 `dt*32`), 피격 플래시(화면 붉은 오버레이 Image alpha `flash` 감쇠 `dt*2.7`), 유닛/보스 피격 파티클(URP ParticleSystem 버스트), resolve 확산 링(LineRenderer 원 확대). 수치는 원본 상수 유지. 비주얼만이므로 테스트 없음, 플레이 확인.

- [ ] **Step 2: 수동 검증** — 패턴 명중/페이즈 전환에서 셰이크·플래시·링·파티클 확인.

- [ ] **Step 3: Commit**

```bash
cd main && git add KiteSquad/Assets/Scripts/Fx && git commit -m "feat: temp juice fx (shake, flash, particles, rings)"
```

---

## Task 12: 통합 플레이스루 + 튜닝 패스

**Files:**
- Modify: 필요한 수치 파일(상수와 원본 대조)

- [ ] **Step 1: 전체 EditMode+PlayMode 테스트**

Run: `unity:unity-cli`로 EditMode·PlayMode 전체 실행.
Expected: 전부 PASS.

- [ ] **Step 2: 수동 풀 플레이스루 대조**

메뉴→시작→우클릭 카이팅→4패턴 각각 추천 진형으로 회피→2페이즈 가속→보스 처치(승) 및 전멸(패) 경로 확인. 원본과 체감(속도/사거리/패턴 타이밍) 비교.
Expected: 원본과 동일한 흐름·난이도. 어긋나면 상수만 조정(수치는 GameConstants 단일 출처).

- [ ] **Step 3: decisions 상태 갱신 제안**

M1 완료 시 `docs/decisions.md`의 #2~#7 상태를 "제안됨"→"유효"로 바꾸도록 사용자에게 제안(사람 승인). `docs/memory/milestones.md`와 `people/ansirh/status.md` 갱신 제안.

- [ ] **Step 4: Commit**

```bash
cd main && git add -A && git commit -m "chore: M1 integration pass and tuning against prototype"
```

---

## Self-Review 메모

- **Spec 커버리지:** 프로토타입의 이동/진형/평타/4패턴/2페이즈/DPS감소/승패/HUD/일시정지/재시작/연출 — 각각 Task 5·3/5·7·4&8·9·9·9·10·6·6·11에 매핑. 미구현(대시/어택땅/잡몹)은 M1 범위 밖(decisions #2). formation-spec의 NavMesh/FlowField/적푸시는 M2~M3(PLAN §4).
- **타입 일관성:** `SlotsFor`/`InDanger`/`Dps`/`IsPhase2` 시그니처는 테스트와 구현에서 동일. 좌표는 전부 `(x→x, z→z)` 규약.
- **플레이스홀더:** 순수 로직(Task 2~9)은 완전한 C# 포함. Unity-에디터 의존(씬/카메라/Canvas/프리미티브 바인딩)은 해당 unity 스킬을 따르는 구체 단계로 기술 — 실행 시 설치된 6000.3.28f1 API로 확정.
