# 연출 프리셋 등록과 호출

다섯 게임 씬과 아티스트 맵의 `StageKit`은 `Assets/Data/Fx/PresentationDatabase.asset`을 공유한다. 처음에는 두 목록이 비어 있다. Inspector에서 사용할 목록에 항목을 추가하고, 목록 안에서 고유한 ID를 지정한다.

| 목록 | 조정할 값 | 동작 |
| --- | --- | --- |
| Effects | Shake Strength, Shake Seconds | 카메라가 흔들리는 크기와 시간. 여러 요청이 겹치면 그 순간 가장 강한 흔들림이 보인다. |
| Effects | Profile, Hold Seconds, Return Seconds | 화면에 바로 적용할 볼륨 프로필, 유지 시간, 현재 분위기로 돌아가는 시간. 흔들림만 필요하면 Profile을 비운다. |
| Moods | Profile, Transition Seconds | 스테이지 분위기에 쓸 볼륨 프로필과 전환 시간. 다른 분위기를 지정하거나 해제할 때까지 유지된다. |

예를 들어 피격 연출을 만들려면 볼륨 프로필에 비네트를 추가하고 색과 강도의 오버라이드를 켠다. Effects에 `impact.heavy`를 등록한 뒤 이 프로필을 연결한다. 분위기에는 색보정 프로필을 사용해 같은 방법으로 Moods에 `stage.fortress`를 등록할 수 있다. 아래 ID는 등록 예시다.

```csharp
using BoneSquadKiting.Fx;

// 게임플레이 메서드 안에서 원하는 시점에 호출한다.
PresentationManager.Current?.SetMood("stage.fortress");
PresentationManager.Current?.Play("impact.heavy");
// 씬 본래의 색으로 돌아갈 때 호출한다.
PresentationManager.Current?.ClearMood();
```

ID의 대소문자를 구분한다. 등록되지 않은 ID를 전달하면 `Play`와 `SetMood`는 `false`를 반환하며 재생 중인 효과는 이어진다. 일시 화면색을 새로 호출하면 그 색이 우선한다. 흔들림만 호출하면 화면색은 그대로 이어진다. 일시 효과가 끝날 때는 그 시점에 지정된 분위기가 보인다.

## 세 갈래 세로 참격

보스나 미니 보스의 공격 순서에 `Assets/Data/Patterns/SovereignTripleSlash.asset` 하나를 넣으면 세 줄이 함께 발동한다. 세 줄을 감싸는 폭 10.2m, 길이 15m의 직사각형 장판 하나가 1.15초 동안 표시된다. 장판이 사라질 때 폭 3m의 참격 경로 세 곳에 있는 병사를 한 번씩 판정한다. 경로의 중심 간격은 3.6m이며, 장판 안에서도 경로 사이의 0.6m 틈은 피해를 받지 않는다.

연출은 `SovereignTripleSlash.prefab` 하나로 재생한다. 세 칼날이 동시에 0.6초 동안 전진하고, 뒤에 남은 바닥 자국은 출발점부터 흐려진다.

장판부터 바닥 자국이 사라질 때까지 연출만 확인하려면 `Assets/Art/Prefab/Telegraphs/SovereignTripleSlashWithWarning.prefab`을 장판의 중심에 배치하고 재생한다. 파란색 로컬 Z축이 진행 방향이다. 실제 전투의 피해 판정은 위의 공격 패턴이 담당한다.

## 대검 내려찍기

`Assets/Art/Prefab/Telegraphs/SovereignSwordPlunge.prefab`을 타격 중심에 배치하면 원형 장판부터 재생한다. 장판이 1.15초에 완전히 사라진 뒤 대검이 나타나고, 1.51초에 검 끝이 바닥에 닿으며 충격파가 퍼진다.
