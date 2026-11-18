# 게임 사운드 연결

Project 창에서 `Assets/Data/Audio/SoundDatabase.asset`을 열고 재생할 항목의 **Clip** 칸에 AudioClip을 넣는다. 각 항목의 **Volume**은 음량, **Pitch**는 재생 속도와 음높이를 조절한다. BGM의 **Loop**는 기본으로 켜져 있다.

| 목록 | ID | 재생 시점 |
| --- | --- | --- |
| Bgm | `bgm.stage` | 스테이지 1~4 시작 또는 재시도 |
| Bgm | `bgm.boss` | 보스 아레나 시작 또는 재시도 |
| Sfx | `squad.revive` | 병사 한 명의 부활 완료 |
| Sfx | `stage.clear` | 스테이지 승리 |
| Sfx | `squad.defeat` | 분대 전멸 |

기본 슬롯은 비어 있다. 필요한 것부터 채우면 된다. 다른 소리를 추가하려면 같은 목록 안에서 고유한 ID를 지정하고 게임 코드에서 대소문자까지 같은 ID로 호출한다.

```csharp
using BoneSquadKiting.Audio;

// 게임플레이 컴포넌트의 메서드 안에서 호출한다.
SoundManager.Current.PlaySfx("squad.revive");
SoundManager.Current.PlayBgm("bgm.boss");
SoundManager.Current.StopBgm();
```

Unity가 플레이 시작 시 `Assets/Resources/Audio/SoundManager.prefab`을 한 번 불러온다. 씬을 직접 실행해도 같은 설정을 사용한다. BGM을 바꾸거나 멈추면 기본 0.5초 동안 음량이 전환된다. 같은 곡을 재생 중에 다시 요청하면 이어서 재생하며, 처음부터 들으려면 `PlayBgm(id, restart: true)`를 호출한다.

효과음은 프리팹의 **Sfx Pool → Max Voices** 개수까지 겹쳐 재생한다. 기본값은 16개이며 모두 사용 중이면 순서대로 한 자리를 재사용한다. 음악과 효과음의 전체 음량은 `Assets/Data/Audio/SoundMixer.mixer`의 BGM·SFX 그룹에서 조정한다.
