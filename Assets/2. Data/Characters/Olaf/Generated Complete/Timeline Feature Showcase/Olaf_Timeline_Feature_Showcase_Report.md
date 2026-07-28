# Olaf Timeline Feature Showcase Build Report

Generated: 2026-07-28 13:50:34

## Feature Matrix

| 기능 | 생성 위치 |
|---|---|
| 공격 Animation | Action / ClashAttack Attacker Track |
| 피격 타이밍 | Battle Events / Hit |
| Camera Shot Blend | Skill Camera Track |
| Camera Local Motion | [AbyssRig] Camera Motion |
| Time Scale | SetTimeScale / RestoreTimeScale |
| 임의 공간 VFX | CombatFrame VFX Clip |
| Anchor Follow VFX | AttackerAnchor / WeaponMain |
| 타깃 부위 VFX | TargetBodyPart + HitIndex |
| Camera Local VFX | Camera Binding |
| VFX 이동·회전·크기 | Start/End Transform + Curve |
| VFX 재생 속도 | PlaybackSpeed |
| Shader FX | Attacker/Target Renderer Property Curve |
| 부위 파괴 분기 | PartBreak Timeline + RequirePartBreak |
| 사망 분기 | Kill Timeline + RequireKill |
| 복귀 | Return Timeline + ReturnOverview |

## Skills

### Olaf_BloodStrike_Complete
- Action: VFX=5, ShaderFX=2
- ClashAttack: VFX=5, ShaderFX=2
- PartBreak: VFX=1, ShaderFX=1
- Kill: VFX=2, ShaderFX=1
- Return: VFX=1, ShaderFX=1

### Olaf_BerserkerDuel_Complete
- Action: VFX=6, ShaderFX=3
- ClashAttack: VFX=6, ShaderFX=3
- PartBreak: VFX=1, ShaderFX=1
- Kill: VFX=2, ShaderFX=1
- Return: VFX=1, ShaderFX=1

### Olaf_MadnessFlurry_Complete
- Action: VFX=5, ShaderFX=2
- ClashAttack: VFX=5, ShaderFX=2
- PartBreak: VFX=1, ShaderFX=1
- Kill: VFX=2, ShaderFX=1
- Return: VFX=1, ShaderFX=1

### Olaf_ImmortalFrenzy_Complete
- Action: VFX=5, ShaderFX=2
- ClashAttack: VFX=5, ShaderFX=2
- PartBreak: VFX=1, ShaderFX=1
- Kill: VFX=2, ShaderFX=1
- Return: VFX=1, ShaderFX=1

## Generated VFX Definitions
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_RageEmbers.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_SlashTrail.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_HitBurst.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_PartBreakBurst.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_KillExplosion.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/VFX Definitions/Olaf_Showcase_ForegroundEmbers.asset

## Generated Shader Definitions
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/Shader FX/Olaf_Showcase_RageGlow.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/Shader FX/Olaf_Showcase_HitFlash.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/Shader FX/Olaf_Showcase_BreakTint.asset
- Assets/2. Data/Characters/Olaf/Generated Complete/Timeline Feature Showcase/Shader FX/Olaf_Showcase_DeathDarken.asset

## Build Log
- [OK] Olaf_BloodStrike_Complete: 5개 Segment Visual FX Showcase 구성
- [OK] Olaf_BerserkerDuel_Complete: 5개 Segment Visual FX Showcase 구성
- [OK] Olaf_MadnessFlurry_Complete: 5개 Segment Visual FX Showcase 구성
- [OK] Olaf_ImmortalFrenzy_Complete: 5개 Segment Visual FX Showcase 구성

## Validation
- PASS
