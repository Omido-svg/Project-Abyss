# Project Abyss Timeline-only Migration Report

- Generated: 2026-07-28 11:17:52
- Unity: 6000.0.56f1
- SkillDefinition: 15
- Passed: 12
- Failed: 3

## Policy

- 모든 전투 스킬은 SkillVisualDefinition과 SkillCutsceneDefinition을 필수로 소유한다.
- Action / ClashAttack / PartBreak / Kill / Return 5개 Timeline을 필수로 소유한다.
- 타격 타이밍은 Timeline Hit Event Clip만 사용한다.
- Animator는 Idle / Hit / Dead / 상태 표현만 담당한다.

## Skills

### PASS — 전투 태세(도사림)

- Skill: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/전투 태세(도사림).asset`
- Visual: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/전투 태세(도사림)/전투 태세(도사림)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/전투 태세(도사림)/전투 태세(도사림)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Enemies/EliteEnemy/Animations/EliteEnemy_Preparation.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 정예병의 압박(결투)

- Skill: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/정예병의 압박(결투).asset`
- Visual: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/정예병의 압박(결투)/정예병의 압박(결투)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/정예병의 압박(결투)/정예병의 압박(결투)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Enemies/EliteEnemy/Animations/EliteEnemy_Duel.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 정예병의 참격(일반공격)

- Skill: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/정예병의 참격(일반공격).asset`
- Visual: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/정예병의 참격(일반공격)/정예병의 참격(일반공격)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/정예병의 참격(일반공격)/정예병의 참격(일반공격)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Enemies/EliteEnemy/Animations/EliteEnemy_NormalAttack.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 처형자의 일격(위세)

- Skill: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/처형자의 일격(위세).asset`
- Visual: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/처형자의 일격(위세)/처형자의 일격(위세)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/EliteEnemy/Skills/Cutscenes/처형자의 일격(위세)/처형자의 일격(위세)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Enemies/EliteEnemy/Animations/EliteEnemy_Prestige.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### FAIL — 난폭한 공격(결투)

- Skill: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/난폭한 공격(결투).asset`
- Visual: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/난폭한 공격(결투)/난폭한 공격(결투)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/난폭한 공격(결투)/난폭한 공격(결투)_Cutscene.asset`
- Animation: ``
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0
- Error: Action: Attacker Animation Clip이 없습니다.
- Error: ClashAttack: Attacker Animation Clip이 없습니다.

### FAIL — 물어뜯기(일반공격)

- Skill: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/물어뜯기(일반공격).asset`
- Visual: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/물어뜯기(일반공격)/물어뜯기(일반공격)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/물어뜯기(일반공격)/물어뜯기(일반공격)_Cutscene.asset`
- Animation: ``
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0
- Error: Action: Attacker Animation Clip이 없습니다.
- Error: ClashAttack: Attacker Animation Clip이 없습니다.

### FAIL — 피의 포식 (위세)

- Skill: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/피의 포식 (위세).asset`
- Visual: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/피의 포식 (위세)/피의 포식 (위세)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Enemies/NormalEnemy/Skills/Cutscenes/피의 포식 (위세)/피의 포식 (위세)_Cutscene.asset`
- Animation: ``
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0
- Error: Action: Attacker Animation Clip이 없습니다.
- Error: ClashAttack: Attacker Animation Clip이 없습니다.

### PASS — 광전사의 결투

- Skill: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_BerserkerDuel_Complete.asset`
- Visual: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_BerserkerDuel_Complete_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Cutscenes/Olaf_BerserkerDuel_Complete/Olaf_BerserkerDuel_Complete_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Duel.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 피의 일격

- Skill: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_BloodStrike_Complete.asset`
- Visual: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_BloodStrike_Complete_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Cutscenes/Olaf_BloodStrike_Complete/Olaf_BloodStrike_Complete_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_NormalAttack.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 불사의 광란

- Skill: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_ImmortalFrenzy_Complete.asset`
- Visual: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_ImmortalFrenzy_Complete_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Cutscenes/Olaf_ImmortalFrenzy_Complete/Olaf_ImmortalFrenzy_Complete_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Prestige.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 광기의 난도질

- Skill: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_MadnessFlurry_Complete.asset`
- Visual: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Olaf_MadnessFlurry_Complete_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Generated Complete/Skills/Cutscenes/Olaf_MadnessFlurry_Complete/Olaf_MadnessFlurry_Complete_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Preparation.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 광기의 난도질(도사림)

- Skill: `Assets/2. Data/Characters/Olaf/Skills/광기의 난도질(도사림).asset`
- Visual: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/광기의 난도질(도사림)/광기의 난도질(도사림)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/광기의 난도질(도사림)/광기의 난도질(도사림)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Preparation.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 광전사의 결투(결투)

- Skill: `Assets/2. Data/Characters/Olaf/Skills/광전사의 결투(결투).asset`
- Visual: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/광전사의 결투(결투)/광전사의 결투(결투)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/광전사의 결투(결투)/광전사의 결투(결투)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Duel.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 불사의 광란(위세)

- Skill: `Assets/2. Data/Characters/Olaf/Skills/불사의 광란(위세).asset`
- Visual: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/불사의 광란(위세)/불사의 광란(위세)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/불사의 광란(위세)/불사의 광란(위세)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_Prestige.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

### PASS — 피의 일격(일반공격)

- Skill: `Assets/2. Data/Characters/Olaf/Skills/피의 일격(일반공격).asset`
- Visual: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/피의 일격(일반공격)/피의 일격(일반공격)_Timeline_Visual.asset`
- Cutscene: `Assets/2. Data/Characters/Olaf/Skills/Cutscenes/피의 일격(일반공격)/피의 일격(일반공격)_Cutscene.asset`
- Animation: `Assets/2. Data/Characters/Olaf/Animations/MixamoAnim/Olaf_NormalAttack.anim`
- Created exclusive visual: False
- Created animation clips: 0
- Created Hit events: 0
- Created VFX events: 0
- Migrated camera shots: 0
- Created camera impact events: 0
- Removed duplicate events: 0
- Removed fixed target animation clips: 0

## Legacy Animation Events

- Scanned .anim clips: 27
- Found legacy events: 0
- Removed legacy events: 0

## AnimationEventRelay

- Found: 0
- Removed: 0
- Modified prefabs: 0
- Modified open scenes: 0
