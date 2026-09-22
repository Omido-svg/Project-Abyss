# Project Abyss — 0922 Phase 1 Baseline Harness

> [0922_PHASE1_BASELINE_HARNESS]  
> Phase 0가 `CLOSED`인 프로젝트에서만 적용한다.

## 목적

Phase 1은 gameplay를 고치는 단계가 아니다.

현재 구현을 0922 정본에 대조하여:

- `PASS`
- `TARGET_GAP`
- `PENDING_CANONICAL`
- `HARNESS_FAIL`

로 분류하고, 각 `TARGET_GAP`을 이후 Phase 소유자와 연결한다.

## PhaseB legacy 처리

다음 기존 검증은 0922 정본과 충돌하므로 삭제하지 않고 historical regression으로만 유지한다.

- C-27 `phaseb.c27.common_status_syntax`
- C-48 `phaseb.c48.regeneration_dimensions`
- C-49 `phaseb.c49.opposite_status_algebra`

Phase 1 이후:

```text
DisplayName = [LEGACY BASELINE] ...
Required = false
```

따라서 Phase 2+의 올바른 0922 구현이 옛 expectation을 깨도 전체 Required gate를 실패시키지 않는다.

## Baseline 검사 범위

- Numeric independent entry
- opposite status preservation
- common cap 제거 여부
- finite / infinite duration
- Factory N/T 보존
- Deferred numeric identity
- Pain
- Fear
- Heat
- Stagnation
- Regeneration
- Designation
- Swift / TurnStart timing
- Protection/Rupture order independence
- Sturdy/Disarm aggregate axis
- PENDING_CANONICAL inventory
- legacy PhaseB isolation

## 실행

Unity 컴파일 후:

```text
Game System Verification
> 0922 Canonical
> Phase 1 - Run Baseline Harness
```

보고서:

```text
Logs/GameSystemVerification/0922_Phase1_Baseline.md
```

## 완료 기준

Phase 1은 모든 0922 규칙이 PASS여야 끝나는 단계가 아니다.

정상적인 완료 예:

```text
PASS > 0
TARGET_GAP > 0
PENDING_CANONICAL >= 0
HARNESS_FAIL = 0

PHASE1_RESULT=PASS_BASELINE_CAPTURED
```

`TARGET_GAP`은 Phase 2+에서 해결한다.

## Runtime 변경

없음.

Phase 1이 수정하는 production 파일은 기존 Verification metadata인
`PhaseBGameSystemVerificationModule.cs`뿐이며 gameplay probe 본문은 그대로 보존한다.
