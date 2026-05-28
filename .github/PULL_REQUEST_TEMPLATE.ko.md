<!-- markdownlint-disable-file MD041 MD013 -->
<!--
참조용 한국어 PR 템플릿입니다.
GitHub 은 `.github/PULL_REQUEST_TEMPLATE.md` (영어, default) 만 자동 채워 줍니다.
한국어로 PR 을 작성하려면 이 파일을 직접 복사해 붙여넣으세요.
-->

## 요약

<!-- 1-3줄로 변경 요약 -->

## 변경 범위

- [ ] CLI (Go) — `cmd/`, `internal/`
- [ ] Connector (C#) — `AgentConnector/`
- [ ] Docs — `docs/`, `README*.md`, `AGENT.md`, `CLAUDE.md`
- [ ] CI / 빌드 — `.github/`, `scripts/`, `.golangci.yml`

## 버전 bump

- [ ] CLI 코드 변경 → `git tag vX.Y.Z` 준비 (이 PR 에서는 태그 누르지 않음)
- [ ] Connector 코드 변경 → `AgentConnector/package.json` 버전 갱신
- [ ] 양쪽 모두 변경 → 둘 다 bump
- [ ] `CHANGELOG.md` `## [Unreleased]` 섹션 업데이트

## 자동 검증 (필수)

- [ ] `go build ./...` 통과
- [ ] `go vet ./...` 통과
- [ ] `go test ./...` 통과
- [ ] `golangci-lint run ./...` 통과
- [ ] `golangci-lint fmt --diff` 변경 없음
- [ ] `gofmt -w .` 후 diff 없음

## 수동 통합 검증 (Unity Editor 열고)

- [ ] `hera-agent-unity status` — state 표시 정상
- [ ] `hera-agent-unity doctor --json` — `unity.instances[0].stale == false`
- [ ] `hera-agent-unity list --names` — 도구 목록 정상
- [ ] `hera-agent-unity exec "return Application.dataPath;"` — 경로 반환
- [ ] `hera-agent-unity console` — 응답 정상
- [ ] `hera-agent-unity editor refresh --compile` — 컴파일 완료까지 대기
- [ ] `hera-agent-unity editor play --wait && hera-agent-unity editor stop` — Play 사이클 정상

## 회귀 위험 검토

- [ ] CLAUDE.md "Hard Constraints" 목록과 충돌하는 변경 없음
- [ ] `s_LockTimeout`, `humanCategories`, `splitArgs`, `gocritic` 제외 등 의도된 설계 유지
- [ ] 응답 envelope 형식 (`success` / `message` / `code` / `data` / `timings`) 변경 없음
- [ ] Heartbeat atomic write 패턴 (`.tmp` → `File.Replace`) 유지
- [ ] 새 의존성 추가 시 명시적 사유

## 관련 이슈 / 컨텍스트

<!-- Closes #N, Refs #M -->
