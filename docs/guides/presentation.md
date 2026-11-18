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
