# KITE SQUAD 25 — M1 프로토타입 이식 + 조작/데이터 확장 Implementation Plan

> **실행 방법:** 태스크를 위에서부터 하나씩(TDD: 실패 테스트 → 구현 → 통과 → 커밋) 진행한다. 각 스텝은 체크박스(`- [ ]`)로 추적한다. Unity 작업은 `unity:unity-cli`·`unity:unity-package-management`·`unity:ui-ugui`·`unity:urp-postprocessing` 스킬을 해당 태스크에서 따른다.

**Goal:** HTML 프로토타입 `KITE SQUAD 25`의 게임플레이를 Unity 6000.3.28f1 · URP로 수치까지 동일하게 재현하고(임시 기하도형), 그 위에 패턴·진형 데이터화(결정 #8)와 RTS식 공격 명령(결정 #9)을 얹는다.

**Architecture:** 게임플레이는 프로토타입의 (x, z) 월드를 Unity **XZ 평면**에 직접 매핑하고, 2D 투영 수식 대신 **직교 쿼터뷰 카메라**로 렌더링한다. 결정론적 로직(진형 슬롯 생성, 장판 shape 판정, 수치)은 MonoBehaviour와 분리된 순수 C# 평가기로 두고 EditMode 테스트로 고정한다. 이동·평타·패턴·HUD는 PlayMode에서 통합 검증한다. 모든 상수는 원본과 동일.

**데이터 구동(결정 #8):** 보스 패턴과 진형은 하드코딩하지 않고 **ScriptableObject 데이터 에셋**(`PatternDefinition`/`AttackSequence`/`FormationDefinition`)으로 둔다. 코드는 범용 평가기(`DangerShapeEval.InDanger(shape, …)`, `FormationSlots.Generate(def)`)만 갖고, 패턴·진형 추가/수정/밸런싱은 에셋 편집으로 한다. 1:1 수치는 에셋 값 + 평가기 EditMode 테스트로 핀 고정한다.

**조작·전투(결정 #9):** 자동 평타(사거리 내 자동 사격)는 유지하되, 교전 지휘를 **분대 오더 상태머신**(`Move`/`AttackMove`/`AttackTarget`)으로 모델링한다. 적은 **`IEnemy`/`IDamageable` + `EnemyRegistry`** 로 추상화한다(M1은 보스 1개만 등록 → M2 잡몹 드롭인). 입력은 **우클릭 스마트 커맨드**(적 콜라이더 피킹→`AttackTarget`, 지면→`Move`)와 **어택땅 A+좌클릭**(→`AttackMove`). 타겟 획득은 `ShotSystem`이 현재 오더를 참조.

**이동 중 사격 금지(결정 #15):** 유닛은 멈춘 상태에서만 사격한다. 기준은 유닛 개별이고 오더 종류와 무관하다(진형 전환으로 자리를 옮기는 중에도 사격 불가). 사거리가 닿지 않는 유닛은 개별로 접근하지 않고 서 있는다. 판정은 순수 함수 한 곳(`FireRules.CanFire`)에 둔다. 이 문서의 아래 코드 블록과 주석 중 "Move 오더에서도 자동 사격"으로 적힌 부분은 이 결정으로 무효다. 프로토타입과 달라지는 지점이다.

**진형 회전(결정 #16):** 마우스 휠로 분대 중심을 축으로 진형을 돌린다. 휠 아래 = 시계 방향, 휠 위 = 반시계 방향(카메라에서 내려다본 기준, Unity에서는 Y축 양의 회전이 시계 방향). 슬롯 = `FormationSlots.Generate` 결과에 현재 회전각을 적용한 값. 회전으로 자리를 옮기는 유닛은 위 규칙대로 사격하지 않는다. 한 칸당 각도는 값으로 노출한다(초기값 제안 15°).

**카메라·진형 키(결정 #18~#20):** 카메라는 직교가 아니라 원근 쿼터뷰다. 분대를 따라가지 않고 화면 가장자리 스크롤로 움직인다. 진형 전환 키는 1~4가 아니라 Q/W/E/R이다. 아래 Task 1의 직교 카메라 설정과 1~4 키 서술은 이 결정으로 무효다. 현재 HUD는 IMGUI 임시 테스트용이고 Task 10의 uGUI HUD는 그대로 남아 있다.

**분대 사거리(결정 #17):** 유닛 개별 사거리(`FireRange`)와 별도로 진형 원점(anchor) 기준 사거리를 둔다. `AttackMove` 분대는 살아 있는 적이 분대 사거리 안에 들어오면 멈춘다. `AttackTarget` 분대는 지정한 적이 분대 사거리 안에 들어오면 멈춘다. 두 값 모두 재조정 대상이라 상수로 분리해 둔다.

**Tech Stack:** Unity 6000.3.28f1, URP, Unity Test Framework(EditMode/PlayMode), uGUI(HUD), TextMeshPro, unity-cli(헤드리스 빌드/테스트).

**좌표 매핑 규약(전 태스크 공통):** 원본 `world.x → Unity x`, `world.z → Unity z`, 높이 `h → Unity y`. 즉 지면은 XZ 평면(y=0). 원본의 모든 속도/거리/반경 상수를 Unity 월드 유닛으로 그대로 사용한다. 화면→월드 변환(`unproject`)은 카메라 레이를 y=0 평면과 교차시켜 구한다.

**작업 규칙:** 구현은 bsk-main 세션이 이 저장소(로컬 `main/` 폴더 = 저장소 루트)에서 한다. 아래 경로와 git 명령은 모두 **저장소 루트 기준**이다. git push는 사용자가 명시적으로 요청할 때만. 커밋은 각 태스크 끝에서.

**이름 규약:** Unity 프로젝트 폴더 `bone-squad-kiting/`, 런타임 어셈블리 `BoneSquadKiting`(`Assets/Scripts/BoneSquadKiting.asmdef`), 네임스페이스 `BoneSquadKiting.*`. 네임스페이스와 같은 이름의 타입을 만들지 않는다(그래서 보스 네임스페이스는 `BoneSquadKiting.Bosses`, 수치 유틸 클래스는 `CombatMath`). 입력은 **새 Input System 전용**(`activeInputHandler: 1`)이라 `UnityEngine.Input`을 쓰지 않는다.

---

## File Structure

프로젝트 루트: `bone-squad-kiting/`(Unity 프로젝트). 스크립트 루트: `Assets/Scripts/`.

| 파일 | 책임 |
| --- | --- |
| `Assets/Scripts/Core/GameConstants.cs` | 원본 상수 전부(수치 단일 출처) |
| `Assets/Scripts/Core/DangerShape.cs` | 장판 모양 데이터 타입(`Disc`/`Annulus`/`CrossBandsX`/`QuadrantBoxes` + 파라미터) |
| `Assets/Scripts/Core/DangerShapeEval.cs` | `InDanger(shape, cx,cz, x,z)` — shape 범용 판정(순수 함수) |
| `Assets/Scripts/Core/FormationSlots.cs` | `Generate(def)` — 진형 정의→25슬롯 오프셋(순수 함수) |
| `Assets/Scripts/Core/CombatMath.cs` | DPS 윈도 계산·페이즈 임계 등 순수 수치 로직 |
| `Assets/Scripts/Data/PatternDefinition.cs` | ScriptableObject: id·name·time·recommend·advice·detail·DangerShape |
| `Assets/Scripts/Data/FormationDefinition.cs` | ScriptableObject: id·name·hotkey·슬롯 생성 규칙(grid/line/ring/cross+파라미터) |
| `Assets/Scripts/Data/AttackSequence.cs` | ScriptableObject: `PatternDefinition[]` 순서(보스별 패턴 묶음) |
| `Assets/Data/Patterns/*.asset`, `Assets/Data/Formations/*.asset`, `Assets/Data/Sequences/*.asset` | 실제 데이터 에셋(원본 값 1:1) |
| `Assets/Scripts/Combat/IEnemy.cs`, `IDamageable.cs` | 적 추상화(위치·생존·피해). 보스/잡몹 공통 |
| `Assets/Scripts/Combat/EnemyRegistry.cs` | 활성 적 목록·등록/해제·획득 질의(최근접/범위). M1=보스1 |
| `Assets/Scripts/Combat/SquadOrder.cs` | `OrderKind{Move,AttackMove,AttackTarget}` + 대상점/타겟 |
| `Assets/Scripts/Combat/EnemyRef.cs` | 콜라이더가 붙은 적 오브젝트가 자신의 `IEnemy`를 노출하는 브리지(우클릭 피킹용) |
| `Assets/Scripts/Squad/FormationController.cs` | 중심점(anchor) 이동, 진형 전환, 유닛 슬롯 배분, **현재 오더 보유** |
| `Assets/Scripts/Squad/SquadUnit.cs` | 유닛 추종(center+offset), 평타 쿨/발사, hp, 사망 |
| `Assets/Scripts/Squad/ShotSystem.cs` | 투사체 풀·이동·명중·피해 + **오더 기반 타겟 획득** |
| `Assets/Scripts/Boss/Boss.cs` | 보스 hp/phase/flash 상태 (`IEnemy`,`IDamageable` 구현) |
| `Assets/Scripts/Boss/BossAttackSystem.cs` | `AttackSequence` 소비·스케줄·텔레그래프·resolve(shape 판정→유닛 피해) |
| `Assets/Scripts/Boss/BossView.cs` | 보스 비주얼(콜라이더+`EnemyRef`), `Bind(Boss)`로 상태 연결 |
| `Assets/Scripts/Boss/TelegraphView.cs` | 장판 시각화(임시 반투명 메시/디캘, DangerShape 기반) |
| `Assets/Scripts/Game/GameDirector.cs` | 상태머신(menu/play/win/lose), 일시정지, 승패, 시스템 오케스트레이션 |
| `Assets/Scripts/Input/SquadInputController.cs` | 우클릭 스마트 커맨드(이동/타겟), **A+좌클릭 어택땅**, 1~4 진형, P 일시정지, Enter 재시작 |
| `Assets/Scripts/UI/Hud.cs` | 보스바·생존·DPS·진형패널·경고·토스트·오버레이 바인딩 |
| `Assets/Scripts/Fx/JuiceFx.cs` | 플래시·파티클·스크린셰이크·링(임시) |
| `Assets/Tests/EditMode/*` | 순수 로직 테스트 |
| `Assets/Tests/PlayMode/*` | 통합 테스트 |

---

## Task 0: Unity 프로젝트 스캐폴딩

> **구현 메모:** EditMode 어셈블리는 `Assets/Tests/EditMode/BoneSquadKiting.Tests.EditMode.asmdef`로 만들었다. PlayMode 어셈블리는 첫 PlayMode 테스트가 생기는 Task 5에서 만든다(스크립트 없는 asmdef는 경고를 낸다).

> **진행 상태:** Step 1~3은 완료·커밋됨(`cccc396`, `40922f4`). 프로젝트는 `bone-squad-kiting/`에 있고 Unity용 무시 규칙은 `bone-squad-kiting/.gitignore`에 들어 있다. 런타임 asmdef `Assets/Scripts/BoneSquadKiting.asmdef`도 이미 있다. **남은 것은 Step 4.**

**Files:**
- Create: `bone-squad-kiting/` (Unity 프로젝트 전체)
- Create: `bone-squad-kiting/.gitignore` (Unity 무시 규칙)

- [x] **Step 1: Editor 6000.3.28f1 설치 확인**

`unity:unity-cli` 스킬을 따른다. 설치 여부 확인:
Run: `unity editors -i` (설치 목록) → 6000.3.28f1 없으면 `unity install --version 6000.3.28f1 --module windows-il2cpp`
Expected: 목록에 `6000.3.28f1`.

- [x] **Step 2: URP 템플릿으로 프로젝트 생성**

Run: `unity create --path "bone-squad-kiting" --version 6000.3.28f1 --template urp` (정확한 플래그는 unity-cli 스킬의 `create` 섹션 확인)
Expected: `bone-squad-kiting/Assets`, `ProjectSettings/ProjectVersion.txt`에 `6000.3.28f1`.

- [x] **Step 3: Unity용 .gitignore 추가**

GitHub `Unity.gitignore` 표준 규칙을 `bone-squad-kiting/.gitignore`에 둔다(프로젝트 폴더 안에 있으므로 경로 접두사 불필요).

- [x] **Step 4: 테스트 프레임워크 어셈블리 준비**

`Assets/Tests/EditMode/EditMode.asmdef`(참조: `BoneSquadKiting` + `UnityEngine.TestRunner`,`UnityEditor.TestRunner`, 플랫폼 Editor), `Assets/Tests/PlayMode/PlayMode.asmdef`(참조: `BoneSquadKiting` + `UnityEngine.TestRunner`) 생성. 기존 `Assets/Scripts/BoneSquadKiting.asmdef`의 `references`가 비어 있으므로 **`Unity.InputSystem`**(Task 6)과 **`Unity.TextMeshPro`**(Task 10)를 추가한다. (ui-ugui/unity-cli 스킬에 asmdef 예시 있음.)

- [x] **Step 5: Commit** (스캐폴딩 자체는 커밋됨. Step 4의 asmdef만 커밋)

```bash
git add bone-squad-kiting/Assets/Tests bone-squad-kiting/Assets/Scripts/BoneSquadKiting.asmdef && git commit -m "chore: add test assemblies and runtime asmdef references"
```

---

## Task 1: 아레나 씬 + 쿼터뷰 직교 카메라 + URP 포스트

> **구현 메모(2026-11-18):** 씬은 `Assets/Editor/ArenaSceneBuilder.cs`(메뉴 `BoneSquadKiting/Rebuild Arena Scene`)가 통째로 생성한다 — 다시 돌리면 씬을 덮어쓰므로 씬을 손으로 고치기 시작하면 이 도구는 쓰지 않는다. 카메라는 +z 쪽에서 보스(−z)를 바라보게 Y 180° 돌려 보스가 화면 위에 온다(월드 +x가 화면 왼쪽). 직교 크기 9. 분대·보스·투사체·장판 비주얼과 게임 컴포넌트 결선까지 이 씬에 들어 있고, `ArenaSceneSmokeTests`가 실제 씬을 플레이시켜 `Logs/Screenshots/`에 스크린샷을 남긴다.

**Files:**
- Create: `bone-squad-kiting/Assets/Scenes/Arena.unity`
- Create: `bone-squad-kiting/Assets/Settings/ArenaVolume.asset` (URP Volume Profile)

- [x] **Step 1: 아레나 지면 생성**

빈 씬 `Arena`에 Plane을 놓고 XZ 범위 `x∈[-12.3,12.3]`, `z∈[-10.2,10.2]`를 덮도록 스케일(Plane 기본 10x10m이므로 scale ≈ (2.46, 1, 2.04), center 0). 어두운 머티리얼(URP/Lit, base color `#0d2434`).

- [x] **Step 2: 직교 쿼터뷰 카메라**

Main Camera: `Projection = Orthographic`, `Orthographic Size ≈ 11`(아레나 z 반경+여유), rotation `(50, 0, 0)`(쿼터뷰 하향각), position를 아레나 중심이 화면 중앙에 오도록 배치(예: `(0, 18, -15)` 후 사이즈/각도로 조정). 배경색 `#050d17`.
Expected: 플레이 시 아레나 전체가 쿼터뷰로 보임.

- [x] **Step 3: 라이팅 + URP 포스트(Bloom/Vignette)**

`unity:urp-postprocessing` 스킬을 따라 Global Volume + Volume Profile(`ArenaVolume`)에 **Bloom**(threshold~0.9, intensity~0.8)과 **Vignette**(intensity~0.35) 추가. 카메라 `Post Processing` 체크. Directional Light 1개(약한 상단광). 원본의 글로·비네트 분위기 재현.

- [x] **Step 4: 검증(스크린샷)**

`unity:unity-cli` 스킬의 스크린샷/플레이모드 기능으로 씬을 캡처해 쿼터뷰·빛 분위기 확인.
Expected: 어두운 아레나 + 비네트 + (이후 오브젝트에서) 블룸.

- [x] **Step 5: Commit**

```bash
git add bone-squad-kiting/Assets && git commit -m "feat: add arena scene with quarter-view ortho camera and URP post"
```

---

## Task 2: 상수 + 패턴 데이터

> **구현 메모(완료):** 에셋 13개는 `Assets/Editor/DefaultDataBuilder.cs`(메뉴 `BoneSquadKiting/Rebuild Default Data`, 헤드리스 `-executeMethod BoneSquadKiting.EditorTools.DefaultDataBuilder.Build`)로 생성했고, 값은 `Assets/Tests/EditMode/DataAssetsTests.cs`가 고정한다. `PatternDefinition`과 `AttackSequence`는 Unity가 스크립트 파일명과 클래스명을 맞추도록 요구해 파일을 나눴다.

**Files:**
- Exists: `Assets/Scripts/Core/GameConstants.cs` (작성·커밋됨 — 아래 값과 대조만)
- Delete: `Assets/Scripts/Core/AttackPattern.cs` + `.meta` (하드코딩 테이블. 결정 #8의 SO 구조가 대체. `PatternId`는 `DangerShape.cs`로 옮김)
- Create: `Assets/Scripts/Core/DangerShape.cs`
- Create: `Assets/Scripts/Data/PatternDefinition.cs`, `Assets/Scripts/Data/AttackSequence.cs`
- Create: `Assets/Data/Patterns/*.asset`(8개), `Assets/Data/Sequences/BossMain.asset`

- [x] **Step 1: 상수 작성(원본과 1:1)**

```csharp
namespace BoneSquadKiting.Core
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

- [x] **Step 2: 장판 모양 데이터 타입(`Core/DangerShape.cs`)**

장판을 **조립형 모양 데이터**로 표현한다(판정 로직은 Task 4 평가기). 패턴 추가 시 에셋에서 모양·파라미터만 바꾼다. 정말 새 기하가 필요할 때만 `DangerShapeType`에 1종 추가.

```csharp
namespace BoneSquadKiting.Core
{
    public enum PatternId { Core, Gate, Quadrant, Shell }
    public enum DangerShapeType { Disc, Annulus, CrossBandsX, QuadrantBoxes }

    [System.Serializable]
    public struct DangerShape
    {
        public DangerShapeType Type;
        public float RMax;        // Disc: 위험 반경 (r < RMax)
        public float RMin, ROut;  // Annulus: RMin < r < ROut
        public float Inner, Outer, HalfZ; // CrossBandsX: |dx|>Inner && |dx|<Outer && |dz|<HalfZ / QuadrantBoxes: |dx|,|dz| ∈ (Inner,Outer)
        public static DangerShape Disc(float r) => new(){Type=DangerShapeType.Disc, RMax=r};
        public static DangerShape Annulus(float rmin,float rout) => new(){Type=DangerShapeType.Annulus, RMin=rmin, ROut=rout};
        public static DangerShape BandsX(float inner,float outer,float halfZ) => new(){Type=DangerShapeType.CrossBandsX, Inner=inner, Outer=outer, HalfZ=halfZ};
        public static DangerShape Quadrants(float inner,float outer) => new(){Type=DangerShapeType.QuadrantBoxes, Inner=inner, Outer=outer};
    }
}
```

- [x] **Step 3: PatternDefinition / AttackSequence ScriptableObject + 데이터 에셋(원본 1:1)**

```csharp
using UnityEngine; using BoneSquadKiting.Core;
namespace BoneSquadKiting.Data
{
    [CreateAssetMenu(menuName="BoneSquadKiting/Pattern")]
    public class PatternDefinition : ScriptableObject
    {
        public PatternId Id;
        public string DisplayName, Advice, Detail;
        public string Recommend;        // "dense"|"line"|"ring"|"cross"
        public float Time;
        public DangerShape Shape;
    }
    [CreateAssetMenu(menuName="BoneSquadKiting/AttackSequence")]
    public class AttackSequence : ScriptableObject { public PatternDefinition[] Patterns; }
}
```

`Assets/Data/Patterns/`에 **8개 에셋**(원본 `ATTACKS[]` 순서·값 1:1), `Assets/Data/Sequences/BossMain.asset`에 그 순서로 배열. 모양 파라미터는 원본 `inDanger`에서:

| # | Id | name | advice | recommend | time | Shape |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Core | 중앙 핵폭발 | 3 · 원형 | ring | 2.15 | `Disc(3.10)` |
| 1 | Gate | 쌍벽 압착 | 2 · 일자 | line | 2.05 | `BandsX(0.83, 6.4, 7.1)` |
| 2 | Quadrant | 사분면 폭격 | 4 · 십자가 | cross | 2.0 | `Quadrants(0.56, 5.7)` |
| 3 | Shell | 외곽 소거 | 1 · 밀집 | dense | 2.1 | `Annulus(1.73, 6.05)` |
| 4 | Core | 중앙 핵폭발 | 3 · 원형 | ring | 1.85 | `Disc(3.10)` |
| 5 | Quadrant | 사분면 폭격 | 4 · 십자가 | cross | 1.8 | `Quadrants(0.56, 5.7)` |
| 6 | Gate | 쌍벽 압착 | 2 · 일자 | line | 1.8 | `BandsX(0.83, 6.4, 7.1)` |
| 7 | Shell | 외곽 소거 | 1 · 밀집 | dense | 1.8 | `Annulus(1.73, 6.05)` |

> 같은 패턴이 time만 다르게 재등장하므로 **슬롯당 에셋 1개**(총 8개)로 둔다. 모양/문구 중복은 데이터 편집으로 흡수(코드 무변경).
>
> detail 문구(삭제되는 `AttackPattern.cs`에서 옮김): Core "중앙이 폭발합니다. 원형으로 흩어지세요!" · Gate "좌우가 휩쓸립니다. 일자로 줄이세요!" · Quadrant "십자 통로만 안전합니다!" · Shell "바깥부터 폭발합니다. 중심으로 모이세요!"

- [x] **Step 4: 기존 하드코딩 테이블 삭제**

에셋에 값을 다 옮긴 뒤 `Assets/Scripts/Core/AttackPattern.cs`와 `.meta`를 삭제한다(`PatternId` 중복 정의 방지).

- [x] **Step 5: 컴파일 확인**

Run: `unity:unity-cli` 스킬의 컴파일/로그 확인 기능으로 에러 0 확인.
Expected: 컴파일 에러 없음.

- [x] **Step 6: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Core bone-squad-kiting/Assets/Scripts/Data bone-squad-kiting/Assets/Data && git commit -m "feat: replace hardcoded attack table with pattern data assets"
```

---

## Task 3: 진형 (FormationDefinition SO + 슬롯 생성기) — 테스트 우선

**Files:**
- Create: `Assets/Scripts/Core/FormationSlots.cs` (순수 생성기)
- Create: `Assets/Scripts/Data/FormationDefinition.cs` (ScriptableObject)
- Create: `Assets/Data/Formations/{dense,line,ring,cross}.asset`
- Test: `Assets/Tests/EditMode/FormationSlotsTests.cs`

진형을 **데이터(Mode+파라미터)** 로 두고 생성기는 범용. 진형 추가 = 에셋 추가(+필요시 Mode 1종).

- [x] **Step 1: 실패 테스트 작성**

원본값: dense=5×5(간격0.53), line=25개 `x=(i%3-1)*0.12, z=(i-12)*0.49`, ring=반경3.7 25개, cross=중심1+4방향×6=25. 테스트는 `FormationDefinition`을 메모리 생성해 `FormationSlots.Generate`로 검증.

```csharp
using NUnit.Framework; using UnityEngine; using BoneSquadKiting.Core; using BoneSquadKiting.Data;
public class FormationSlotsTests
{
    static FormationDefinition Def(FormationMode m, float p0=0,float p1=0,int c=0)
    { var d = ScriptableObject.CreateInstance<FormationDefinition>(); d.Mode=m; d.Spacing=p0; d.Radius=p1; d.Count=c; return d; }

    [Test] public void Dense_IsCentered5x5()
    {
        var s = FormationSlots.Generate(Def(FormationMode.Grid, p0:0.53f));
        Assert.AreEqual(25, s.Length);
        Assert.AreEqual(new Vector2(0,0), s[12]);
        Assert.That(s[0], Is.EqualTo(new Vector2(-1.06f,-1.06f)).Using(V2Cmp(1e-4f)));
    }
    [Test] public void Line_IsStaggeredColumn()
    {
        var d = Def(FormationMode.Line, p0:0.49f); d.Stagger = 0.12f;
        var s = FormationSlots.Generate(d);
        Assert.AreEqual(25, s.Length);
        Assert.That(s[0],  Is.EqualTo(new Vector2(-0.12f, -5.88f)).Using(V2Cmp(1e-4f)));
        Assert.That(s[13], Is.EqualTo(new Vector2(0f, 0.49f)).Using(V2Cmp(1e-4f)));
    }
    [Test] public void Ring_RadiusIs3_7()
    { foreach (var p in FormationSlots.Generate(Def(FormationMode.Ring, p1:3.7f)))
        Assert.That(p.magnitude, Is.EqualTo(3.7f).Within(1e-3f)); }
    [Test] public void Cross_HasCenterAndArms()
    { var s = FormationSlots.Generate(Def(FormationMode.Cross, p0:0.61f));
      Assert.AreEqual(new Vector2(0,0), s[0]);
      Assert.That(s[^1], Is.EqualTo(new Vector2(0, 6*0.61f)).Using(V2Cmp(1e-4f))); }

    static System.Collections.Generic.IComparer<Vector2> V2Cmp(float eps) => new Cmp(eps);
    class Cmp : System.Collections.Generic.IComparer<Vector2>
    { float e; public Cmp(float e){this.e=e;} public int Compare(Vector2 a, Vector2 b)
      => (Mathf.Abs(a.x-b.x)<e && Mathf.Abs(a.y-b.y)<e) ? 0 : 1; }
}
```

- [x] **Step 2: 실패 확인**

Run: `unity:unity-cli` 스킬의 EditMode 테스트 실행(정확 커맨드는 스킬 참조).
Expected: FAIL (`FormationSlots`/`FormationDefinition` 미정의).

- [x] **Step 3: 구현(SO + 생성기, 원본 slotsFor 포팅)**

```csharp
// Data/FormationDefinition.cs
using UnityEngine; using UnityEngine.InputSystem;
namespace BoneSquadKiting.Data
{
    public enum FormationMode { Grid, Line, Ring, Cross }
    [CreateAssetMenu(menuName="BoneSquadKiting/Formation")]
    public class FormationDefinition : ScriptableObject
    {
        public string Id, DisplayName; public Key HotKey;   // Input System key (Digit1..Digit4)
        public FormationMode Mode;
        public float Spacing;  // Grid 간격 / Cross 암 간격 / Line z간격
        public float Stagger;  // Line x 지그재그 폭
        public float Radius;   // Ring 반경
        public int Count;      // 예약(가변 인원)
    }
}
```
```csharp
// Core/FormationSlots.cs — 오프셋 Vector2(x, z), 25개
using UnityEngine; using System.Collections.Generic; using BoneSquadKiting.Data;
namespace BoneSquadKiting.Core
{
    public static class FormationSlots
    {
        public static Vector2[] Generate(FormationDefinition d)
        {
            var o = new List<Vector2>(25);
            switch (d.Mode)
            {
                case FormationMode.Grid: // dense: 간격 0.53
                    for (int iz=0; iz<5; iz++) for (int ix=0; ix<5; ix++)
                        o.Add(new Vector2((ix-2)*d.Spacing, (iz-2)*d.Spacing)); break;
                case FormationMode.Line:
                    for (int i=0; i<25; i++) o.Add(new Vector2((i%3-1)*d.Stagger, (i-12)*d.Spacing)); break;
                case FormationMode.Ring:
                    for (int i=0; i<25; i++){ float a=Mathf.PI*2f*i/25f; o.Add(new Vector2(Mathf.Cos(a)*d.Radius, Mathf.Sin(a)*d.Radius)); } break;
                case FormationMode.Cross:
                    o.Add(Vector2.zero);
                    for (int i=1; i<=6; i++){ o.Add(new Vector2(-i*d.Spacing,0)); o.Add(new Vector2(i*d.Spacing,0));
                        o.Add(new Vector2(0,-i*d.Spacing)); o.Add(new Vector2(0,i*d.Spacing)); } break;
            }
            return o.ToArray();
        }
    }
}
```
에셋: `dense`(Grid, Spacing 0.53, HotKey Digit1), `line`(Line, Spacing 0.49, Stagger 0.12, HotKey Digit2), `ring`(Ring, Radius 3.7, HotKey Digit3), `cross`(Cross, Spacing 0.61, HotKey Digit4). 슬롯 수 25는 `GameConstants.SquadSize`와 같은 고정값이다(가변 인원은 M1 범위 밖). 런타임은 이 에셋 목록을 `FormationController`/HUD/입력이 공유.

> 주의: cross는 중심1 + 6×4=24 = 25. 인덱스 순서가 원본과 동일(`[-x,+x,-z,+z]` 반복)해야 유닛 슬롯 배분이 일관된다. line/ring/grid 기본값은 원본과 동일하게 에셋에 저장.

- [x] **Step 4: 통과 확인**

Run: EditMode 테스트.
Expected: PASS (4 테스트).

- [x] **Step 5: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Core/FormationSlots.cs bone-squad-kiting/Assets/Scripts/Data/FormationDefinition.cs bone-squad-kiting/Assets/Data/Formations bone-squad-kiting/Assets/Tests/EditMode/FormationSlotsTests.cs && git commit -m "feat: add formation definitions (SO) and slot generator with tests"
```

---

## Task 4: 장판 판정 (DangerShapeEval, shape 범용) — 테스트 우선

**Files:**
- Create: `Assets/Scripts/Core/DangerShapeEval.cs` (`DangerShape.cs`는 Task 2에서 생성)
- Test: `Assets/Tests/EditMode/DangerShapeEvalTests.cs`

패턴 id가 아니라 **DangerShape 데이터**로 판정 → 패턴 추가 시 코드 무변경(에셋의 shape만 지정).

- [x] **Step 1: 실패 테스트(원본 inDanger 진리표 — shape로)**

원본값 그대로: Disc `r<3.10`; BandsX `|dx|>0.83 && |dx|<6.4 && |dz|<7.1`; Quadrants `|dx|>0.56 && |dz|>0.56 && |dx|<5.7 && |dz|<5.7`; Annulus `1.73<r<6.05`. (dx=x-cx, dz=z-cz)

```csharp
using NUnit.Framework; using BoneSquadKiting.Core;
public class DangerShapeEvalTests
{
    [Test] public void Disc_InsideRadiusIsDanger()
    {
        var s = DangerShape.Disc(3.10f);
        Assert.IsTrue (DangerShapeEval.InDanger(s, 0,0, 2.0f,0));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0,0, 3.2f,0));
    }
    [Test] public void BandsX_CenterCorridorIsSafe()
    {
        var s = DangerShape.BandsX(0.83f, 6.4f, 7.1f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0,0, 0.5f,0)); // |dx|<0.83 safe
        Assert.IsTrue (DangerShapeEval.InDanger(s, 0,0, 2.0f,0));
    }
    [Test] public void Quadrants_CrossCorridorSafe()
    {
        var s = DangerShape.Quadrants(0.56f, 5.7f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0,0, 0.0f, 3.0f)); // on axis safe
        Assert.IsTrue (DangerShapeEval.InDanger(s, 0,0, 3.0f, 3.0f)); // diagonal danger
    }
    [Test] public void Annulus_CenterSafeOuterDanger()
    {
        var s = DangerShape.Annulus(1.73f, 6.05f);
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0,0, 1.0f,0)); // r<1.73 safe
        Assert.IsTrue (DangerShapeEval.InDanger(s, 0,0, 4.0f,0));
        Assert.IsFalse(DangerShapeEval.InDanger(s, 0,0, 7.0f,0)); // r>6.05 safe
    }
}
```

- [x] **Step 2: 실패 확인** — Run EditMode 테스트. Expected: FAIL (`DangerShapeEval` 미정의).

- [x] **Step 3: 구현(shape 범용 평가)**

```csharp
using UnityEngine;
namespace BoneSquadKiting.Core
{
    public static class DangerShapeEval
    {
        public static bool InDanger(DangerShape s, float cx, float cz, float x, float z)
        {
            float dx = x - cx, dz = z - cz, r = Mathf.Sqrt(dx*dx + dz*dz);
            switch (s.Type)
            {
                case DangerShapeType.Disc:         return r < s.RMax;
                case DangerShapeType.Annulus:      return r > s.RMin && r < s.ROut;
                case DangerShapeType.CrossBandsX:  return Mathf.Abs(dx) > s.Inner && Mathf.Abs(dx) < s.Outer && Mathf.Abs(dz) < s.HalfZ;
                case DangerShapeType.QuadrantBoxes:return Mathf.Abs(dx) > s.Inner && Mathf.Abs(dz) > s.Inner && Mathf.Abs(dx) < s.Outer && Mathf.Abs(dz) < s.Outer;
                default: return false;
            }
        }
    }
}
```

- [x] **Step 4: 통과 확인** — Run EditMode 테스트. Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Core/DangerShape.cs bone-squad-kiting/Assets/Scripts/Core/DangerShapeEval.cs bone-squad-kiting/Assets/Tests/EditMode/DangerShapeEvalTests.cs && git commit -m "feat: add data-driven danger-shape evaluator with tests"
```

---

## Task 5: 분대 이동 — 중심점 + 유닛 추종

> **구현 메모(2026-11-18):** Task 5~9의 로직과 테스트는 구현·커밋됨(EditMode 17, PlayMode 22 통과). 남은 것은 씬에 붙이는 단계뿐이다 — Task 5 Step 6(`SquadView`는 작성됨, 씬 배치 전), Task 6 Step 2(수동 입력 확인), Task 7 Step 6(보스·투사체 비주얼, `BossView`/`EnemyRef` 코드는 작성됨), Task 8 Step 5(`TelegraphView` 미작성). Task 1 씬 작업과 함께 한다.
>
> 아래 코드 블록과 실제 구현의 차이: 상태 enum은 `GamePhase`(필드명 `Phase`와 구분), `ShotSystem.OnBossHit` → `OnHit`, `GameDirector`에 HUD용 이벤트(`PatternStarted`/`PatternResolved`/`FormationChanged`/`PhaseTwoStarted`)와 테스트용 `Step(dt)` 추가, 공격 간격 전환 임계는 `GameConstants.FastAttackHpFrac`(0.55), 진형 핫키는 에셋의 `HotKey` + Q/W/E/R, PlayMode 공용 헬퍼는 `SquadTestRig.MakeFc()`. 실제 코드가 기준이다.

**Files:**
- Create: `Assets/Scripts/Squad/FormationController.cs`
- Create: `Assets/Scripts/Squad/SquadUnit.cs`
- Test: `Assets/Tests/PlayMode/SquadMovementTests.cs`

- [x] **Step 1: 실패 PlayMode 테스트**

목적지를 주면 중심이 그쪽으로 이동하고, 유닛들이 `중심+슬롯` 근처로 수렴한다.

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BoneSquadKiting.Squad; using BoneSquadKiting.Data;

public class SquadMovementTests
{
    // Shared helper: build a FormationController with in-memory formation defs assigned.
    // Reused by CombatTests / PatternResolveTests (any test that calls fc.Init()).
    public static FormationController MakeFc()
    {
        var go = new GameObject("fc"); var fc = go.AddComponent<FormationController>();
        FormationDefinition D(string id, FormationMode m, float sp=0, float r=0, float st=0)
        { var d = ScriptableObject.CreateInstance<FormationDefinition>(); d.Id=id; d.Mode=m; d.Spacing=sp; d.Radius=r; d.Stagger=st; return d; }
        fc.Formations = new() { D("dense",FormationMode.Grid,0.53f), D("line",FormationMode.Line,0.49f,st:0.12f),
                                D("ring",FormationMode.Ring,r:3.7f), D("cross",FormationMode.Cross,0.61f) };
        fc.Init();
        return fc;
    }

    [UnityTest] public IEnumerator Anchor_MovesTowardDestination()
    {
        var fc = MakeFc(); fc.SetFormation("dense", silent:true);
        fc.SetDestination(new Vector2(5f, 2f));
        float t = 0; while (t < 2f) { fc.Tick(0.016f); t += 0.016f; yield return null; }
        Assert.That(Vector2.Distance(fc.Anchor, new Vector2(5f,2f)), Is.LessThan(0.2f));
    }

    [UnityTest] public IEnumerator Units_ConvergeToSlots()
    {
        var fc = MakeFc(); fc.SetFormation("dense", silent:true);
        float t = 0; while (t < 2f) { fc.Tick(0.016f); t += 0.016f; yield return null; }
        var u0 = fc.Units[0];
        Vector2 want = fc.Anchor + u0.Slot;
        Assert.That(Vector2.Distance(new Vector2(u0.X,u0.Z), want), Is.LessThan(0.1f));
    }
}
```

> 이후 `CombatTests`/`PatternResolveTests`에서도 `new GameObject...AddComponent<FormationController>()` 대신 `SquadMovementTests.MakeFc()`를 써서 진형 에셋이 주입된 상태로 `Init()`한다.

- [x] **Step 2: 실패 확인** — Run PlayMode 테스트. Expected: FAIL (타입 미정의).

- [x] **Step 3: SquadUnit 구현(데이터+이동, 원본 update 포팅)**

중심 이동: `d>0.024`면 `min(d, 7.4*dt)`. 유닛: 목표 `clamp(anchor+slot, LIMIT)`, 거리>0.015면 `min(dist, (9.7+min(9,dist*2.4))*dt)`. (원본과 동일. SmoothDamp은 스펙의 대안이나 M1은 원본 수식 유지해 1:1 보장.)

```csharp
using UnityEngine;
using BoneSquadKiting.Core;
namespace BoneSquadKiting.Squad
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

- [x] **Step 4: FormationController 구현**

중심 이동 + 25유닛 생성/슬롯 배분 + 틱. `Init()`은 원본 reset의 유닛 배치(시작 `z=3.4` dense). `Tick(dt)`는 anchor 이동 후 각 유닛 `MoveToward`. (MonoBehaviour지만 로직은 `Tick(dt)`로 분리해 테스트가 수동 구동.)

```csharp
using UnityEngine;
using System.Collections.Generic;
using BoneSquadKiting.Core; using BoneSquadKiting.Combat; using BoneSquadKiting.Data;
namespace BoneSquadKiting.Squad
{
    public class FormationController : MonoBehaviour
    {
        public Vector2 Anchor = new(0, 3.4f);
        public Vector2 Destination = new(0, 3.4f);
        public string Formation = "dense";
        public List<FormationDefinition> Formations = new(); // 4 assets assigned in scene / tests
        public SquadOrder Order;                              // current squad order (Task 5.5)
        public readonly List<SquadUnit> Units = new();

        FormationDefinition Find(string id) => Formations.Find(f => f.Id == id);

        public void Init()
        {
            Units.Clear();
            var slots = FormationSlots.Generate(Find("dense"));
            for (int i = 0; i < GameConstants.SquadSize; i++)
                Units.Add(new SquadUnit { Id = i, Slot = slots[i], X = slots[i].x, Z = 3.4f + slots[i].y,
                                          Cd = Random.value*0.65f, Anim = Random.value*6.28f });
        }

        public void SetFormation(string id, bool silent = false)
        {
            Formation = id; var slots = FormationSlots.Generate(Find(id));
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

- [x] **Step 5: 통과 확인** — Run PlayMode 테스트. Expected: PASS.

- [x] **Step 6: 유닛 비주얼(임시 도형) 바인딩**

각 `SquadUnit`에 작은 Capsule/Cylinder 프리미티브를 대응시키는 `SquadView`(MonoBehaviour)를 만들어 매 프레임 `transform.position = new Vector3(u.X, 0, u.Z)`. 색: 청록 머티리얼. (렌더만, 로직 아님.)

- [x] **Step 7: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Squad bone-squad-kiting/Assets/Tests/PlayMode/SquadMovementTests.cs && git commit -m "feat: squad formation movement (anchor + unit follow) with playmode tests"
```

---

## Task 5.5: 적 추상화 + 분대 오더 (결정 #9) — 테스트 우선

**Files:**
- Create: `Assets/Scripts/Combat/IEnemy.cs`, `IDamageable.cs`
- Create: `Assets/Scripts/Combat/EnemyRegistry.cs`
- Create: `Assets/Scripts/Combat/SquadOrder.cs`
- Test: `Assets/Tests/EditMode/EnemyRegistryTests.cs`

어택땅·우클릭 타겟의 토대. M1엔 보스 1개만 등록되지만 M2 잡몹이 바로 드롭인된다.

- [x] **Step 1: 실패 테스트(레지스트리 획득)**

```csharp
using NUnit.Framework; using UnityEngine; using BoneSquadKiting.Combat;
public class EnemyRegistryTests
{
    class FakeEnemy : IEnemy { public Vector2 Pos{get;set;} public bool Alive{get;set;}=true; }
    [Test] public void NearestInRange_PicksClosestAlive()
    {
        var reg = new EnemyRegistry();
        var a = new FakeEnemy{Pos=new Vector2(2,0)}; var b = new FakeEnemy{Pos=new Vector2(5,0)};
        reg.Register(a); reg.Register(b);
        Assert.AreSame(a, reg.NearestInRange(Vector2.zero, 10f));
        a.Alive = false;
        Assert.AreSame(b, reg.NearestInRange(Vector2.zero, 10f)); // dead skipped
        Assert.IsNull(reg.NearestInRange(Vector2.zero, 1f));      // out of range
    }
}
```

- [x] **Step 2: 실패 확인** — Run EditMode. Expected: FAIL.

- [x] **Step 3: 구현**

```csharp
// Combat/IEnemy.cs
using UnityEngine;
namespace BoneSquadKiting.Combat
{
    public interface IDamageable { void ApplyDamage(float amount); }
    public interface IEnemy { Vector2 Pos { get; } bool Alive { get; } }
}
```
```csharp
// Combat/EnemyRegistry.cs
using System.Collections.Generic; using UnityEngine;
namespace BoneSquadKiting.Combat
{
    public class EnemyRegistry
    {
        public readonly List<IEnemy> Enemies = new();
        public void Register(IEnemy e){ if(!Enemies.Contains(e)) Enemies.Add(e); }
        public void Unregister(IEnemy e){ Enemies.Remove(e); }
        public IEnemy NearestInRange(Vector2 from, float range)
        {
            IEnemy best=null; float bd=range*range;
            foreach (var e in Enemies){ if(!e.Alive) continue;
                float d=(e.Pos-from).sqrMagnitude; if(d<=bd){bd=d; best=e;} }
            return best;
        }
    }
}
```
```csharp
// Combat/SquadOrder.cs
using UnityEngine;
namespace BoneSquadKiting.Combat
{
    public enum OrderKind { Move, AttackMove, AttackTarget }
    public struct SquadOrder
    {
        public OrderKind Kind; public Vector2 Point; public IEnemy Target;
        public static SquadOrder Move(Vector2 p)=>new(){Kind=OrderKind.Move, Point=p};
        public static SquadOrder AttackMove(Vector2 p)=>new(){Kind=OrderKind.AttackMove, Point=p};
        public static SquadOrder Attack(IEnemy t)=>new(){Kind=OrderKind.AttackTarget, Target=t};
    }
}
```

`FormationController`에 `public SquadOrder Order;` 추가. `SetDestination`은 `Order = SquadOrder.Move(p)` + anchor 목적지 지정으로 통합. `AttackMove(p)`도 anchor 목적지를 p로 두되 Kind만 다름(이동 중 자동사격은 기존대로, Task 7에서 타겟 획득이 오더를 참조). `AttackTarget(t)`는 타겟이 사거리(`FireRange`) 밖일 때만 anchor를 타겟 쪽으로(클램프 내) 전진하고, 이미 사거리 안이면 **그 자리에 멈춰** 사격한다(Task 9 `IssueOrder`). M1 아레나에서는 보스가 항상 사거리 안이라 보스 우클릭은 "정지 후 집중사격"이 된다.

- [x] **Step 4: 통과 확인** — Run EditMode. Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Combat bone-squad-kiting/Assets/Tests/EditMode/EnemyRegistryTests.cs && git commit -m "feat: enemy abstraction (IEnemy/registry) and squad order state"
```

---

## Task 6: 입력 — 우클릭 스마트 커맨드 / 어택땅 / 진형 / 일시정지 / 재시작 (결정 #9)

**Files:**
- Create: `Assets/Scripts/Input/SquadInputController.cs`

- [x] **Step 1: 구현(스마트 커맨드 + 어택땅)**

- **우클릭**: 카메라 레이를 먼저 **적 콜라이더**에 Raycast → 맞으면 그 `IEnemy`로 `AttackTarget`; 아니면 y=0 지면 교차점으로 `Move`.
- **A → 좌클릭(어택땅)**: `A` 누르면 어택땅 대기모드(커서 피드백). 다음 좌클릭의 지면점으로 `AttackMove`(좌클릭이 적 위면 `AttackTarget`). Esc/우클릭으로 취소.
- 진형 키 `1/q→dense,2/w→line,3/e→ring,4/r→cross`(에셋 HotKey 기반), `p→pause`, `enter→restart`, `space→(예약)`.

```csharp
using UnityEngine; using UnityEngine.InputSystem;
using BoneSquadKiting.Squad; using BoneSquadKiting.Game; using BoneSquadKiting.Combat;
namespace BoneSquadKiting.Input
{
    public class SquadInputController : MonoBehaviour
    {
        public Camera Cam; public FormationController Squad; public GameDirector Director;
        public LayerMask EnemyMask;
        static readonly Plane Ground = new(Vector3.up, Vector3.zero);
        bool _attackMovePending;

        void Update()
        {
            if (Director.Phase == Phase.Play && !Director.Paused)
            {
                if (Pressed(Key.A)) _attackMovePending = true;
                if (Pressed(Key.Escape)) _attackMovePending = false;

                // LMB: resolve attack-move if armed
                if (LmbDown() && _attackMovePending)
                {
                    _attackMovePending = false;
                    if (PickEnemy(out var en)) Director.IssueOrder(SquadOrder.Attack(en));
                    else if (PickGround(out var gp)) Director.IssueOrder(SquadOrder.AttackMove(gp));
                }
                // RMB: cancels a pending attack-move, otherwise smart command — enemy → attack target, ground → move
                else if (RmbDown())
                {
                    if (_attackMovePending) _attackMovePending = false;
                    else if (PickEnemy(out var en)) Director.IssueOrder(SquadOrder.Attack(en));
                    else if (PickGround(out var gp)) Director.IssueOrder(SquadOrder.Move(gp));
                }

                HandleFormationKeys();
            }
            // Pause must stay reachable while paused, so it sits outside the !Paused block.
            if (Director.Phase == Phase.Play && Pressed(Key.P)) Director.TogglePause();
            if (Pressed(Key.Enter) && Director.Phase is Phase.Menu or Phase.Win or Phase.Lose) Director.Restart();
        }

        bool PickEnemy(out IEnemy enemy)
        {
            enemy = null;
            var ray = Cam.ScreenPointToRay(MousePos());
            if (Physics.Raycast(ray, out var hit, 500f, EnemyMask)
                && hit.collider.TryGetComponent<EnemyRef>(out var r) && r.Enemy is { Alive: true })
            { enemy = r.Enemy; return true; }
            return false;
        }
        bool PickGround(out Vector2 p)
        {
            p = default; var ray = Cam.ScreenPointToRay(MousePos());
            if (Ground.Raycast(ray, out float e)) { var w = ray.GetPoint(e); p = new Vector2(w.x, w.z); return true; }
            return false;
        }
        void HandleFormationKeys()
        {
            if (Pressed(Key.Digit1)||Pressed(Key.Q)) Director.ChangeFormation("dense");
            if (Pressed(Key.Digit2)||Pressed(Key.W)) Director.ChangeFormation("line");
            if (Pressed(Key.Digit3)||Pressed(Key.E)) Director.ChangeFormation("ring");
            if (Pressed(Key.Digit4)||Pressed(Key.R)) Director.ChangeFormation("cross");
        }
        // New Input System only (ProjectSettings activeInputHandler: 1) — UnityEngine.Input would throw.
        static bool Pressed(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
        static bool LmbDown() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        static bool RmbDown() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        static Vector3 MousePos() => Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
    }
}
```

> `EnemyRef`(MonoBehaviour)는 콜라이더가 붙은 적 오브젝트에서 자신의 `IEnemy`를 노출하는 작은 브리지(코드는 Task 7 Step 6). 보스 비주얼에 콜라이더+`EnemyRef` 부착. 입력은 `Keyboard.current`/`Mouse.current`를 직접 읽는다 — 런타임 asmdef에 `Unity.InputSystem` 참조가 있어야 한다(Task 0 Step 4).
> 우클릭 지면 드래그 이동(원본 draggingRight 느낌)을 유지하려면 `GetMouseButton(1)` held일 때 적 비피킹 시 계속 `Move` 갱신하도록 확장 가능.

- [ ] **Step 2: 수동 검증** — PlayMode에서 우클릭 지면 이동·우클릭 보스 공격명령·A+좌클릭 어택땅·1~4 진형 전환 확인(스크린샷/플레이).

- [x] **Step 3: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Input && git commit -m "feat: squad input (rmb smart command, attack-move, formation keys, pause, restart)"
```

---

## Task 7: 자동 평타 + 투사체 + 보스 HP

**Files:**
- Create: `Assets/Scripts/Boss/Boss.cs`
- Create: `Assets/Scripts/Squad/ShotSystem.cs`
- Test: `Assets/Tests/PlayMode/CombatTests.cs`

- [x] **Step 1: 실패 테스트(보스가 시간 경과로 피해 입음)**

```csharp
using System.Collections; using NUnit.Framework; using UnityEngine; using UnityEngine.TestTools;
using BoneSquadKiting.Squad; using BoneSquadKiting.Bosses; using BoneSquadKiting.Core; using BoneSquadKiting.Combat;

public class CombatTests
{
    [UnityTest] public IEnumerator Boss_TakesDamageOverTime()
    {
        var fc = SquadMovementTests.MakeFc(); fc.Anchor = new Vector2(0, 3.4f);
        var boss = new Boss();
        var reg = new EnemyRegistry(); reg.Register(boss);
        var shots = new ShotSystem();
        var order = SquadOrder.AttackMove(fc.Anchor); // auto-fire at in-range enemies
        float start = boss.Hp, t = 0;
        while (t < 3f) { fc.Tick(0.016f); shots.Tick(fc.Units, reg, order, 0.016f); t += 0.016f; yield return null; }
        Assert.That(boss.Hp, Is.LessThan(start));
    }
}
```

- [x] **Step 2: 실패 확인** — Run PlayMode. Expected: FAIL.

- [x] **Step 3: Boss 구현 (`IEnemy`,`IDamageable`)**

```csharp
using UnityEngine; using BoneSquadKiting.Core; using BoneSquadKiting.Combat;
namespace BoneSquadKiting.Bosses
{
    public class Boss : IEnemy, IDamageable
    {
        public float Hp = GameConstants.BossMax; public int Phase = 1; public float Flash;
        public float HpFrac => Hp / GameConstants.BossMax;
        // Boss is fixed at arena position; M2 enemies will carry their own moving Pos.
        public Vector2 Pos => new(GameConstants.BossX, GameConstants.BossZ);
        public bool Alive => Hp > 0;
        public void ApplyDamage(float amount) { Hp = Mathf.Max(0, Hp - amount); Flash = 0.8f; }
    }
}
```

> `GameDirector.Restart`에서 `EnemyRegistry.Register(Boss)`. 보스 비주얼(Step 6)에 콜라이더+`EnemyRef{ Enemy = boss }` 부착해 우클릭 피킹 가능하게.

- [x] **Step 4: ShotSystem 구현(오더 기반 타겟 획득 + 원본 발사/명중)**

유닛 평타: **타겟 선택** — 오더가 `AttackTarget`이고 그 적이 생존·사거리 내면 그 적, 아니면 `registry.NearestInRange(유닛, 15.9)`. 타겟 있고 cd<=0 → 쿨 `0.76+rand*0.16`, 투사체 생성(타겟 방향, 속도22). 투사체는 **생성 시점 타겟**을 추적 보관(`TargetRef`); 명중(반경1.15) 시 `IDamageable.ApplyDamage(33)`, hit 콜백(DPS·플래시). M1은 타겟이 보스뿐이라 결과는 원본과 동일하되, 구조는 다중 적 대응.

```csharp
using System.Collections.Generic; using UnityEngine;
using BoneSquadKiting.Core; using BoneSquadKiting.Combat;
namespace BoneSquadKiting.Squad
{
    public struct Shot { public float X, Z, Vx, Vz, Life; public int Owner; public IEnemy Target; }
    public class ShotSystem
    {
        public readonly List<Shot> Shots = new();
        public System.Action<float> OnBossHit; // damage dealt (name kept; = any enemy hit)

        static IEnemy Acquire(SquadUnit u, EnemyRegistry reg, SquadOrder order)
        {
            var p = new Vector2(u.X, u.Z);
            if (order.Kind == OrderKind.AttackTarget && order.Target is { Alive: true } t
                && (t.Pos - p).magnitude < GameConstants.FireRange) return t;
            return reg.NearestInRange(p, GameConstants.FireRange);
        }

        public void Tick(IList<SquadUnit> units, EnemyRegistry reg, SquadOrder order, float dt)
        {
            foreach (var u in units)
            {
                if (u.Dead || u.Cd > 0) continue;
                var tgt = Acquire(u, reg, order);
                if (tgt == null) continue;
                u.Cd = GameConstants.UnitCdMin + Random.value*GameConstants.UnitCdRand;
                float ang = Mathf.Atan2(tgt.Pos.y-u.Z, tgt.Pos.x-u.X);
                Shots.Add(new Shot { X = u.X+Mathf.Cos(ang)*0.29f, Z = u.Z+Mathf.Sin(ang)*0.29f,
                                     Vx = Mathf.Cos(ang)*GameConstants.ShotSpeed, Vz = Mathf.Sin(ang)*GameConstants.ShotSpeed,
                                     Life = GameConstants.ShotLife, Owner = u.Id, Target = tgt });
                u.ShotCount++;
            }
            for (int i = Shots.Count-1; i >= 0; i--)
            {
                var b = Shots[i]; b.X += b.Vx*dt; b.Z += b.Vz*dt; b.Life -= dt;
                var tp = b.Target is { Alive: true } ? b.Target.Pos : new Vector2(float.NaN, float.NaN);
                if (b.Target is { Alive: true } && Mathf.Sqrt((b.X-tp.x)*(b.X-tp.x)+(b.Z-tp.y)*(b.Z-tp.y)) < GameConstants.ShotHitRadius)
                {
                    (b.Target as IDamageable)?.ApplyDamage(GameConstants.UnitDamage);
                    OnBossHit?.Invoke(GameConstants.UnitDamage); Shots.RemoveAt(i);
                }
                else if (b.Life <= 0 || Mathf.Abs(b.X) > 15 || Mathf.Abs(b.Z) > 13) Shots.RemoveAt(i);
                else Shots[i] = b;
            }
        }
    }
}
```

- [x] **Step 5: 통과 확인** — Run PlayMode. Expected: PASS.

- [x] **Step 6: 보스/투사체 비주얼(임시) + 피킹 브리지** — 보스 큰 Cylinder(붉은 Emissive, 콜라이더 유지, 레이어 `Enemy`), 투사체 작은 Sphere/TrailRenderer. 보스 오브젝트에 아래 두 컴포넌트를 붙인다.

```csharp
// Combat/EnemyRef.cs
using UnityEngine;
namespace BoneSquadKiting.Combat
{
    public class EnemyRef : MonoBehaviour { public IEnemy Enemy; } // set at runtime (interfaces are not serialized)
}
```
```csharp
// Boss/BossView.cs
using UnityEngine; using BoneSquadKiting.Combat;
namespace BoneSquadKiting.Bosses
{
    [RequireComponent(typeof(EnemyRef))]
    public class BossView : MonoBehaviour
    {
        Boss _boss;
        public void Bind(Boss boss) { _boss = boss; GetComponent<EnemyRef>().Enemy = boss; }
        void Update() { if (_boss != null) transform.position = new Vector3(_boss.Pos.x, 0, _boss.Pos.y); }
    }
}
```

- [x] **Step 7: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Boss bone-squad-kiting/Assets/Scripts/Combat/EnemyRef.cs bone-squad-kiting/Assets/Scripts/Squad/ShotSystem.cs bone-squad-kiting/Assets/Tests/PlayMode/CombatTests.cs && git commit -m "feat: auto-attack shots and boss hp with playmode test"
```

---

## Task 8: 보스 패턴 시스템 (스케줄·텔레그래프·resolve)

**Files:**
- Create: `Assets/Scripts/Boss/BossAttackSystem.cs`
- Create: `Assets/Scripts/Boss/TelegraphView.cs`
- Test: `Assets/Tests/PlayMode/PatternResolveTests.cs`

- [x] **Step 1: 실패 테스트(장판 안 유닛만 피해)**

core 패턴 resolve 시 중심 반경<3.10 유닛이 55 피해.

```csharp
using System.Collections; using NUnit.Framework; using UnityEngine; using UnityEngine.TestTools;
using BoneSquadKiting.Squad; using BoneSquadKiting.Bosses; using BoneSquadKiting.Core;

public class PatternResolveTests
{
    [UnityTest] public IEnumerator Core_DamagesUnitsInRadius()
    {
        var fc = SquadMovementTests.MakeFc(); fc.Anchor = Vector2.zero;
        foreach (var u in fc.Units) { u.X = 0; u.Z = 0; } // all at center → all in core danger
        var sys = new BossAttackSystem();
        sys.ForceResolve(DangerShape.Disc(3.10f), 0, 0, fc.Units); // resolve by shape, not pattern id
        Assert.That(fc.Units[0].Hp, Is.EqualTo(GameConstants.UnitMax - GameConstants.PatternUnitDamage).Within(0.01f));
        yield break;
    }
}
```

- [x] **Step 2: 실패 확인** — Run PlayMode. Expected: FAIL.

- [x] **Step 3: BossAttackSystem 구현**

상태: 현재 패턴(`PatternDefinition`)/elapsed/remaining, nextAttack 타이머, attackIndex. 시퀀스는 **`AttackSequence` 에셋** 주입(`public AttackSequence Sequence;`). `Tick`에서 패턴 없으면 nextAttack 감소→StartPattern, 있으면 remaining 감소→막판 0.38s 전까지 장판 중심이 anchor 추종(lerp 11), remaining<=0이면 Resolve. `Resolve`: 각 유닛 `DangerShapeEval.InDanger(shape,…)`면 55 피해·사망, 결과(피격/사망 수) 반환. nextAttack = HpFrac<0.55 ? 1.45 : 1.9. `ForceResolve(DangerShape,…)`는 테스트/디버그용.

```csharp
using System.Collections.Generic; using UnityEngine;
using BoneSquadKiting.Core; using BoneSquadKiting.Squad; using BoneSquadKiting.Data;
namespace BoneSquadKiting.Bosses
{
    public class BossAttackSystem
    {
        public AttackSequence Sequence;  // injected (SO). null-safe for ForceResolve tests.
        public struct Active { public PatternDefinition P; public float Cx, Cz, Elapsed, Remaining; }
        public Active? Current; public int Index; public float NextAttack = GameConstants.FirstAttackDelay;

        public System.Action<PatternDefinition> OnStart;
        public System.Action<int,int> OnResolved; // hitCount, lost

        public void Tick(FormationController squad, Boss boss, float dt)
        {
            if (Current is Active a)
            {
                a.Elapsed += dt; a.Remaining = Mathf.Max(0, a.P.Time - a.Elapsed);
                if (a.Remaining > GameConstants.CastTrackLockTime)
                { a.Cx = Mathf.Lerp(a.Cx, squad.Anchor.x, Mathf.Min(1, dt*11)); a.Cz = Mathf.Lerp(a.Cz, squad.Anchor.y, Mathf.Min(1, dt*11)); }
                Current = a;
                if (a.Remaining <= 0) Resolve(a.P.Shape, a.Cx, a.Cz, squad.Units, boss);
            }
            else { NextAttack -= dt; if (NextAttack <= 0) StartPattern(squad, boss); }
        }

        void StartPattern(FormationController squad, Boss boss)
        {
            var seq = Sequence.Patterns; var p = seq[Index % seq.Length]; Index++;
            Current = new Active { P = p, Cx = squad.Anchor.x, Cz = squad.Anchor.y, Elapsed = 0, Remaining = p.Time };
            OnStart?.Invoke(p);
        }

        public void ForceResolve(DangerShape shape, float cx, float cz, IList<SquadUnit> units)
            => Resolve(shape, cx, cz, units, null);

        void Resolve(DangerShape shape, float cx, float cz, IList<SquadUnit> units, Boss boss)
        {
            int hit = 0, lost = 0;
            foreach (var u in units)
            {
                if (u.Dead) continue;
                if (DangerShapeEval.InDanger(shape, cx, cz, u.X, u.Z))
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

- [x] **Step 4: 통과 확인** — Run PlayMode. Expected: PASS.

- [x] **Step 5: TelegraphView(임시 시각화)**

현재 패턴의 안전/위험 영역을 지면 위 반투명 메시로 표시: core=원, gate=좌우 사각, quadrant=4모서리 사각, shell=도넛. M1은 간단히 반투명 Quad/절차 메시 + 붉은/청록 색. 중심은 `Current.Cx,Cz` 추종. 카운트다운은 Task 9 HUD에서.

- [x] **Step 6: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Boss bone-squad-kiting/Assets/Tests/PlayMode/PatternResolveTests.cs && git commit -m "feat: boss attack scheduling, telegraph tracking, and pattern resolve"
```

---

## Task 9: GameDirector (상태머신·페이즈·DPS·승패)

**Files:**
- Create: `Assets/Scripts/Game/GameDirector.cs`
- Create: `Assets/Scripts/Core/CombatMath.cs`
- Test: `Assets/Tests/EditMode/CombatMathTests.cs`

- [x] **Step 1: 실패 테스트(DPS 윈도 & 페이즈 임계)**

```csharp
using NUnit.Framework; using System.Collections.Generic; using BoneSquadKiting.Core;
public class CombatMathTests
{
    [Test] public void Dps_IsSumOverWindow()
    {
        var ev = new List<(float t,float d)> { (0.0f,33), (1.0f,33), (2.0f,33) };
        Assert.That(CombatMath.Dps(ev, now:2.0f), Is.EqualTo(99f/GameConstants.DpsWindow).Within(0.01f));
    }
    [Test] public void Phase2_TriggersBelow54Percent()
    {
        Assert.IsFalse(CombatMath.IsPhase2(0.55f));
        Assert.IsTrue (CombatMath.IsPhase2(0.53f));
    }
}
```

- [x] **Step 2: 실패 확인** — Run EditMode. Expected: FAIL.

- [x] **Step 3: CombatMath 구현** (클래스명이 네임스페이스 `BoneSquadKiting.Combat`과 겹치지 않게 `CombatMath`)

```csharp
using System.Collections.Generic;
namespace BoneSquadKiting.Core
{
    public static class CombatMath
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

- [x] **Step 4: 통과 확인** — Run EditMode. Expected: PASS.

- [x] **Step 5: GameDirector 구현(원본 game 상태 + update 루프 결선)**

`Phase {Menu,Play,Win,Lose}`. 필드: elapsed, paused, hitEvents, lastDps, bossPhase. `Update(dt)`에서 play일 때: squad.Tick → shots.Tick(+hitEvent 기록) → attack.Tick → DPS 갱신 → 페이즈 전환 체크(HpFrac<0.54 & phase1→phase2) → 전멸 시 `Lose`, 보스 0 시 `Win`. `ChangeFormation/TogglePause/Restart/Begin`. hitEvent는 shots.OnBossHit에서 `(elapsed, dmg)` 추가, 2.4s 지난 건 제거. 시스템 인스턴스(FormationController, ShotSystem, BossAttackSystem, Boss)를 보유·구동.

```csharp
using System.Collections.Generic; using UnityEngine;
using BoneSquadKiting.Core; using BoneSquadKiting.Squad; using BoneSquadKiting.Bosses;
using BoneSquadKiting.Combat; using BoneSquadKiting.Data;
namespace BoneSquadKiting.Game
{
    public enum Phase { Menu, Play, Win, Lose }
    public class GameDirector : MonoBehaviour
    {
        public FormationController Squad;           // assigned in scene
        public AttackSequence Sequence;             // boss pattern sequence asset
        public BossView BossView;                   // scene boss visual (collider + EnemyRef)
        public Phase Phase = Phase.Menu; public bool Paused;
        public float Elapsed, LastDps; public int BossPhase = 1;
        public Boss Boss { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public BossAttackSystem Attacks { get; private set; }
        public ShotSystem Shots { get; private set; }
        readonly List<(float t, float d)> _hits = new();
        public float TotalDamage;

        public void Restart()
        {
            Boss = new Boss(); Enemies = new EnemyRegistry(); Enemies.Register(Boss);
            Attacks = new BossAttackSystem { Sequence = Sequence }; Shots = new ShotSystem();
            _hits.Clear(); TotalDamage = 0; Elapsed = 0; BossPhase = 1; Paused = false;
            Shots.OnBossHit += d => { _hits.Add((Elapsed, d)); TotalDamage += d; };
            Squad.Init(); Squad.SetFormation("dense", true);
            Squad.Anchor = new Vector2(0, 3.4f); Squad.Destination = Squad.Anchor;
            Squad.Order = SquadOrder.Move(Squad.Anchor);   // auto-fire still engages in-range enemies
            if (BossView) BossView.Bind(Boss);
            Phase = Phase.Play;
        }

        public void TogglePause() { if (Phase == Phase.Play) Paused = !Paused; }
        public void ChangeFormation(string id) { if (Phase == Phase.Play) Squad.SetFormation(id); }

        // Called by input. Move/AttackMove set anchor destination.
        // AttackTarget advances only when the target is out of range; in range it holds position and fires.
        public void IssueOrder(SquadOrder o)
        {
            if (Phase != Phase.Play || Paused) return;
            Squad.Order = o;
            if (o.Kind == OrderKind.AttackTarget && o.Target != null)
            {
                bool inRange = (o.Target.Pos - Squad.Anchor).magnitude < GameConstants.FireRange;
                Squad.SetDestination(inRange ? Squad.Anchor : o.Target.Pos);
            }
            else Squad.SetDestination(o.Point);
        }

        void Update()
        {
            if (Phase != Phase.Play || Paused) return;
            float dt = Mathf.Min(0.042f, Time.deltaTime); Elapsed += dt;
            Squad.Tick(dt);
            Shots.Tick(Squad.Units, Enemies, Squad.Order, dt);
            Attacks.Tick(Squad, Boss, dt);
            _hits.RemoveAll(e => Elapsed - e.t >= GameConstants.DpsWindow);
            LastDps = CombatMath.Dps(_hits, Elapsed);
            if (CombatMath.IsPhase2(Boss.HpFrac) && BossPhase == 1) { BossPhase = 2; Boss.Phase = 2; }
            bool allDead = true; foreach (var u in Squad.Units) if (!u.Dead) { allDead = false; break; }
            if (allDead) Phase = Phase.Lose;
            else if (Boss.Hp <= 0) Phase = Phase.Win;
        }
    }
}
```

> `BossView`(Task 7 Step 6에서 작성): 콜라이더 + `EnemyRef`를 들고 `Bind(Boss)`로 참조 연결. 우클릭 피킹·플래시 표시에 사용.
>
> **확인 필요(원본 대조):** 2페이즈 전환 임계는 `Phase2HpFrac = 0.54`인데 `BossAttackSystem.Resolve`의 공격 간격 전환은 `0.55f` 하드코딩이다. 프로토타입 원본에서 두 값이 실제로 다른지 확인하고, 같다면 `CombatMath.IsPhase2(boss.HpFrac)`로 통일한다.

- [x] **Step 6: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/Game bone-squad-kiting/Assets/Scripts/Core/CombatMath.cs bone-squad-kiting/Assets/Tests/EditMode/CombatMathTests.cs && git commit -m "feat: game director state machine with dps and phase logic"
```

---

## Task 10: HUD (uGUI)

**Files:**
- Create: `Assets/Scripts/UI/Hud.cs`
- Create: `bone-squad-kiting/Assets/Prefabs/HudCanvas.prefab` (또는 씬 내 Canvas)

- [ ] **Step 1: Canvas 구성**

`unity:ui-ugui` 스킬을 따라 Screen Space - Overlay Canvas에 배치: 상단 보스 HP 바(Image fill)+퍼센트+수치(TMP), 생존수 TMP, DPS TMP, 하단 진형 패널(버튼 4개 dense/line/ring/cross, 선택/추천 하이라이트), 중앙 상단 패턴 경고(이름+카운트다운+진행바), 중앙 토스트 TMP, 시작/승패 오버레이 패널(제목/설명/시작 버튼). 원본 HUD 레이아웃·문구 참고.

- [ ] **Step 2: Hud.cs 바인딩**

`GameDirector`를 참조해 매 프레임 갱신: 보스 fill=`Boss.HpFrac`, 퍼센트=`Ceil(frac*100)`, 생존=`Units.Count(!Dead)`(10 미만 붉게), DPS=`Round(LastDps)`. `Attacks.OnStart`→경고 표시(이름/추천 버튼 하이라이트), `Tick` 중 `Current.Remaining`→카운트다운/진행바, `OnResolved(hit,lost)`→토스트("PERFECT DODGE!" 또는 "피격 n명 · 전사 m명"). 진형 버튼 클릭→`Director.ChangeFormation`. 오버레이: Phase Menu/Win/Lose에 표시, 시작 버튼→`Director.Restart`.

- [ ] **Step 3: 수동 검증** — 플레이로 보스바 감소·생존수·DPS·경고·토스트·진형 하이라이트 동작 확인(스크린샷).

- [ ] **Step 4: Commit**

```bash
git add bone-squad-kiting/Assets/Scripts/UI bone-squad-kiting/Assets/Prefabs bone-squad-kiting/Assets/Scenes && git commit -m "feat: ugui hud (boss bar, alive, dps, formation panel, warnings, overlay)"
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
git add bone-squad-kiting/Assets/Scripts/Fx && git commit -m "feat: temp juice fx (shake, flash, particles, rings)"
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
git add -A && git commit -m "chore: M1 integration pass and tuning against prototype"
```

---

## Self-Review 메모

- **Spec 커버리지:** 프로토타입의 이동/진형/평타/4패턴/2페이즈/DPS감소/승패/HUD/일시정지/재시작/연출 — 각각 Task 5·3/5·7·4&8·9·9·9·10·6·6·11에 매핑. **추가(결정 #8·#9):** 패턴·진형 데이터화(Task 2·3·4), 적 추상화·오더(Task 5.5), 우클릭 스마트 커맨드·어택땅(Task 6), 오더 기반 타겟 획득(Task 7·9). 대시는 M4. formation-spec의 NavMesh/FlowField/적푸시는 M2~M3(PLAN §4).
- **타입 일관성:** `FormationSlots.Generate(def)`/`DangerShapeEval.InDanger(shape,…)`/`ShotSystem.Tick(units,registry,order,dt)`/`BossAttackSystem`(+`AttackSequence`)/`Dps`/`IsPhase2` 시그니처는 테스트와 구현에서 동일. 좌표는 전부 `(x→x, z→z)` 규약. `IEnemy.Pos`도 동일 규약.
- **데이터↔코드 경계:** 패턴/진형의 **값**은 ScriptableObject 에셋(원본 1:1), **로직**은 범용 평가기. 1:1 보장은 평가기 EditMode 테스트 + 에셋 값. 새 패턴/진형 추가는 에셋 추가로(코드 무변경), 새 기하/모드는 평가기에 1종 추가.
- **이름 충돌 회피:** 네임스페이스 `BoneSquadKiting.Bosses`(클래스 `Boss`와 분리), 클래스 `CombatMath`(네임스페이스 `BoneSquadKiting.Combat`과 분리). 폴더명은 `Scripts/Boss/`·`Scripts/Combat/` 그대로.
- **M1 단일 보스 한계:** 어택땅·우클릭 타겟은 적이 보스뿐이라 체감은 제한적(두 명령 모두 보스 지향으로 수렴). 구조(레지스트리·오더·`IEnemy`)는 M2 잡몹에서 그대로 활성화.
- **플레이스홀더:** 순수 로직(Task 2~9)은 완전한 C# 포함. Unity-에디터 의존(씬/카메라/Canvas/프리미티브/콜라이더·`EnemyRef` 바인딩, SO 에셋 작성)은 해당 unity 스킬을 따르는 구체 단계로 기술 — 실행 시 설치된 6000.3.28f1 API로 확정.
