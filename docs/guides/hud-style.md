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

## 메인 메뉴 직접 편집

`MainMenu.unity`의 Main Menu Canvas는 씬에 저장된 uGUI다.
Widgets 아래 Title과 New Game / Continue / Quit 버튼의 RectTransform, Image,
Text를 직접 편집한다. Fade는 화면 전환용이므로 Widgets 바깥에 유지한다.
버튼의 On Click에는 MainMenu의 NewGame / Continue / Quit이 저장되어 있다.
실행 시 저장된 Canvas가 있으면 다시 만들거나 스타일을 덮어쓰지 않는다.
이어하기 활성화와 화면 전환 페이드만 게임 상태에 맞춰 갱신한다.
Canvas가 없는 이전 씬과 테스트는 기존 생성 경로를 사용한다.
