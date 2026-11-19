# Unity 구현 대기 — 규칙서 ↔ 코드 대조

`Battle-Logic` 브랜치(맥 worktree `~/Project/Project-Abyss-Battle`)에서 할 구현 작업 목록. 11-18 첫 대조 기준. **끝난 항목은 지운다** — 남아 있는 항목 = 미결.

- 정본은 규칙서. 코드가 규칙서와 다르면 코드를 고친다
- 확인 도구 — 시스템 검증(`GameSystemVerificationRunner`, Phase A~F)은 Production `BattleRuntimeFactory`로 조립돼 정상. 캐릭터 검증(`CharacterVerificationRunner`)은 6번(검증 도구 개편) 전까지 도사림 결과를 믿지 말 것
- 구현 중 판단이 필요한 지점(규칙 해석 · 적용 범위 · (미정) 칸 임시값)은 구현 전에 사용자에게 묻는다

## 진행 순서 (1층 플레이테스트 기준)

### 2. 히후미 남은 규칙
- **C3 속임수 자동 선택 기준** — 선택 UI · 로직은 11-19 완료. 자동 계획(자동 플랜 · AI)은 지금 **항상 시고로**. 사용자 답 = 「상황별」 — 기준(예: 만개까지 남은 뼈 N 이상 · 체력 X% 이상이면 대실패)은 미정
- **위세 발동 UI는 임시**(`BattlePrestigeButtonUI` — 오른쪽 가운데 버튼, 11-19). 코더 UI로 대체 예정. ⚠ **조준이 필요한 위세(빌야르 룬 새기기 · 베르세르크 등)의 대상 선택이 없다** — 지금은 살아 있는 첫 적의 첫 생존 부위로 넘긴다
- **히후미 로드아웃 도사림 칸이 시작 세트와 다르다** — 지금 포커페이스 · 뼈에 새긴다 · 살은 내주지 / 규칙서 13.1 = 웅크리기 · 포커페이스 · 뼈에 새긴다(C10에서 같이)

### 3. 공용 규칙
- **C4 침체(11.2)** — 코드는 위세 게이지만 깎는다(`CharacterStatusController.ApplyTurnEndPrestigeDelta`). 규칙 = 게이지 0이라 못 깎은 만큼 전체 체력 피해 · 게이지 없는 적은 늘 0 · 히후미는 피격 적립
- **C7 플레이어 체력 내성(6.5)** — 코드에 없음. 빌야르 타 / 관 / 절 · 명성 관 / 절 / 타 · 히후미 절 / 타 / 관(저항 / 중립 / 약점)
- **C8 잡몹 공격 유형(12.3)** — 잡몹 하나 = 유형 하나. 데이터 확인 필요

### 4. 데이터 점검
- **C10** — 시작 지급 세트(13.1) · 빌야르 굴림 유형(17.7) · 기계 보스 냉각 카드 = 해결 도사림(12.5)
  - 히후미 로드아웃은 평타 3칸(동귀어진 추가함) 확인. 결투 · 도사림 칸은 미확인

### 5. 일괄 정리 (영향 범위 넓음 — 따로 묶는다)
- **C9 노려보기 삭제(8장 · 11-17)** — 코드 5곳 · 에셋 6개(`common.preparation.glare` · `Olaf_Glare` · TEMP_BALANCE `Common_Glare` 등). 히후미 · 올라프 · 유진 런타임 스킬 목록에 아직 있다
- **C11 이름** — 올라프 → 빌야르, 유진 → 명성. 코드 · 표시 이름 · 검증 프로필 이름(`올라프_Verification` 등)

### 6. 검증 도구 개편
- **캐릭터 검증 강제 실행 커버리지** — 효과를 옛 Entry(`TempBalanceSkillProxyEffectDefinition`)로 하나씩 직접 실행해서, native 라우팅으로 도는 도사림 15건이 「지원하지 않는 Effect 계약」으로 실패(`CharacterVerificationForcedScenarioRunner.cs` `VerifyEffectEntry`). native 라우팅 스킬은 실제 경로(`SkillEffectDispatcher`)로 실행하고 결과(방어도 · 빛 · 상태)를 보는 방식으로
  - 실패 목록 — 공용 웅크리기 · 노려보기(3캐릭터) / 빌야르 피의 서약 · 피로 사는 숨 · 광기를 가라앉히다 · 흐트러짐을 삼키다 · 상처를 지우다 · 최후의 저항 / 명성 숨을 죽이다 · 칼을 갈다 · 기회를 엿보다
  - `coverage.manifest.closed_world`(미등록 Effect)도 같은 원인
  - 5번 뒤에 두는 이유 — 노려보기 삭제 · 이름 변경이 검증 프로필과 스킬 목록을 건드린다
- 11-18 — 캐릭터 검증 Fixture를 Production `BattleRuntimeFactory`로 바꿔 실행 오류 17 → 0(통과 → 실패 0건)

### 7. 판단이 필요한 검증 (B)
- **빌야르 평타 기본 잔효과(`phased.olaf.o01`)** — 코드(`OlafMadnessMechanic.OnExchangeResolved`)는 적중 출혈 1/2 · BLUE 평타 승리 광기 +1을 규칙대로 구현. 검증은 RED 0/0/0 · BLUE 광기 0 — 검증 Fixture가 피해 정보 없는 가짜 교환을 만드는 것으로 보인다. 실전 확인 필요
- **살수의 감 재굴림(`phased.yujin.y05`)** — 규칙 = 결투 한정 · 진 교환에서 슬로우 → 「감 1로 다시 굴림 / 아낌」 선택. 검증은 옛 「공격 슬롯 예약 재굴림」을 기대. 슬로우 선택 UI까지 구현됐는지 확인

### 8. 그 밖
- 히후미 결투 3장(도발형 · 전체 정화 · 판을 엎다) — 캐릭터 검증에서 자원을 채워도 `CanUseByResource = false`. 6번 개편 뒤 다시 볼 것
- 동귀어진 연출 = **임시로 `푼돈 걸기_Visual` 참조**(11-18). 전용 연출 · 애니메이션은 코더 몫
- 연출(`SkillVisualDefinition`) 없는 카드 8장 — 판돈을 올리다 · 곱절을 노리다 · 운명을 흔들다 · 몸에 새기다 · 굴림 도감 · 판을 엎다 · 도발형 · 전체 정화형
- 굴림 텍스처 검증(Phase E) — 빌야르 결투 5 · 평타 10장의 위치별 평균이 기준과 0.1~2 어긋남(TEMP_BALANCE 데이터 · 「0917 authority pending」). Phase E 실행은 `PhaseEContentManifest.asset`을 덮어쓰므로 결과를 커밋하지 말 것

## 윈도우에서 할 것
- **명세 xlsx 재생성** — `abyss-workspace/tools/spec/spec_build.ps1`(Excel COM). 11-18 `spec_data.ps1` 문구 갱신분(대실패 자해 문턱 · 과감한 판단 완전 차단 · 동귀어진 순서 · 파괴 권한)이 아직 xlsx에 없다
- **Battle-Logic 작업 환경 세팅**(윈도우에서 구현을 이어갈 때)
  - Unity **6000.0.56f1** 설치(다른 버전으로 열면 프로젝트가 업그레이드된다)
  - worktree 생성 — `git worktree add <경로> Battle-Logic`(원 폴더는 Game-Design 유지)
  - `.git/info/exclude`에 `CLAUDE.md` · `AGENTS.md` · `GameSystemVerification/` 추가(이 브랜치의 .gitignore는 안 막는다)
  - `.vscode/settings.json` — worktree 폴더 이름으로 솔루션명이 바뀌면 `git update-index --skip-worktree .vscode/settings.json`
  - MCP for Unity 연결 — Unity `Window → MCP for Unity`에서 Transport **Stdio** → Client Claude Code → Configure(uv 필요)
- **sim_py 측정**이 필요하면 윈도우 쪽이 빠르다(쳇 번들 포함) — 지금 걸린 측정 없음

## 기획 쪽 연결 항목
- sim_py HF-B03 동귀어진 — sim 20 · 빛 0 / Unity 10 · 빛 1(`작업/히후미.md` 수치 대기)

## 작업 메모
- Unity를 플레이하면 TMP 동적 폰트(`Assets/7. Fonts/TheOneLord SDF.asset` · `NotoSansKR-VariableFont_wght SDF.asset`)가 채워진다 — 커밋 전에 되돌린다
- 테스트 씬 캐릭터 선택 = PlayerPrefs `ProjectAbyss.TestEncounter.Player`(0 빌야르 · 1 명성 · 2 히후미)
- `Battle Test Scene` 단독 실행은 RunProgression이 없다(런 단위 기능은 `Run Flow Test`에서)
