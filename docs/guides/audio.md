# 게임 사운드 연결

Project 창에서 `Assets/Data/Audio/SoundDatabase.asset`을 열고 재생할 항목의 **Clip** 칸에 AudioClip을 넣는다. 각 항목의 **Volume**은 음량, **Pitch**는 재생 속도와 음높이를 조절한다. BGM의 **Loop**는 기본으로 켜져 있다.

| 목록 | ID | 재생 시점 |
| --- | --- | --- |
| Bgm | `bgm.stage3` | 메인 메뉴 진입 또는 복귀 |
| Bgm | `bgm.stage` | 1스테이지 진입 |
| Bgm | `bgm.stage2` | 2스테이지 진입 |
| Bgm | `bgm.boss` | 보스 스테이지 진입 |
| Sfx | `squad.revive` | 부활 중 뼈가 한곳으로 모이기 시작할 때 |
| Sfx | `stage.clear` | 스테이지 승리 |
| Sfx | `squad.defeat` | 분대 전멸 |

데이터베이스에는 배경음악 4곡과 효과음 21개가 등록되어 있다. 게임 씬의 **Stage Run → Stages → Bgm Id**에서 각 스테이지의 곡을 지정한다. 이어하기와 재시도도 해당 스테이지의 설정을 사용한다.

`squad.revive`는 뼈가 모여 조립되는 소리를 재생한다. `stage.clear`와 `squad.defeat`는 전용 클립이 없어 비어 있다. 추가한 효과음은 분대(`squad.*`), 적(`enemy.*`), 보스(`boss.*`)로 구분하며 게임 코드에서 데이터베이스와 같은 ID로 호출한다.

```csharp
using BoneSquadKiting.Audio;

// 게임플레이 컴포넌트의 메서드 안에서 호출한다.
SoundManager.Current.PlaySfx("squad.revive");
SoundManager.Current.PlayBgm("bgm.boss");
SoundManager.Current.StopBgm();
```

메인 메뉴와 게임 씬에는 `Assets/Resources/Audio/SoundManager.prefab`이 배치되어 있다. 씬을 바꿔도 처음 시작한 매니저가 계속 재생한다. 스테이지 클리어 화면에서는 현재 곡이 이어지고, 다음 스테이지나 메인 메뉴에 들어가면 0.5초 동안 이전 곡의 음량이 줄면서 새 곡의 음량이 커진다. 전멸 시에는 음악이 페이드아웃된다. 같은 곡을 처음부터 다시 들으려면 `PlayBgm(id, restart: true)`를 호출한다.

효과음은 프리팹의 **Sfx Pool → Max Voices** 개수까지 겹쳐 재생한다. 기본값은 16개이며 모두 사용 중이면 순서대로 한 자리를 재사용한다. 음악과 효과음의 전체 음량은 `Assets/Data/Audio/SoundMixer.mixer`의 BGM·SFX 그룹에서 조정한다.

## 전투 효과음의 재생 시점

| 동작 | 효과음 ID | 기준 |
| --- | --- | --- |
| 석궁 발사 | `squad.crossbow.fire` | 병사의 상체 사격 클립 8프레임(30fps)에 `AttackRelease`가 대기 중인 화살을 발사할 때. 분대장의 창은 제외한다. |
| 화살 명중 | `squad.arrow.hit.armor`, `squad.arrow.hit.humanoid` | 화살이 살아 있는 잡몹이나 미니 보스에 닿을 때 한 종류를 고른다. **Armored**를 켠 적(투석기·투창병)은 금속음, 나머지는 둔탁한 명중음이다. |
| 진형·피격 | `squad.formation.command`, `squad.hit.bones` | 유효한 진형 명령을 받으면 기합이 난다. 피해를 받아 병사가 쓰러지는 순간에는 뼈가 흩어지는 소리가 난다. |
| 부활 | `squad.revive.magic`, `squad.revive` | 부활을 시작하면 시전자의 마력음이 난다. 진행률 84%에서 궤도를 돌던 뼈들이 한곳으로 모이기 시작하면 조립음이 난다. 그 전에 시전자가 쓰러지면 조립음은 생략한다. |
| 불마법사 | `enemy.firemage.scorch`, `enemy.firemage.eruption` | 장판이 보이기 시작할 때 그을림, 장판이 발동할 때 분출. 병사가 피했어도 들린다. |
| 투창 | `enemy.javelin.shout`, `enemy.javelin.flight` | 일반 투창병은 투척 클립의 8프레임(30fps)에 기합·비행음·투사체가 함께 나간다. 모델이 없는 창투척 미니 보스는 W 양옆 띠 장판의 시전 시작을 기준으로 삼는다. |
| 투창 분열·착지 | `enemy.javelin.split`, `enemy.javelin.impact` | 창투척 미니 보스 W의 마지막 경고 구간에 분열음, 양쪽 띠의 피해 판정에 착지음이 난다. 일반 투창병의 직선 투사체에는 분열음이 없다. |
| 투석기 | `enemy.catapult.launch`, `enemy.catapult.flight`, `enemy.catapult.impact` | Fire 클립의 0.575초 이벤트에 나무 팔 소리가 난다. 돌이 팔을 떠날 때 비행음, 목표 지점에 닿아 파편이 튈 때 충돌음이 난다. |

투창 미니 보스의 W는 게임에서 양옆 띠 장판으로 표현한다. 분열하는 창 모델이 없어 분열음을 마지막 경고에 맞췄다. 여러 병사가 동시에 쏠 때는 같은 발사음이나 명중음을 0.06초 안에 한 번만 재생한다.

효과음의 **Start Offset**은 원본 앞의 무음을 건너뛰는 시간(초)이다. 아군과 적 군대 효과음 16개의 파형을 측정해 각각 0.026~0.073초를 지정했다. 클립을 교체하면 새 파일에 맞춰 조정한다.
