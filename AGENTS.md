# 프로젝트 맥락 (bsk-project)

> 이 절은 팀원 모두의 AI(Claude Code, Codex, ChatGPT 등)가 공유하는 프로젝트 공통 메모리다.
> 팀 합의로만 바꾼다. 개인 지시나 개인 작업 메모는 넣지 않는다 (아래 "개인 메모리").
> 이 파일 전체를 200줄 이내로 유지하고, 자세한 내용은 `docs/`로 넘긴다.

## 프로젝트

- **한 줄 설명:** (정하면 채움)
- **기간·마감:** (정하면 채움)
- **팀:** 박영우 (`zzazan-bro`, 기획·팀장), 허한결 (`ansirh`, 프로그래머), 김준성 (`JS66KIM`, 아티스트), 심영환 (`Shim0Hwan`, 아티스트)

## 명령

AI가 작업하면서 직접 실행하는 명령이다. 코드를 바꾸면 "검증"을 돌린 뒤에 끝났다고 말한다.

- 설치: (정하면 채움)
- 실행: (정하면 채움)
- 검증: (정하면 채움)

## 어디를 보면 되나

| 알고 싶은 것 | 문서 |
| --- | --- |
| 방향성, 범위, 하지 않는 것 | `docs/memory/direction.md` |
| 마일스톤과 현재 단계 | `docs/memory/milestones.md` |
| 기획 전체 | `docs/PLAN.md` |
| 지금까지 내린 결정과 이유 | `docs/decisions.md` |
| 팀원별 현재 작업 | `docs/memory/people/<깃허브 닉네임>/status.md` |
| 용어 | `docs/memory/glossary.md` |
| AI 사용 기록 | `docs/memory/ai-usage.md` |

## 절대 원칙

1. (정하면 추가)

## 작업 규칙

- 커밋 메시지 형식: `feat:` `fix:` `docs:` `chore:` + 영어 명령문
- 기획을 바꾸는 결정은 `docs/decisions.md`에 기록하고 `docs/PLAN.md` 해당 장을 같은 커밋에서 고친다.
- 기능 동작을 바꾸는 코드는 관련 문서를 같은 커밋에서 고친다.
- 화면 문구는 영어, `docs/` 내부 문서는 한국어

## AI가 작업할 때

- 작업 전에 `docs/memory/milestones.md`와 담당자의 `docs/memory/people/<깃허브 닉네임>/status.md`를 읽고 현재 단계를 확인한다.
- 작업 세션이 끝나면 담당자의 `status.md` 갱신을 제안한다. 다른 사람의 폴더(`people/<다른 닉네임>/`)는 고치지 않는다.
- 개인 작업 메모는 `people/<닉네임>/notes.md`에 둔다. 이 파일은 gitignore 처리되어 레포에 올라가지 않는다.
- AI로 코드·문서·이미지를 만들었으면 `docs/memory/ai-usage.md`에 한 줄 추가를 제안한다.
- 결정은 사람이 내린다. AI가 제안한 결정은 `docs/decisions.md`에 "제안됨"으로 적고, 승인되면 "유효"로 바꾼다.
- 이 절과 `docs/memory/direction.md`는 사람이 합의해서 바꾼다. AI는 수정을 제안만 한다.

## 개인 메모리 (레포에 올리지 않음)

- **Claude Code:** 이 프로젝트에만 적용할 개인 지시는 `CLAUDE.local.md`(gitignore 처리됨), 모든 프로젝트에 적용할 개인 지시는 `~/.claude/CLAUDE.md`에 둔다. 자동 메모리는 각자 컴퓨터에만 저장된다.
- **ChatGPT 등 레포를 자동으로 읽지 않는 도구:** 대화를 시작할 때 이 파일과 필요한 문서를 첨부한다.
