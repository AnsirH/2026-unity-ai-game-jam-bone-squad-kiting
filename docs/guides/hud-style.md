# 게임 HUD 스타일

`HudSkin.Apply`는 HUD 시작 시 기존 위젯에 어두운 패널과 황동색 테두리를 적용한다.
기존 버튼 이벤트, 진형 단축키, 체력과 분대 수 갱신은 `Hud`에서 그대로 처리한다.
상단 좌측은 분대 상태, 중앙은 보스 체력, 하단 중앙은 진형 카드다.
시작·결과 화면은 중앙 카드와 재시작/진행 버튼으로 구성한다.
테스트 정보는 실행 시 숨기며 F1로 토글한다.

씬의 기존 HUD 참조를 유지하므로 새 에셋이나 프리팹 교체 없이 게임 씬에 적용된다.

## 이미지와 폰트

`Assets/Resources/HudArtwork.asset`에 `Assets/Art/UI/VintageBone`의 스프라이트를 연결한다.
현재 스타일은 누렇게 삭은 뼈, 녹슨 철과 닳은 가죽이다. 이전 금속 스타일은
`Assets/Art/UI/DarkFantasy`에 보존한다.
중앙 패널·상태창·시작 버튼·진형 버튼·구분선을 각각 사용하며 글자는 별도 Text다.
진형 버튼의 오른쪽 영역에 키와 이름을 배치하고 선택 시 틴트를 밝게 한다.
생존 수·DPS·체력·결과 제목은 기존 HUD에서 계속 갱신한다.

한글은 `Assets/Art/UI/Fonts/NotoSerifKR-Regular.otf`, 영문 제목은
`Cinzel-Regular.ttf`를 사용한다. 같은 폴더에 각 OFL 라이선스를 보관한다.
메인 메뉴의 제목과 버튼에도 같은 폰트와 버튼 아트를 사용한다.

## 왕좌로 가는 길

1스테이지 사제 처치 후 화면 11시 방향에서 흰 뼈색의 `PORTAL` 해골 화살표가 움직인다. 게임 카메라에 워프가 처음 보이면 그 시도 동안 사라진다. 2스테이지 성문이 열리면 문 안팎의 두 화살표가 보스 안내와 비슷한 크기로 워프 방향을 가리킨다. 3스테이지를 시작하면 상단 중앙에서 같은 도안에 `BOSS`가 새겨진 화살표가 위아래로 움직인다. 전투 경고판이 열리면 그 아래에서 길을 안내한다. 게임 카메라에 서리기사의 발끝·다리 또는 상체가 처음 보이면 해당 시도의 안내가 끝난다. 카메라를 돌리거나 보스가 순간이동해도 다시 나오지 않으며, 스테이지를 재시도하면 다시 안내한다. 아이콘은 `ClassicBone/BossApproach.svg`, `PortalApproachNW.svg`(1스테이지), `PortalApproach.svg`(2스테이지)에서 편집한다.

## 메인 메뉴 직접 편집

`MainMenu.unity`의 Main Menu Canvas는 씬에 저장된 uGUI다.
Widgets 아래 Title과 New Game / Continue / Quit 버튼의 RectTransform, Image,
Text를 직접 편집한다. Fade는 화면 전환용이므로 Widgets 바깥에 유지한다.
버튼의 On Click에는 MainMenu의 NewGame / Continue / Quit이 저장되어 있다.
실행 시 저장된 Canvas가 있으면 다시 만들거나 스타일을 덮어쓰지 않는다.
이어하기 활성화와 화면 전환 페이드만 게임 상태에 맞춰 갱신한다.
Canvas가 없는 이전 씬과 테스트는 기존 생성 경로를 사용한다.
