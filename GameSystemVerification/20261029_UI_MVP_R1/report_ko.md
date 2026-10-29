# UI 리팩터링 검증 기록 — G0 / R1 / G1

- 작성: 2026-10-29
- 프로젝트: `F:\GitCorp\Project-Abyss`
- 브랜치/커밋: `Default-Battle-Test` @ `43a58c8bf211a5d692a92c4495d483c3f489cce8`
- Unity: 6000.0.56f1 (URP)
- 기준 문서: `Project_Abyss_UI_Refactoring_Roadmap_20261029.md`, `전투 시스템 규칙(1029).md`
- 커밋/푸시: 하지 않음 (로컬 검증만)

## 1. G0 기준선

| 항목 | 결과 |
|---|---|
| C# 컴파일 오류 | 0건 (`error CS` 0, `ProjectAbyss.Runtime.dll` 재빌드 성공) |
| 기존 경고 | `LocalPackages\AbyssSecondaryRig\Runtime\SecondaryRigController.cs(14,55): CS0414` 1건 (R1과 무관, 이전부터 존재) |
| 기존 데이터 이슈 | 감정 증강 63개 로드 실패(0/63) — UI와 별개의 데이터/직렬화 문제로 분리 |
| 기존 콘텐츠 경고 | `[0922 Enemy DuelQuota][PENDING_CANONICAL] Requested=3, Candidates=0` — 정본 미정 데이터 경고, R1과 무관 |
| 씬 | `Assets/5. Scenes/Battle Test Scene.unity`, `Assets/5. Scenes/Run Flow Test.unity` |

## 2. R1 적용 상태 (재적용하지 않고 확인)

이미 로컬에 적용되어 있었다(미커밋). 로드맵 R1-1의 "이미 적용했다면 재적용하지 말고 먼저 확인한다"에 따라 검증만 수행.

| 파일 | 상태 |
|---|---|
| `Presentation/UI/Components/ActionSlotButtonUI.cs` | 수정 (View 전용화) |
| `Presentation/UI/Components/ActionSlotButtonPresenter.cs` | 신규 (표시 데이터 + 클릭 중재) |
| `Presentation/UI/Panels/BattleConfiguredSlotBoardUI.cs` | 수정 (View 전용화) |
| `Presentation/UI/Panels/BattleConfiguredSlotBoardPresenter.cs` | 신규 (레이블/입력 변환) |
| `Presentation/UI/Core/BattleUIRefreshScheduler.cs` | 수정 (Batch 위임) |
| `Presentation/UI/Core/BattleUIRefreshBatch.cs` | 신규 (Dirty 배치 + 스냅샷) |
| `Editor/Verification/BattleUiMvpRefactorVerification20261029.cs` | 신규 (검증 메뉴) |

계약 보존 확인:
- `ActionSlotButtonUI.Bind(BattleUIManager, Character, BodyPart, int, ActionSlot, bool)` 시그니처 동일
- `BattleConfiguredSlotBoardUI.Configure(BattleManager, BattleUIManager, RectTransform, Button, TMP_Text)` 시그니처 동일
- `[SerializeField]` 필드 목록/타입/순서 동일 (씬·프리팹 직렬화 유지)
- `OpenOwnerActionSlot`, `RemoveActionSlotAndContinueEditing` 호출 경로 유지
- `.meta` GUID 유지(기존 파일), 신규 파일은 자체 `.meta` 생성

## 3. 검증 결과

### 3-1. 스모크 테스트
메뉴 `Game System Verification > 20261029 UI MVP Refactor - Smoke Test`

```
RESULT PASS=13 FAIL=0
```
(Batch drain/take-snapshot, null 무시, 빈 슬롯 레이블, 인덱스 클램프, 보드 시그니처 등)

### 3-2. Play Mode 검증 (Battle Test Scene)

| 검증 항목 | 결과 |
|---|---|
| 전투 준비/턴 1 진입, UI 매니저·스케줄러 활성 | 정상 |
| 빈 슬롯 좌클릭 → 소유 부위/인덱스 선택 + 스킬 선택 모드 | 정상 (`mode=SelectSkill`, `owner=Olaf`, `part=HEAD`) |
| 계획 배치 후 슬롯 레이블 | `행동 #1 / 물어뜯기 / 정예 몹 · Head` |
| 대상 재지정(단일 HP 적) 후 레이블 갱신 | `훈련용 일반 적 / SINGLE HP` 로 갱신 |
| 배치된 슬롯 우클릭 → 제거 + 같은 인덱스로 편집 계속 | 정상 (슬롯 제거, `mode=SelectSkill`) |
| 제거 후 재바인드 레이블 | `빈 슬롯` 으로 복귀 |
| 슬롯 보드(런타임 인스턴스, 실제 플레이어 데이터) | 4개 항목 생성, `계획 완료 · 연결 HEAD · SPD 3 / 물어뜯기 → 정예 몹 Head · 빛 0` 표기 정상 |
| `MarkAllDirty()` → `Flush()` 반복 | 예외 없음, 중복 갱신 폭주 없음 |
| Play Mode 콘솔 오류 | 0건 (기존 `DuelQuota` 경고 1건만) |

증빙: `battle_planning_r1.png` (전투 계획 화면 캡처)

### 3-3. 관찰 사항 (결함 아님)
- 현재 씬의 실사용 입력 경로는 월드 슬롯 스트립(`BattleWorldActionSlotCellUI`)이고, 클래식 패널(`ActionSlotSelectorUI`/`SkillSelectPanelUI`)은 비활성 상태다. R1 View는 두 경로 중 클래식 패널 쪽에서 동작하며, `Bind` 후 좌/우클릭 계약은 위 표대로 확인했다.
- `BattleConfiguredSlotBoardUI`는 어떤 씬/프리팹에도 배치되어 있지 않다(현재 미사용 컴포넌트). 런타임 인스턴스로 기능만 확인했다.
- 우클릭 제거 직후 레이블은 재바인드 시점에 갱신된다. 이는 R1 이전 구현과 동일한 동작(레이블은 `Bind`에서만 생성)이다.

## 4. G1 판단

| 체크 | 판정 |
|---|---|
| 빈/사용 중 슬롯 선택, 스킬 선택, 조준, 취소, 재배치 일관성 | 예 |
| Command Service만으로 규칙 변경 가능(Presenter에 규칙 없음) | 예 |
| UI가 데미지·빛 지불 등 계산을 중복 수행하지 않음 | 예 |
| 이벤트 누락·중복 구독·씬 재진입 문제 재현 | 없음 |
| 반복 갱신 성능 저하/표시 지연 | 없음 (Dirty일 때만 갱신, 시그니처 비교로 불필요 재생성 감소) |
| UI 작업 시 `BattleUIManager`만 매번 수정하는 상황 | 해소(슬롯 View/Presenter 분리) |

**결론: G1에서 UI 아키텍처 리팩터링을 종료해도 된다.** R2/R3/R4는 구체적 유지보수 문제가 확인될 때만 착수한다.
