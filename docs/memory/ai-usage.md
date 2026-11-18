# AI 사용 기록

> AI로 코드·문서·이미지를 만들었으면 한 줄 추가한다. AI는 추가를 제안하고 사람이 승인한다.

| 날짜 | 누가 | 도구 | 무엇을 | 어디에 | 검토 |
| --- | --- | --- | --- | --- | --- |
| 2026-11-18 | ansirh | Claude Code | M1 구현 계획서 작성·개정(결정 #8·#9 반영, 실제 프로젝트 경로·네임스페이스·Input System에 맞춤) | `docs/plans/2026-11-18-m1-kite-squad-port.md`, `docs/PLAN.md`, `docs/decisions.md` | 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | M1 Task 0·2·3·4·5.5 코드와 테스트(패턴·진형 데이터 타입, 장판 판정, 진형 슬롯 생성, 적 추상화·오더), 데이터 에셋 13개와 생성 도구 | `bone-squad-kiting/Assets/Scripts/`, `Assets/Data/`, `Assets/Editor/`, `Assets/Tests/EditMode/` | EditMode 테스트 14개 통과, 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | M1 Task 5~9 코드와 테스트(분대 이동, 평타·투사체, 보스 패턴 시스템, GameDirector, 입력) | `bone-squad-kiting/Assets/Scripts/`(`Squad/`, `Boss/`, `Game/`, `Input/`, `Combat/`), `Assets/Tests/` | EditMode 17·PlayMode 22 통과, 실제 플레이 확인 전, 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | 아레나 씬과 생성 도구, 장판·투사체 임시 비주얼, 씬 스모크 테스트, 정지 시에만 사격하는 규칙 | `bone-squad-kiting/Assets/Scenes/Arena.unity`, `Assets/Editor/ArenaSceneBuilder.cs`, `Assets/Prototype/`, `Assets/Scripts/`, `Assets/Tests/` | EditMode 29·PlayMode 26 통과, 스크린샷 확인, 사람 플레이 확인 전 |
| 2026-11-18 | ansirh | Claude Code | M2 잡몹 떼 사전 조사·설계 초안, 결정 #11~#17 기록(잡몹 이동·타겟팅, 오더별 사격, 클릭 판정, 멈춘 유닛만 사격, 진형 회전, 분대 사거리). 클릭 판정은 OpenBW 소스와 스타2 모드 문서를 조사해 비교 | `docs/plans/2026-11-18-m2-mob-swarm-design.md`, `docs/decisions.md`, `docs/PLAN.md`, `docs/plans/2026-11-18-m1-kite-squad-port.md` | 수치는 계산 추정이며 측정 전, #14~#17은 ansirh 결정, #11~#13은 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | 스테이지 구조·에픽 몬스터 기획 초안과 마일스톤 재구성안, 결정 #18~#23 기록 | `docs/plans/2026-11-18-stage-structure-design.md`, `docs/PLAN.md`, `docs/decisions.md`, `docs/memory/` | 뼈대(#21)만 ansirh 결정, 세부와 수치는 사람 검토 전·플레이 검증 전 |
| 2026-11-18 | ansirh | Claude Code | 인원수 기반 진형·재배정·패턴 축소, 원근 쿼터뷰와 가장자리 스크롤 카메라, uGUI HUD, 연출, 전체 경기 자동 테스트 | `bone-squad-kiting/Assets/Scripts/`(`Core/`, `Squad/`, `Cameras/`, `UI/`, `Fx/`), `Assets/Editor/`, `Assets/Tests/`, `Assets/Scenes/Arena.unity` | EditMode 113·PlayMode 46 통과, 스크린샷 확인, 사람 플레이 확인 전 |
| 2026-11-18 | ansirh | Claude Code | 마감 일정에 맞춘 작업 순서, 결정 #24~#28 기록(클리어 조건, 부활 액션, 아티스트 제작 맵, Flow Field), Flow Field 설계 추가 | `docs/decisions.md`, `docs/memory/milestones.md`, `docs/plans/2026-11-18-m2-mob-swarm-design.md` §12 | 구현량·시간은 추정, 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | 아티스트용 맵 제작 안내, 분대 경계 이동 보완 설계(자리 배정·사선 경계·벽 우회), 웨이브 설계 제안, 결정 #29~#36 기록 | `docs/guides/map-authoring.md`, `docs/plans/2026-11-18-squad-boundary-movement.md`, `docs/plans/2026-11-18-stage-structure-design.md`, `docs/decisions.md` | 코드를 읽고 쓴 것이며 에디터에서 실행해 보지 않음, 사람 검토 전 |
| 2026-11-18 | ansirh | Claude Code | 스테이지 설정 설계(미니 보스 위치·연출, 부활, 분대장), 결정 #37~#42 기록 | `docs/plans/2026-11-18-stage-setup-design.md`, `docs/decisions.md`, `docs/memory/` | 설계는 제안이며 실행 전, 사람 검토 전 |
