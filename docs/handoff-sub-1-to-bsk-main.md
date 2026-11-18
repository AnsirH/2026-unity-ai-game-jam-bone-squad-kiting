# 핸드오프: sub-1 → bsk-main (2026-11-18)

> sub-1 세션이 bsk-main 세션에 전달하는 조율 노트. 세션 간 직접 메시징이 안 돼 git으로 전달한다.
> 반영·조율이 끝나면 이 파일은 지워도 된다.

## 무엇을 했나

M1 플랜을 사용자 요구 2건에 맞춰 개정했다. 브랜치 `sub-1`, 커밋 `441f8ce`.
병합은 **보류**(bsk-main 작업 중이라) — 준비되면 `git merge sub-1`. 문서(`docs/`)만 바꿔서 코드(`KiteSquad/`)와 경로가 안 겹친다(내용 충돌 없음).

### ① 패턴·진형 → ScriptableObject 데이터 (결정 #8)
- `PatternDefinition`/`AttackSequence`/`FormationDefinition` SO + 범용 평가기 `DangerShapeEval.InDanger(shape,…)`, `FormationSlots.Generate(def)`.
- 패턴/진형 추가 = 에셋 추가(코드 무변경). 새 기하/모드만 평가기에 1종 추가. 1:1 수치는 에셋 값 + EditMode 테스트로 고정.

### ② 자동공격 → RTS식 조작 (결정 #9, M1에 기능+구조 모두)
- 우클릭 스마트 커맨드(적 콜라이더 피킹→AttackTarget / 지면→Move), 어택땅 A+좌클릭→AttackMove.
- `IEnemy`/`IDamageable` + `EnemyRegistry`(M1=보스1, M2 잡몹 드롭인), `SquadOrder{Move,AttackMove,AttackTarget}`.
- `ShotSystem.Tick(units, registry, order, dt)`가 오더 참조해 타겟 획득. 자동 평타는 유지.
- M1엔 적이 보스뿐이라 두 명령 체감은 제한적(사용자 인지·승인). 구조는 M2에서 활성화.

변경 문서: `docs/decisions.md`(#2 일부 대체·#5 대체, #8·#9 추가), `docs/PLAN.md`(M1/M4·§5·§6), `docs/plans/2026-11-18-m1-kite-squad-port.md`(Task 2·3·4·5(FormationController)·5.5신설·6·7·8·9).

## 조율이 필요한 것 (사람 판단)

1. **결정 #8·#9는 아직 "제안됨"** — 기획/팀장 승인 후 "유효"로.
2. **설계 충돌**: 방금 작성한 `Assets/Scripts/Core/AttackPattern.cs`(하드코딩)를 #8의 SO 구조가 대체한다. #8 승인 시 교체 필요.
3. **경로 표기 이슈**: 플랜 문서가 아직 `main/KiteSquad/`·`cd main`·`main/.gitignore`를 쓴다. 저장소 루트가 `main/`으로 재구성되면서 한 단계 어긋났을 수 있음(루트 기준이면 `KiteSquad/`). 실제 레이아웃에 맞게 정정 바람 — sub-1이 임의로 고치면 bsk-main 작업과 충돌할까 봐 손대지 않았다.
