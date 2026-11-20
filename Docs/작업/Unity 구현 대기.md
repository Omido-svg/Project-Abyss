# Unity 구현 대기 — 규칙서 ↔ 코드 대조

`Battle-Logic` 브랜치(맥 worktree `~/Project/Project-Abyss-Battle`)에서 할 구현 작업 목록. 11-18 첫 대조 기준. **끝난 항목은 지운다** — 남아 있는 항목 = 미결.

- 정본은 규칙서. 코드가 규칙서와 다르면 코드를 고친다
- 확인 도구 — 시스템 검증(`GameSystemVerificationRunner`, Phase A~F)은 Production `BattleRuntimeFactory`로 조립돼 정상. 캐릭터 검증(`CharacterVerificationRunner`)은 6번(검증 도구 개편) 전까지 도사림 결과를 믿지 말 것
- 구현 중 판단이 필요한 지점(규칙 해석 · 적용 범위 · (미정) 칸 임시값)은 구현 전에 사용자에게 묻는다

## 진행 순서 (1층 플레이테스트 기준)

### 2. 히후미 남은 규칙
- **C3 속임수 자동 선택 기준** — 선택 UI · 로직은 11-19 완료. 자동 계획(자동 플랜 · AI)은 지금 **항상 시고로**. 사용자 답 = 「상황별」 — 기준(예: 만개까지 남은 뼈 N 이상 · 체력 X% 이상이면 대실패)은 미정
- **위세 발동 UI는 임시**(`BattlePrestigeButtonUI` — 오른쪽 가운데 버튼, 11-19). 코더 UI로 대체 예정. ⚠ **조준이 필요한 위세(빌야르 룬 새기기 · 베르세르크 등)의 대상 선택이 없다** — 지금은 살아 있는 첫 적의 첫 생존 부위로 넘긴다

### 4. 데이터 점검
- **빌야르 「틈을 파고들다」 굴림 수** — 규칙서 17.2 · 17.7은 1굴림, 에셋(`Olaf_N_11`)은 3굴림(타입은 관통으로 맞음). 굴림 수를 1로 줄일지 결정 대기

### 6. 검증 도구 개편 (캐릭터 검증 `CharacterVerificationRunner`)
11-20 실측(Play 모드) — 빌야르 37/78 · 명성 33/67 · 히후미 45/51 통과. 시스템 검증(Phase D)은 실제 전투 흐름이라 별개로 계속 통과 중
- ✅ 허용 목록 — 캐릭터 rider 4종(`TempBalanceSkillProxy` · `OlafMadness` · `YujinRider` · `YujinFinisher` EffectDefinition)이 「지원하지 않는 Effect 계약」으로 떨어지던 것을 허용 목록에 넣었다(27→37 · 28→33 · 43→45)
- ✅ 프로필 재생성 — 없는 스킬의 저장 케이스 22개 삭제(빌야르 10 · 명성 12, 「검증 Fixture 누락」 0건). 적 프로필 3개는 변화 없음
- ✅ ⑤ 데이터 — 같은 skillId 중복 4건(표준 · 블로토 · 환형 공격/수비)을 에셋 하나로 합쳤다(11-20). 자원을 채워도 못 쓰던 5장은 전부 규칙상 잠금이었다 → 검증기가 조건을 만들어야 한다(①에 포함)
  - 닥치는 대로 · 끝장을 보다 = 빛 4(기본 최대치 3, 열광 레벨업 뒤에만) / 최후의 저항 = 「지난 턴 짓눌림」 하드 게이트 / 히후미 도발형 · 전체 정화 · 판을 엎다 = 수치 (미정)이라 코드가 막아 둠 → 검증은 「막혀 있어야 정상」으로
- **빌야르 카드 수치가 규칙서 17장과 어긋난다** — 런 획득 풀의 TEMP_BALANCE 에셋이 굴림 형식은 같은데 값이 1~3씩 다르다(끈질기게 규칙 14 · 15~22 / 16~21 / 15~22 ↔ 에셋 12 · 13~20 / 14~19 / 13~20 · 단칼 규칙 17~22 ↔ 20~25 · 미쳐물어뜯기 13~20 ↔ 12~19 · 피의돌진 13~20 / 9~16 ↔ 14~21 / 10~17 등). 굳히기는 일치. 몇몇 카드엔 규칙에 없는 TEMP 프록시 효과(사용시 출혈 등)도 붙어 있다. 전체 대조 → 일괄 수정 필요(명성 · 히후미도 같은 확인)
- 코더 시절 고정 감사(`SkillIdentityVerification` R7-2 · 스킬 164장 집계 · `AbilityCoreFreezeGuard`)는 데이터를 바꾸면 깨지는 기준선 검사다 — 노려보기 삭제 · 중복 정리로 깨졌다. 갱신하거나 폐기할지 결정 대기
- **③ 옛 기대값** — 스킬 수 11/12(옛 로드아웃 기준) · 「머리 슬롯 위세 허용」(위세는 슬롯을 안 쓴다, 9장) · 명성 환형 로드아웃 · 무기 교체 시점 · 추격 상한 · 표식 44 발화 · 살수의 감 재굴림 / 빌야르 굴림 기준값(Dice 계약 11건) · 일방 공격 잔효과(`one_sided_unique_effects_disabled`). 규칙서와 대조해서 고친다
- **① `NotScheduled` 약 45건** — 효과를 하나씩 따로 실행하는데 굴림 위치 · 승패 조건을 만들어 주지 않는다(`CharacterVerificationForcedScenarioRunner.VerifyEffectEntry`). 효과별 상황 구성이 필요 — 크다. 「실제 경로 1회 실행 + 시스템 검증에 맡김」으로 줄일지 결정 대기
- **② 「Applied인데 상태 변화 없음」 약 12건** — 지문(`CaptureFingerprint`)이 무기 디버프 · 감 · 지정 등을 안 본다. 진짜 무효과인지 하나씩 확인

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
- **코드 안 이름은 옛 이름이다**(11-20 C11) — 화면 · 툴팁 · 검증 표시 이름만 빌야르 · 명성으로 바꿨다. 클래스 · 파일 · 폴더 · skillId(`Olaf*` · `Yujin*` · `olaf.*` · `yujin.*`) · 코드 주석은 그대로
- Unity를 플레이하면 TMP 동적 폰트(`Assets/7. Fonts/TheOneLord SDF.asset` · `NotoSansKR-VariableFont_wght SDF.asset`)가 채워진다 — 커밋 전에 되돌린다
- 테스트 씬 캐릭터 선택 = PlayerPrefs `ProjectAbyss.TestEncounter.Player`(0 빌야르 · 1 명성 · 2 히후미)
- `Battle Test Scene` 단독 실행은 RunProgression이 없다(런 단위 기능은 `Run Flow Test`에서)
