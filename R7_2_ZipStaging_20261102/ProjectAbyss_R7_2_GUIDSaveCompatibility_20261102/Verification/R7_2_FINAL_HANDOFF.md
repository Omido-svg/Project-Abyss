# Project Abyss R7-2 — SkillId/GUID 및 세이브 호환성 계약

기준일: 2026-11-02. Unity Editor 실제 프로젝트 F:\GitCorp\Project-Abyss에서 수행.
전투 규칙 정본: 전투 시스템 규칙(1029)(3).md.
**R7-2의 목적은 SkillId를 무작정 변경하지 않으면서 중복 에셋의 참조·세이브 복원을 확정하는 것.**

## 데이터 계약
- 물리적으로 기존 SkillDefinition 145개와 중복 SkillId 4종/8개 에셋은 그대로 남아 있다.
- 기존 SourceAsset GUID, .meta, SkillId, UpgradeProfile, Prefab, Loadout 에셋을 변경하지 않았다.
- 새 R7SkillIdentityRegistry20261102 ScriptableObject가 8개 Unity GUID와 원본 에셋의 직접 참조를 등록한다. 런타임 빌드에서 AssetDatabase를 쓰지 않는다.
- 원본 세이브의 SkillId 문자열 목록은 유지한다. GUID 배열을 병렬 추가한다 (Normal/Duel/Preparation/PrestigeSkillGuids 및 OwnedSkillGuids).
- 새 세이브는 충돌 SkillId에 해당하는 GUID를 기록한다. 다른 스킬의 GUID 슬롯은 빈 문자열이다.
- GUID가 저장된 경우 정확한 GUID+SkillId+후보 에셋을 조회하며, 잘못된 GUID는 복원을 실패시키고 기존 장착 상태는 유지한다. 다른 에셋으로 임의 대체하지 않는다.
- 이전 구형 ID-only 세이브는 해당 현재 로드아웃의 후보가 하나면 그 후보를 사용하고, 둘 이상일 때는 레지스트리에서 선언된 LegacyIdDefault를 사용한다. 이 기본값은 현재 Olaf_TODO_Loadout / Yujin_TODO_Loadout의 PhaseE 사용 에셋에 부합한다.
- IMPORTANT: 구형 ID-only 데이터만으로 과거에 Design2026 원본이 쓰였는지 PhaseE 에셋이 쓰였는지를 소급 판별할 수 없다. 따라서 원래 사용한 변형 에셋을 완벽히 복원하는 것은 논리적으로 불가능하다. 이번 정책은 결정적/명시적 기본값이지 역사적 완전 동등성 보증이 아니다.
- 중복 ID의 소유 판정 및 SkillUpgradeState 레벨은 GUID별로 분리한다. 네이티브 코드에는 GetLevel(stableId,guid) 오버로드를 제공한다.
- SkillCatalog.FindById 는 같은 ID의 서로 다른 원본이 두 개 이상 있으면 첫 번째를 임의 반환하지 않고 null을 반환한다.
- R8 Validator는 4개 쌍의 정확한 GUID 계약이 검증되면 기존 DUPLICATE_SKILL_ID Blocker 대신 R7_2_GUID_CONTRACT_RESOLVED Info를 보고한다.
- R7 native coverage의 분류는 R7_2_GUID_CONTRACT_NATIVE_PENDING. GUID 충돌은 해결됐지만 해당 효과까지 네이티브 이관 완료됐다는 뜻은 아니다.

## 기존 ID → 8개 에셋 계약

### olaf.duel.standard
- [ID-only 기본] Olaf_D_01 / cd90424506385934784ae6ead386e62b / Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Olaf_D_01.asset
- [명시적 GUID 전용] Olaf_Standard / d685378dcfe71d3418bf9cde7ff0542d / Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_Standard.asset

### olaf.preparation.bloto
- [ID-only 기본] Olaf_P_01 / 3b904fc8be62e1d4e9f69e195c2f1c9f / Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Preparation/Olaf_P_01.asset
- [명시적 GUID 전용] Olaf_Bloto / 0cd53d9b56e64a24b82911598f800af9 / Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_ShowOff.asset

### yujin.preparation.hwanhyeong.attack
- [ID-only 기본] Yujin_P_03 / 271781acc71e5ca46aa05fd44090435c / Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_03.asset
- [명시적 GUID 전용] 환형_공격 / 0a914f0a1b2c3d4e5f60718293a4b5c6 / Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_공격.asset

### yujin.preparation.hwanhyeong.defense
- [ID-only 기본] Yujin_P_04 / e13cd646b4403b545a8e6ca6def71493 / Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_04.asset
- [명시적 GUID 전용] 환형_수비 / 0b914f0b1c2d3e4f5061728394a5b6c7 / Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_수비.asset

## 실제 변경 파일
- Runtime: R7SkillIdentityRegistry20261102.cs (신규), CharacterSkillLoadoutRuntime.cs, CharacterSkillLoadoutState.cs, SkillCatalog.cs, SkillUpgradeSystem.cs.
- Editor: R7Stage2SkillIdContractGate20261102.cs (신규), R8NativeValidation20261102.cs, R7NativeMigrationCoverageGate20261102.cs.
- 신규 SO: Assets/2. Data/Resources/ProjectAbyss/R7/R7SkillIdentityRegistry.asset (+.meta).
- 기존 8개 SkillDefinition은 수정하지 않았다.
- 변경 전 스크립트 백업: R7_2_Backup_20261102.

## 검증
- R7-2 자체 EditMode 대상 검사: 18/18 PASS (에셋, GUID, 현재 로드아웃 기본값, GUID 복원, 구형 복원, 소유권, 업그레이드 격리, 잘못된 GUID 롤백).
- 이전 JSON 구조 그대로 JsonUtility.FromJson으로 Deserialize → ID-only PhaseE 기본 에셋 복원: PASS.
- 스크립트 정적 검사 및 Unity Editor 컴파일: 오류 0.
- R9 전체 회귀: **미실행** (사용자 요청).
- PlayMode 검증: **미실행** (사용자 요청).
- Scene 저장: 없음, Battle Test Scene Dirty 유지. Git commit/push: 없음.

## 제한 및 후속
- R7-3~R7-8에서 복합/캐릭터 고유 효과를 실제 네이티브화할 때, 기존 SkillId만으로 두 변형의 효과를 결정하는 코드를 추가하면 안 된다. 반드시 GUID 키를 사용해야 한다.
- 이전 ID-only 세이브에서 대체 에셋을 썼다면 저장값만으로 판별할 수 없으므로 사용자에게 자동 복원 성공이라고 단정하지 않는다.
- R7-2는 4개 중복 SkillId를 물리적으로 이름 변경하는 단계가 아니라, 기존 데이터와 저장 형식을 보존하는 식별자 계약이다.
- R7-9 패시브/기타 런타임 생명주기 및 전체 전투 동등성은 이 단계의 검증 대상이 아니다.
