# 서리 기사 보스 연출 조립 가이드

## 작업을 시작할 씬

| 목적 | 씬 | 확인할 곳 |
| --- | --- | --- |
| 왕좌에서 보스와 검기 배치 확인 | [BackgroundTest.unity](../../bone-squad-kiting/Assets/Scenes/BackgroundTest.unity) | Hierarchy에서 `BossStage`, `FrostKnight`, `SovereignSlash`를 찾습니다. `TwinSwordSlash`도 배치돼 있으며, 미리 보려면 이 오브젝트를 활성화합니다. |
| 실제 게임에 연출 반영 | [Game.unity](../../bone-squad-kiting/Assets/Scenes/Game.unity) | `Stage Run → Stages`의 세 번째 항목이 보스 스테이지입니다. 2026-11-19 확인 기준으로 이 씬에는 `BossStage` 배경이 연결돼 있고, 서리 기사 본체와 검기 프리팹은 별도로 연결해야 합니다. |

## 왕좌와 보스 본체

| 조립할 부분 | 에셋 | 배치하거나 조정할 곳 |
| --- | --- | --- |
| 왕좌와 단상 | [BossStage.prefab](../../bone-squad-kiting/Assets/Art/Prefab/BossStage.prefab), [Throne Chamber.prefab](../../bone-squad-kiting/Assets/Art/Prefab/BossStageSections/Throne%20Chamber.prefab) | 맵 전체는 `BossStage`, 왕좌 구역은 `Throne Chamber`입니다. 의자 `BA_HornedThrone`을 기준으로 보스의 앉은 위치를 맞춥니다. |
| 보스 본체와 대검 | [FrostKnight.prefab](../../bone-squad-kiting/Assets/Art/Character/FrostKnight/FrostKnight.prefab) | 대검 소환과 소멸, 두 겹의 남색 아우라, 사망 디졸브가 들어 있습니다. |
| 보스 모션 | [FrostKnight.controller](../../bone-squad-kiting/Assets/Art/Character/FrostKnight/FrostKnight.controller) | 왕좌 모션은 `HeadBowed`, `ThroneIdle`, `RaiseHands`, `ThroneSlam`입니다. 전투 모션은 `StandUp`, `ChargedSlash`, `SwordPlant`, `Groggy`, `Die`입니다. |
| 검을 바닥에 꽂을 때의 불길 | `FrostKnight` 아래의 `Sword Plant Fire Area` | `SwordPlant` 시작 후 0.25초에 불길이 솟습니다. 상자 아랫면을 바닥에 맞추고 Scale의 X와 Z로 범위, Y로 높이를 조절합니다. 프리팹 단독 미리보기에서는 이 배치를 사용합니다. 보스전에서는 즉사 판정이 발생할 때 선택된 구역 전체에 불길이 솟습니다. 지속 시간은 `Burn Seconds`에서 조절합니다. [불길과 디졸브 조정 안내](../mob-shader.md) |

## 공격 이펙트와 경고 장판

공격 프리팹은 Unity Project 창의 [Assets/Art/Prefab/Telegraphs/](../../bone-squad-kiting/Assets/Art/Prefab/Telegraphs/)에 있습니다.

왕좌 페이즈의 평행 참격은 시전 순간의 분대 중심을 기준으로 두 줄을 함께 −45°~+45° 범위로 돌립니다. 각 줄은 폭 2.4m, 길이 12m이고 가운데 안전 통로는 1.8m입니다. 예고가 끝나면 `FrostKnightView.ParallelEffect`에 연결된 `SovereignSlashLongTrail`이 각 줄의 왕좌 쪽 끝에서 입구 쪽으로 진행합니다. 원형 공격은 시전 시점 분대 중심에 고정되며, 장판과 피해 반경은 생존 병사 수에 따른 원형 진형 반경의 90%입니다. 빈자리 두 곳에도 같은 장판과 타격 이펙트가 동시에 재생됩니다. 평행 참격은 기본 설정에서 세 세트의 방향이 서로 최소 22.5° 벌어지며, 각 세트의 두 칼날은 평행하게 진행합니다. 피해 판정은 분대 쪽에 예고한 범위에서만 발생합니다. 두 패턴의 범위와 비율은 `Game` 씬의 `Boss Arena → Tuning`에서 조정합니다.

2페이즈 가로 차징에서는 보스가 왼쪽 또는 오른쪽에 나타납니다. 차징을 시작하는 순간부터 검을 휘두르기 전까지 공중의 몸통을 클릭해 공격할 수 있습니다. 사거리 안에 멈춘 병사는 자동 사격으로도 피해를 줍니다.

폭 5m의 장판 세 줄이 보스 발밑에서 반대쪽으로 22m 자라며, 기본 속도에서 약 1.63초 뒤 세 줄이 끝까지 차면 `SovereignTripleSlash`의 검기 세 개가 한 번에 나갑니다. 검기와 끝 잔상은 프리팹의 크기를 유지하고 진행 거리만 방 너비에 맞춥니다. 두 안전 통로의 폭은 각각 일자 진형의 병사 간격에 `Charge Gap Padding`을 더한 값의 두 배입니다. 기본 설정에서는 각각 1.22m입니다.

두 페이즈의 참격은 바닥 흔적을 0.12m 들어 올려 보스방 중앙 문양을 가로질러도 보이게 합니다. 높이는 `FrostKnightView → Slash Mark Lift`에서 조절합니다.

| 원하는 연출 | 프리팹 | 배치 기준 |
| --- | --- | --- |
| 원형 장판 뒤 대검 낙하 | [SovereignSwordPlunge.prefab](../../bone-squad-kiting/Assets/Art/Prefab/Telegraphs/SovereignSwordPlunge.prefab) | 타격 중심에 놓습니다. 재생 후 1.15초에 장판이 사라지고 1.51초에 검 끝이 바닥에 닿으며 충격파가 퍼집니다. |
| 경고부터 세 갈래 참격까지 미리 보기 | [SovereignTripleSlashWithWarning.prefab](../../bone-squad-kiting/Assets/Art/Prefab/Telegraphs/SovereignTripleSlashWithWarning.prefab) | 장판 중심에 놓고 파란색 로컬 Z축을 공격 방향으로 맞춥니다. 장판, 참격, 바닥 잔상이 차례로 재생됩니다. |
| 전투 패턴에서 세 갈래 참격 재생 | [SovereignTripleSlash.prefab](../../bone-squad-kiting/Assets/Art/Prefab/Telegraphs/SovereignTripleSlash.prefab) | [공격 패턴](../../bone-squad-kiting/Assets/Data/Patterns/SovereignTripleSlash.asset)의 `Burst Effect`에 연결돼 있습니다. 이 패턴을 보스 공격 순서에 넣으려면 [BossMain.asset](../../bone-squad-kiting/Assets/Data/Sequences/BossMain.asset)의 `Patterns`에 추가합니다. |
| 단일 검기와 쌍검 검기 | `SovereignSlash.prefab`, `TwinSwordSlash.prefab` | 단일 검기를 넓게 보이려면 `SovereignSlashWide.prefab`, 잔상을 길게 남기려면 `SovereignSlashLongTrail.prefab`을 씁니다. |
| 경고 장판만 배치 | `TelegraphDisc.prefab`, `TelegraphDonut.prefab`, `TelegraphRectangle.prefab`, `TelegraphLethalRectangle.prefab` | 각각 원형, 도넛형, 직사각형, 치명 공격용 직사각형 장판입니다. |

특정 모션의 한 프레임에 이펙트를 붙일 때는 Animator와 같은 오브젝트의 `AnimationEffectSpawner`에 프리팹과 생성 위치를 등록하고, 클립에 `SpawnEffect` 이벤트를 추가합니다. [애니메이션 이벤트 설정](animation-effect-spawner.md)

## 효과음과 화면 효과

보스방에서도 복도에서 사용하던 게임 카메라가 같은 시야각으로 분대를 따라갑니다. 검격·착지·사망 때 이 카메라에 흔들림이 적용됩니다. 게임 카메라와 `BackgroundTest` 카메라는 맵 바깥을 검정색으로 표시합니다. 보스 스테이지는 `BackgroundTest`와 같은 방향광과 `StageBossVolume`을 사용합니다.

`Game` 씬에서는 보스방 진입 후 복도와 입구 아치가 사라져도 등불의 밝기를 유지합니다. 입구 바닥에 닿는 빛도 `BackgroundTest`에서 보던 상태로 남습니다.

보스 효과음 원본은 [Assets/soundresource/03_보스몬스터 사운드/](../../bone-squad-kiting/Assets/soundresource/03_보스몬스터%20사운드/)에 있습니다. 아래 재생 ID는 [SoundDatabase.asset](../../bone-squad-kiting/Assets/Data/Audio/SoundDatabase.asset)에 등록돼 있습니다.

| 재생 시점 | 사운드 ID |
| --- | --- |
| 회전검기를 휘두를 때 / 검기가 날아갈 때 | `boss.spinningblade.swing` / `boss.spinningblade.flight` |
| 대검이 나타날 때 / 떨어질 때 / 바닥에 닿을 때 | `boss.greatsword.summon` / `boss.greatsword.fall` / `boss.greatsword.impact` |
| 보스 스테이지 배경음 | `bgm.boss` |

맵 전체의 색감은 [StageBossVolume.asset](../../bone-squad-kiting/Assets/Settings/StageBossVolume.asset)에서 조절합니다. 타격 순간의 카메라 흔들림과 일시적인 화면색 변화는 [PresentationDatabase.asset](../../bone-squad-kiting/Assets/Data/Fx/PresentationDatabase.asset)의 `Effects`에 등록합니다. 확인 시점에는 `Effects`와 `Moods`가 모두 비어 있습니다. [연출 프리셋 등록과 호출](presentation.md)
