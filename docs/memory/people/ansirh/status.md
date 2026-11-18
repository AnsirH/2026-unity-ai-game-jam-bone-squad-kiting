# 허한결 (`ansirh`) 현재 상태

> 본인 또는 본인의 AI만 고친다. 레포에 올라가 팀이 함께 본다. 지난 작업은 쌓지 말고 최근 상태만 남긴다.
> 최종 갱신: 2026-11-18

## 담당

- 프로그래밍

## 진행 중

- M1(프로토타입 이식 + 조작/데이터 확장) 구현. 계획서: `docs/plans/2026-11-18-m1-kite-squad-port.md`

## 완료

- Unity 프로젝트 스캐폴딩(`bone-squad-kiting/`, 6000.3.28f1 URP), EditMode 테스트 어셈블리
- 패턴·진형 데이터화(결정 #8): `PatternDefinition`/`AttackSequence`/`FormationDefinition` + 에셋 13개, 장판 판정·진형 슬롯 생성기
- 적 추상화와 분대 오더(결정 #9의 토대): `IEnemy`/`IDamageable`/`EnemyRegistry`/`SquadOrder`
- EditMode 테스트 14개 통과

## 막힘

- 결정 #8·#9가 아직 "제안됨"(팀장 승인 전)인데 구현은 그 구조를 따르고 있다
- 2페이즈 임계값이 0.54(페이즈 전환)와 0.55(공격 간격) 두 가지다. 프로토타입 원본 대조 필요
- `origin/main`에 `Art 폴더 추가` 커밋이 있어 push 전에 병합해야 한다

## 다음 할 일

- Task 1: 아레나 씬 + 쿼터뷰 직교 카메라 + URP 포스트
- Task 5: 분대 이동(중심점 + 유닛 추종), 첫 PlayMode 테스트
- 이후 Task 6(입력) → 7(평타·보스) → 8(패턴) → 9(GameDirector) → 10(HUD) → 11(연출) → 12(통합)
