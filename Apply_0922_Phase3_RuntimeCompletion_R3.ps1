param(
    [string]$ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Normalize-Newlines([string]$Text)
{
    if ($null -eq $Text)
    {
        return ''
    }

    return $Text.Replace("`r`n", "`n")
}

function Read-Normalized([string]$Path)
{
    if (-not (Test-Path -LiteralPath $Path))
    {
        throw "File not found: $Path"
    }

    return Normalize-Newlines(
        [System.IO.File]::ReadAllText($Path))
}

function Write-Utf8Bom(
    [string]$Path,
    [string]$Content)
{
    $encoding =
        New-Object System.Text.UTF8Encoding($true)

    [System.IO.File]::WriteAllText(
        $Path,
        (Normalize-Newlines $Content),
        $encoding)
}

function Backup-Once([string]$Path)
{
    if (-not (Test-Path -LiteralPath $Path))
    {
        return
    }

    $relative =
        $Path.Substring($ProjectRoot.Length).TrimStart('\', '/')

    $backup =
        Join-Path `
            (Join-Path $ProjectRoot 'PatchBackups\0922_Phase3_RuntimeCompletion_R2') `
            $relative

    $directory =
        Split-Path -Parent $backup

    if (-not (Test-Path -LiteralPath $directory))
    {
        New-Item `
            -ItemType Directory `
            -Path $directory `
            -Force |
            Out-Null
    }

    if (-not (Test-Path -LiteralPath $backup))
    {
        Copy-Item `
            -LiteralPath $Path `
            -Destination $backup

        Write-Host "[BACKUP] $backup"
    }
}

function Replace-Once(
    [string]$Path,
    [string]$Old,
    [string]$New,
    [string]$AlreadyMarker = '')
{
    $content =
        Read-Normalized $Path

    if (-not [string]::IsNullOrWhiteSpace($AlreadyMarker) -and
        $content.Contains($AlreadyMarker))
    {
        Write-Host "[SKIP] already patched: $Path"
        return
    }

    $oldNormalized =
        Normalize-Newlines $Old

    $newNormalized =
        Normalize-Newlines $New

    $index =
        $content.IndexOf(
            $oldNormalized,
            [System.StringComparison]::Ordinal)

    if ($index -lt 0)
    {
        throw (
            "Expected source block not found.`n" +
            "Path=$Path")
    }

    $second =
        $content.IndexOf(
            $oldNormalized,
            $index + $oldNormalized.Length,
            [System.StringComparison]::Ordinal)

    if ($second -ge 0)
    {
        throw (
            "Expected source block is not unique.`n" +
            "Path=$Path")
    }

    Backup-Once $Path

    $patched =
        $content.Substring(0, $index) +
        $newNormalized +
        $content.Substring(
            $index + $oldNormalized.Length)

    Write-Utf8Bom $Path $patched
    Write-Host "[OK] $Path"
}

Write-Host "Project root: $ProjectRoot"
Write-Host ''

# =====================================================================
# Prerequisites
# =====================================================================

$phase2Report =
    Join-Path $ProjectRoot `
        'Logs\GameSystemVerification\0922_Phase2_CoreStatusStorage.md'

if (-not (Test-Path -LiteralPath $phase2Report))
{
    throw 'Phase 2 report missing.'
}

$phase2 =
    Read-Normalized $phase2Report

if (-not (
    $phase2.Contains('RESULT: PASS_CORE_STORAGE') -or
    $phase2.Contains('PHASE2_RESULT=PASS_CORE_STORAGE')))
{
    throw 'Phase 2 is not closed.'
}

# Phase 3 algebra/verifier from the previous package must exist.
$algebraPath =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Status\Common\CommonStatusAlgebra.cs'

$phase3Verifier =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Editor\Verification\Canonical0922Phase3CalculationTurnEndVerification.cs'

if (-not (Test-Path -LiteralPath $algebraPath))
{
    throw 'Phase 3 CommonStatusAlgebra.cs is missing.'
}

if (-not (Test-Path -LiteralPath $phase3Verifier))
{
    throw 'Phase 3 verifier is missing.'
}

# =====================================================================
# Runtime paths
# =====================================================================

$common =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Status\Common\CommonCombatStatuses.cs'

$stagnation =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Status\Common\StagnationStatus.cs'

$controller =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Characters\CharacterBase\CharacterStatusController.cs'

$character =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Characters\CharacterBase\Character.cs'

$resources =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Characters\CharacterBase\CharacterResourceController.cs'

$damage =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Battle\Damage\DamagePipeline.cs'

$stagger =
    Join-Path $ProjectRoot `
        'Assets\1. Scripts\Runtime\Characters\CharacterBase\StaggerGaugeMechanic.cs'

# =====================================================================
# 1) Heat / Regeneration must stop executing per-entry gameplay.
# =====================================================================

$oldHeat = @'
/// <summary>
/// 0922 저장 모델에서는 Heat도 일반 NumericTimed N/T Entry다.
/// 위세 적용 시점(OnApply -> TurnEnd) 변경은 Phase 3에서 처리한다.
/// </summary>
public sealed class HeatStatus : NumericTimedStatus
{
    public HeatStatus(int stack = 1, int duration = 1)
        : base("열기", stack, duration) { }

    public override void OnApply()
    {
        // Phase 2는 저장 모델만 변경한다. 기존 gameplay timing은 Phase 3까지 보존한다.
        if (Owner != null && Stack > 0)
            Owner.AddPrestige(Stack);
    }
}
'@

$newHeat = @'
/// <summary>
/// 0922 열기 N·T.
/// 개별 Entry는 gameplay 효과를 직접 실행하지 않는다.
/// TurnEnd에서 살아 있는 Heat/Stagnation N을 합산해 한 번 적용한다.
/// </summary>
public sealed class HeatStatus : NumericTimedStatus
{
    public HeatStatus(int stack = 1, int duration = 1)
        : base("열기", stack, duration) { }
}
'@

Replace-Once `
    $common `
    $oldHeat `
    $newHeat `
    '개별 Entry는 gameplay 효과를 직접 실행하지 않는다.'

$oldRegen = @'
/// <summary>
/// 0922 저장 모델: 재생의 N은 Stack, T는 Duration으로 독립 보관한다.
/// HP+Stagger 동시 aggregate 효과는 Phase 3에서 정본화한다.
/// </summary>
public sealed class RegenerationStatus : NumericTimedStatus
{
    public int HealAmount => Stack;
    public RegenerationRecoveryChannel Channel { get; private set; }

    public RegenerationStatus(
        int turns = 1,
        int healAmount = 1,
        RegenerationRecoveryChannel channel = RegenerationRecoveryChannel.HitPoints)
        : base("재생", healAmount, turns)
    {
        Channel = channel;
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (Owner == null || HealAmount <= 0)
            return;

        // Phase 2는 N/T 저장만 정본화한다.
        // 두 회복 채널을 하나의 aggregate N으로 처리하는 것은 Phase 3 소유다.
        if (Channel == RegenerationRecoveryChannel.Stagger)
            Owner.GetMechanic<StaggerGaugeMechanic>()?.Recover(HealAmount);
        else
            Owner.RestoreCurrentHP(HealAmount);
    }
}
'@

$newRegen = @'
/// <summary>
/// 0922 재생 N·T.
/// Channel은 구 asset/생성자 호환용 metadata로만 보존한다.
/// 실제 gameplay는 TurnEnd에서 모든 살아 있는 Regeneration N을 합산한 뒤
/// HP와 Stagger를 각각 한 번 회복한다.
/// </summary>
public sealed class RegenerationStatus : NumericTimedStatus
{
    public int HealAmount => Stack;
    public RegenerationRecoveryChannel Channel { get; private set; }

    public RegenerationStatus(
        int turns = 1,
        int healAmount = 1,
        RegenerationRecoveryChannel channel = RegenerationRecoveryChannel.HitPoints)
        : base("재생", healAmount, turns)
    {
        Channel = channel;
    }
}
'@

Replace-Once `
    $common `
    $oldRegen `
    $newRegen `
    'Channel은 구 asset/생성자 호환용 metadata'

# R1 already created this file. Create it only if it is still missing.
if (-not (Test-Path -LiteralPath $stagnation))
{
    $stagnationContent = @'
using UnityEngine;

/// <summary>
/// 0922 침체 N·T.
/// Heat와 저장 단계에서 서로 제거하지 않고 TurnEnd 계산에서만 상쇄한다.
/// </summary>
public sealed class StagnationStatus : NumericTimedStatus
{
    public StagnationStatus(
        int stack = 1,
        int duration = 1)
        : base("침체", stack, duration)
    {
    }
}
'@

    Write-Utf8Bom `
        $stagnation `
        $stagnationContent

    Write-Host "[CREATE] $stagnation"
}

# =====================================================================
# 2) TurnEnd aggregate BEFORE duration tick.
# =====================================================================

$oldTurnEnd = @'
    public void OnTurnEnd()
    {
        TickCharacterStatuses(
            StatusEffectTickTiming.TurnEnd);

        if (owner != null && !owner.IsDead)
        {
            TickPartStatuses(
                StatusEffectTickTiming.TurnEnd);
        }
    }
'@

$newTurnEnd = @'
    public void OnTurnEnd()
    {
        // [0922_PHASE3_TURN_END_AGGREGATE]
        // 살아 있는 Numeric N을 먼저 합산해 효과를 한 번 적용하고,
        // 그 뒤 각 Entry의 Duration을 독립 감소시킨다.
        ApplyCanonicalTurnEndAggregates();

        TickCharacterStatuses(
            StatusEffectTickTiming.TurnEnd);

        if (owner != null && !owner.IsDead)
        {
            TickPartStatuses(
                StatusEffectTickTiming.TurnEnd);
        }
    }

    private void ApplyCanonicalTurnEndAggregates()
    {
        if (owner == null || owner.IsDead)
            return;

        int heat =
            GetActiveNumericTotal<HeatStatus>();

        int stagnation =
            GetActiveNumericTotal<StagnationStatus>();

        int prestigeDelta =
            heat - stagnation;

        if (prestigeDelta != 0)
        {
            owner.AdjustPrestige(
                prestigeDelta);
        }

        int regeneration =
            GetActiveNumericTotal<RegenerationStatus>();

        if (regeneration <= 0)
            return;

        // 0922 Regeneration: aggregate N을 만든 뒤
        // HP +N과 Stagger +N을 각각 한 번만 적용한다.
        owner.RestoreCurrentHP(
            regeneration);

        owner.GetMechanic<StaggerGaugeMechanic>()?
            .Recover(regeneration);
    }

    private int GetActiveNumericTotal<TStatus>()
        where TStatus : NumericTimedStatus
    {
        int total =
            CommonStatusAlgebra.GetNumericTotal<TStatus>(
                characterStatuses);

        if (owner?.BodyParts == null)
            return total;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            total +=
                CommonStatusAlgebra.GetNumericTotal<TStatus>(
                    part.StatusEffects);
        }

        return Mathf.Max(0, total);
    }
'@

Replace-Once `
    $controller `
    $oldTurnEnd `
    $newTurnEnd `
    '[0922_PHASE3_TURN_END_AGGREGATE]'

# =====================================================================
# 3) Pain common healing path for HP + Stagger.
# =====================================================================

$oldRestore = @'
    public void RestoreCurrentHP(int amount)
    {
        if (RuntimeStatus == null || amount <= 0)
            return;

        int modified = amount;
        foreach (StatusEffect effect in StatusEffects)
        {
            if (effect != null)
                modified = effect.ModifyHealing(modified);
        }

        modified = Mathf.Max(0, modified);
        if (modified <= 0)
            return;

        // P0 D-07 확정 규칙:
        // 모든 체력 회복은 값을 나누지 않고 전체 HP +N 과 최저 비파괴 부위 +N에 동시에 들어간다.
        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP + modified,
                MaxCombatHP);

        RestoreLowestBodyPartHpPreserveState(modified);
    }
'@

$newRestore = @'
    public int ModifyHealingAmount(int amount)
    {
        return CommonStatusAlgebra.ApplyHealingModifiers(
            StatusEffects,
            amount);
    }

    public void RestoreCurrentHP(int amount)
    {
        if (RuntimeStatus == null || amount <= 0)
            return;

        int modified =
            ModifyHealingAmount(amount);

        if (modified <= 0)
            return;

        // P0 D-07 확정 규칙:
        // 모든 체력 회복은 값을 나누지 않고 전체 HP +N 과 최저 비파괴 부위 +N에 동시에 들어간다.
        RuntimeStatus.currentHP =
            Mathf.Min(
                RuntimeStatus.currentHP + modified,
                MaxCombatHP);

        RestoreLowestBodyPartHpPreserveState(modified);
    }
'@

Replace-Once `
    $character `
    $oldRestore `
    $newRestore `
    'public int ModifyHealingAmount(int amount)'

$oldRecover = @'
    public void Recover(int amount)
    {
        if (IsSuppressed || amount <= 0 || vulnerabilityWindowOpen)
            return;
        currentGauge = Mathf.Clamp(currentGauge + amount, 0, maxGauge);
    }
'@

$newRecover = @'
    public void Recover(int amount)
    {
        if (IsSuppressed || amount <= 0 || vulnerabilityWindowOpen)
            return;

        int modified =
            owner?.ModifyHealingAmount(amount) ?? amount;

        if (modified <= 0)
            return;

        currentGauge =
            Mathf.Clamp(
                currentGauge + modified,
                0,
                maxGauge);
    }
'@

Replace-Once `
    $stagger `
    $oldRecover `
    $newRecover `
    'owner?.ModifyHealingAmount(amount)'

# =====================================================================
# 4) Signed raw Prestige adjustment for Heat/Stagnation net.
# Existing AddPrestige remains unchanged for ordinary positive rewards.
# =====================================================================

$resourceInsertMarker = @'
    public bool TryConsumePrestige(int amount)
'@

$resourceInsert = @'
    public int AdjustPrestige(int delta)
    {
        if (owner?.RuntimeStatus == null ||
            owner.CurrentStatus == null ||
            delta == 0)
        {
            return 0;
        }

        int before =
            owner.RuntimeStatus.currentPrestige;

        int after =
            Mathf.Clamp(
                before + delta,
                0,
                owner.CurrentStatus.maxPrestige);

        owner.RuntimeStatus.currentPrestige =
            after;

        int actualDelta =
            after - before;

        Debug.Log(
            $"{owner.Data?.CharacterName ?? owner.name} 위세 변화 : " +
            $"{(actualDelta >= 0 ? "+" : string.Empty)}{actualDelta} " +
            $"({after}/{owner.CurrentStatus.maxPrestige})");

        return actualDelta;
    }

    public bool TryConsumePrestige(int amount)
'@

Replace-Once `
    $resources `
    $resourceInsertMarker `
    $resourceInsert `
    'public int AdjustPrestige(int delta)'

$characterPrestigeMarker = @'
    public void AddPrestige(int amount)
'@

$characterPrestigeInsert = @'
    public int AdjustPrestige(int delta)
    {
        if (resourceController == null)
            return 0;

        return resourceController.AdjustPrestige(
            delta);
    }

    public void AddPrestige(int amount)
'@

Replace-Once `
    $character `
    $characterPrestigeMarker `
    $characterPrestigeInsert `
    'public int AdjustPrestige(int delta)'

# =====================================================================
# 5) Fear fixed presence axis, separate from Numeric Strength/Weakness.
# =====================================================================

$rollHeaderOld = @'
        int value = roll + rulebreakerBonus;
        int commonShift = 0;
        int commonMaxReduction = 0;
'@

$rollHeaderNew = @'
        int value = roll + rulebreakerBonus;
        int commonShift = 0;
        int commonMaxReduction = 0;

        int fearPenalty =
            CommonStatusAlgebra.GetFearRollPenalty(
                StatusEffects);

        if (action?.OwnerPart != null)
        {
            fearPenalty =
                Mathf.Max(
                    fearPenalty,
                    CommonStatusAlgebra.GetFearRollPenalty(
                        action.OwnerPart.StatusEffects));
        }
'@

Replace-Once `
    $character `
    $rollHeaderOld `
    $rollHeaderNew `
    'CommonStatusAlgebra.GetFearRollPenalty'

$rollApplyOld = @'
        int nonCommonDelta = value - roll;
        value += commonShift;

        if (commonMaxReduction > 0 && action?.Skill != null)
        {
            int shiftedMaximum = Mathf.Max(
                1,
                action.Skill.MaxPower +
                nonCommonDelta +
                commonShift -
                commonMaxReduction);
'@

$rollApplyNew = @'
        int nonCommonDelta = value - roll;

        // 0922: Strength/Weakness는 Numeric 합산축,
        // Fear는 presence 여부에 따른 fixed -1 추가축이다.
        value += commonShift - fearPenalty;

        if (commonMaxReduction > 0 && action?.Skill != null)
        {
            int shiftedMaximum = Mathf.Max(
                1,
                action.Skill.MaxPower +
                nonCommonDelta +
                commonShift -
                fearPenalty -
                commonMaxReduction);
'@

Replace-Once `
    $character `
    $rollApplyOld `
    $rollApplyNew `
    'value += commonShift - fearPenalty;'

$rollListOld = @'
            if (effect is ICommonRollShiftStatus shiftStatus)
            {
                commonShift += shiftStatus.GetRollShift(action);
                continue;
            }
'@

$rollListNew = @'
            // Fear는 아래 fixed presence axis에서 한 번만 처리한다.
            if (effect is OlafFearStatus)
                continue;

            if (effect is ICommonRollShiftStatus shiftStatus)
            {
                commonShift += shiftStatus.GetRollShift(action);
                continue;
            }
'@

Replace-Once `
    $character `
    $rollListOld `
    $rollListNew `
    'Fear는 아래 fixed presence axis'

# =====================================================================
# 6) HP flat axis: aggregate Rupture - Protection once.
# Preserve unrelated StatusEffect target modifiers.
# =====================================================================

$damageOld = @'
    private static void ApplyFlatTargetModifiers(DamageContext context)
    {
        int damage = context.TargetModifiedDamage;

        if (context.Request.ApplyTargetModifiers && context.Target != null)
        {
            foreach (StatusEffect effect in context.Target.StatusEffects)
            {
                if (effect == null)
                    continue;

                damage = Mathf.Max(
                    0,
                    Mathf.FloorToInt(effect.ModifyDamageTaken(context.Action, damage)));
            }

            if (context.TargetPart != null)
            {
                foreach (StatusEffect effect in context.TargetPart.StatusEffects)
                {
                    if (effect == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        Mathf.FloorToInt(effect.ModifyDamageTaken(context.Action, damage)));
                }
            }
        }

        context.TargetModifiedDamage = damage;
        context.RecordStage(DamageStage.TargetModifiers, damage);
    }
'@

$damageNew = @'
    private static void ApplyFlatTargetModifiers(DamageContext context)
    {
        int damage =
            context.TargetModifiedDamage;

        if (context.Request.ApplyTargetModifiers &&
            context.Target != null)
        {
            int commonFlat =
                CommonStatusAlgebra.GetHpDamageFlatModifier(
                    context.Target,
                    context.TargetPart);

            // 0922: RuptureTotal - ProtectionTotal을 먼저 합산한 뒤
            // 내성 결과에 한 번 적용하고 clamp도 한 번만 수행한다.
            damage =
                Mathf.Max(
                    0,
                    damage + commonFlat);

            // 공용 HP flat 축 이외의 상태 고유 damage modifier는 보존한다.
            foreach (StatusEffect effect in context.Target.StatusEffects)
            {
                if (effect == null ||
                    effect is ProtectionStatus ||
                    effect is RuptureStatus)
                {
                    continue;
                }

                damage =
                    Mathf.Max(
                        0,
                        Mathf.FloorToInt(
                            effect.ModifyDamageTaken(
                                context.Action,
                                damage)));
            }

            if (context.TargetPart != null)
            {
                foreach (StatusEffect effect in context.TargetPart.StatusEffects)
                {
                    if (effect == null ||
                        effect is ProtectionStatus ||
                        effect is RuptureStatus)
                    {
                        continue;
                    }

                    damage =
                        Mathf.Max(
                            0,
                            Mathf.FloorToInt(
                                effect.ModifyDamageTaken(
                                    context.Action,
                                    damage)));
                }
            }
        }

        context.TargetModifiedDamage =
            damage;

        context.RecordStage(
            DamageStage.TargetModifiers,
            damage);
    }
'@

Replace-Once `
    $damage `
    $damageOld `
    $damageNew `
    'CommonStatusAlgebra.GetHpDamageFlatModifier'

# =====================================================================
# 7) Stagger flat axis: aggregate Disarm - Sturdy once.
# Preserve unrelated flat status modifiers.
# =====================================================================

$staggerFlatOld = @'
    // [0917_CONFIRMED_GAP:STAGGER_FLAT_HELPER]
    private static int ApplyTargetStaggerDamageFlatModifiers(
        BattleAction action,
        Character target,
        BodyPart targetPart,
        int damage)
    {
        if (target == null || damage <= 0)
            return Mathf.Max(0, damage);

        int flatModifier = 0;

        if (target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (effect != null)
                {
                    flatModifier +=
                        effect.GetStaggerDamageTakenFlatModifier(action);
                }
            }
        }

        if (targetPart != null &&
            targetPart.Owner == target &&
            targetPart.StatusEffects != null)
        {
            foreach (StatusEffect effect in targetPart.StatusEffects)
            {
                if (effect != null)
                {
                    flatModifier +=
                        effect.GetStaggerDamageTakenFlatModifier(action);
                }
            }
        }

        return Mathf.Max(0, damage + flatModifier);
    }
'@

$staggerFlatNew = @'
    // [0922_PHASE3_STAGGER_FLAT_AXIS]
    private static int ApplyTargetStaggerDamageFlatModifiers(
        BattleAction action,
        Character target,
        BodyPart targetPart,
        int damage)
    {
        if (target == null || damage <= 0)
            return Mathf.Max(0, damage);

        int flatModifier =
            CommonStatusAlgebra.GetStaggerDamageFlatModifier(
                target,
                targetPart);

        if (target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (effect == null ||
                    effect is SturdyStatus ||
                    effect is DisarmStatus)
                {
                    continue;
                }

                flatModifier +=
                    effect.GetStaggerDamageTakenFlatModifier(
                        action);
            }
        }

        if (targetPart != null &&
            targetPart.Owner == target &&
            targetPart.StatusEffects != null)
        {
            foreach (StatusEffect effect in targetPart.StatusEffects)
            {
                if (effect == null ||
                    effect is SturdyStatus ||
                    effect is DisarmStatus)
                {
                    continue;
                }

                flatModifier +=
                    effect.GetStaggerDamageTakenFlatModifier(
                        action);
            }
        }

        return Mathf.Max(
            0,
            damage + flatModifier);
    }
'@

Replace-Once `
    $stagger `
    $staggerFlatOld `
    $staggerFlatNew `
    '[0922_PHASE3_STAGGER_FLAT_AXIS]'

# =====================================================================
# Final installation checks matching the Phase 3 verifier.
# =====================================================================

$fail =
    New-Object System.Collections.Generic.List[string]

$commonText =
    Read-Normalized $common

$controllerText =
    Read-Normalized $controller

$characterText =
    Read-Normalized $character

$resourceText =
    Read-Normalized $resources

$damageText =
    Read-Normalized $damage

$staggerText =
    Read-Normalized $stagger

if ($commonText.Contains(
    'public override void OnApply()'))
{
    $heatIndex =
        $commonText.IndexOf(
            'public sealed class HeatStatus',
            [System.StringComparison]::Ordinal)

    $swiftIndex =
        $commonText.IndexOf(
            'public sealed class SwiftStatus',
            [System.StringComparison]::Ordinal)

    if ($heatIndex -ge 0 -and
        $swiftIndex -gt $heatIndex)
    {
        $heatBlock =
            $commonText.Substring(
                $heatIndex,
                $swiftIndex - $heatIndex)

        if ($heatBlock.Contains(
            'public override void OnApply()'))
        {
            $fail.Add('Heat still overrides OnApply')
        }
    }
}

$regenIndex =
    $commonText.IndexOf(
        'public sealed class RegenerationStatus',
        [System.StringComparison]::Ordinal)

$painIndex =
    $commonText.IndexOf(
        'public sealed class PainStatus',
        [System.StringComparison]::Ordinal)

if ($regenIndex -ge 0 -and
    $painIndex -gt $regenIndex)
{
    $regenBlock =
        $commonText.Substring(
            $regenIndex,
            $painIndex - $regenIndex)

    if ($regenBlock.Contains(
        'public override void OnTurnEnd'))
    {
        $fail.Add('Regeneration still overrides OnTurnEnd')
    }
}

if (-not $controllerText.Contains(
    'ApplyCanonicalTurnEndAggregates();'))
{
    $fail.Add('TurnEnd aggregate resolver missing')
}

if (-not $characterText.Contains(
    'value += commonShift - fearPenalty;'))
{
    $fail.Add('Fear fixed roll axis missing')
}

if (-not $characterText.Contains(
    'public int ModifyHealingAmount(int amount)'))
{
    $fail.Add('Common healing modifier missing')
}

if (-not $resourceText.Contains(
    'public int AdjustPrestige(int delta)'))
{
    $fail.Add('Signed prestige adjustment missing')
}

if (-not $damageText.Contains(
    'CommonStatusAlgebra.GetHpDamageFlatModifier'))
{
    $fail.Add('HP aggregate flat axis missing')
}

if (-not $staggerText.Contains(
    'CommonStatusAlgebra.GetStaggerDamageFlatModifier'))
{
    $fail.Add('Stagger aggregate flat axis missing')
}

if (-not $staggerText.Contains(
    'owner?.ModifyHealingAmount(amount)'))
{
    $fail.Add('Stagger Pain healing path missing')
}

if (-not (Test-Path -LiteralPath $stagnation))
{
    $fail.Add('StagnationStatus.cs missing')
}

if ($fail.Count -gt 0)
{
    throw (
        "Phase 3 R2 installation verification failed:`n- " +
        ($fail -join "`n- "))
}

Write-Host ''
Write-Host '=== 0922 PHASE 3 RUNTIME COMPLETION R2 APPLIED ===' `
    -ForegroundColor Green

Write-Host 'All six previously failing runtime wiring domains are now installed.'
Write-Host ''
Write-Host 'Return to Unity and wait for compilation.'
Write-Host 'Then rerun:'
Write-Host 'Game System Verification > 0922 Canonical > Phase 3 - Verify Calculation and TurnEnd'
Write-Host ''
Write-Host 'Expected:'
Write-Host 'PASS=16 FAIL=0'
Write-Host 'PHASE3_RESULT=PASS_CALCULATION_TURNEND'
