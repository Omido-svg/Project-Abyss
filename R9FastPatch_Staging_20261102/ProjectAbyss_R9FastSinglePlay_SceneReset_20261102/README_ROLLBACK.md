# Project Abyss R9 Fast Single-Play Regression (2026-11-02)

## 적용
- Assets/ 경로의 R9FullRegressionRunner20261102.cs 와 .meta 원본 경로.
- 새 메뉴: Game System Verification > R9 - Start FULL Fast Single-Play Regression
- EditMode Gate 30회, PlayMode Gate 39회 (한 번의 PlayMode 진입).
- PlayMode 사이클 반복 대신 런타임에서 Battle Test Scene을 38회 다시 로드함.
- 매 Gate 별 CSV/LOG/예외 정책 유지. 원본 Dirty 씬을 저장하지 않음.
- R7.Stage0Ledger 및 R7.Stage1Boundary 중복 등록 2회 제거.

## 검증
- 69/69 Gates PASS (Edit 30 / Play 39), Assertions 1325/1325 PASS.
- 실행 시간 2026-11-02 10:07:42 ~ 10:10:34 UTC, 약 2분 52초.
- 테스트 후 EditMode/Dirty=true 복귀 확인.
- 이전 71 Gate/1349 Assertions의 차이: 중복 Edit 2 Gate/24 Assertions.
- 새 방식의 런타임 씬 재로드는 디스크의 Battle Test Scene을 읽으므로
  미저장 씬 편집 내용은 매 Gate의 런타임 리로드에 반영되지 않음.
- 변경 범위는 R9 실행기 스크립트 1개와 기존 .meta 보존. SO/GUID 변경 없음.
- 커밋/푸시 금지 규칙 준수.

## 설치
이미 MCP로 해당 프로젝트에 적용한 상태이므로 다시 설치할 필요 없음.
별도 프로젝트 적용 시 ZIP 내 Assets/ 이하 경로를 해당 Assets/에 덮어쓰기.

## 롤백
1. PlayMode 종료.
2. Backup/BeforeChange/R9FullRegressionRunner20261102.cs 를
   Assets/1. Scripts/Editor/R9/R9FullRegressionRunner20261102.cs에 복원.
3. GUID 보존을 위해 .meta 유지 또는 백업본으로 복원.
4. Unity Refresh/Compile. Battle Test Scene은 저장하지 않음.
5. 기존 결과 로그는 Backup/BeforeChange에 보존됨.
