using UnityEngine;

public enum StatusEffectId
{
    Bleeding = 0, // legacy serialized id: Olaf unique keyword 「혈상」
    Burn = 1,
    Stun = 2,

    Strength = 3,
    Weakness = 4,
    Sturdy = 5,
    Disarm = 6,
    Fracture = 7,
    Protection = 8,
    Rupture = 9,
    Heat = 10,
    Regeneration = 11,
    Pain = 12,
    OlafBloodWound = 13,
    Swift = 14,

    // 0922 append-only ids. 기존 serialized enum 번호는 절대 바꾸지 않는다.
    Stagnation = 15,
    Designation = 16,
    Fear = 17
}

/// <summary>
/// Status effect ScriptableObject authoring schema.
/// Legacy=0을 유지해 기존 asset에 새 필드가 없어도 동작 의미가 바뀌지 않게 한다.
/// Canonical0922는 N/T/∞ 및 Presence T-only 계약을 명시적으로 opt-in한다.
/// </summary>
public enum StatusEffectAuthoringSchema
{
    Legacy = 0,
    Canonical0922 = 1
}

/// <summary>
/// 반대 상태를 저장 단계에서 지우지 않고 계산 단계에서 대수합할 때 사용하는 공용 축.
/// 조건식은 raw presence / numeric total / effective axis를 서로 분리해 조회한다.
/// </summary>
public enum StatusEffectEffectiveAxis
{
    RollShift = 0,
    HpDamageTakenFlat = 1,
    StaggerDamageTakenFlat = 2,
    TurnEndPrestige = 3,
    SpeedMaximumIncrease = 4,
    RollMaximumReduction = 5,
    Regeneration = 6
}

public static class StatusEffectFactory
{
    /// <summary>
    /// Legacy 생성 계약. Phase 5에서 의도적으로 보존한다.
    /// 기존 asset의 stale Duration=3이 갑자기 실전 지속시간으로 활성화되지 않게
    /// 이 overload들의 의미는 0922 이전과 동일하게 유지한다.
    /// </summary>
    public static StatusEffect Create(
        StatusEffectId id,
        int stack)
    {
        return Create(
            id,
            stack,
            3,
            0,
            RegenerationRecoveryChannel.HitPoints);
    }

    /// <summary>
    /// Legacy 생성 계약. Canonical authoring은 CreateCanonical0922/CreateForAuthoring을 사용한다.
    /// </summary>
    public static StatusEffect Create(
        StatusEffectId id,
        int stack,
        int duration)
    {
        return Create(
            id,
            stack,
            duration,
            0,
            RegenerationRecoveryChannel.HitPoints);
    }

    /// <summary>
    /// Legacy 생성 계약. 기존 serialized data와 debug/verification 호출을 보호한다.
    /// </summary>
    public static StatusEffect Create(
        StatusEffectId id,
        int stack,
        int duration,
        int regenerationHealAmount,
        RegenerationRecoveryChannel regenerationChannel)
    {
        int safeStack =
            Mathf.Max(1, stack);
        int safeDuration =
            Mathf.Max(1, duration);

        return id switch
        {
            StatusEffectId.Bleeding =>
                new Bleeding(
                    safeStack,
                    safeDuration),
            StatusEffectId.Burn =>
                new Burn(
                    safeStack,
                    safeDuration),
            StatusEffectId.Stun =>
                new Stun(),
            StatusEffectId.Strength =>
                new StrengthStatus(safeStack),
            StatusEffectId.Weakness =>
                new WeaknessStatus(safeStack),
            StatusEffectId.Sturdy =>
                new SturdyStatus(safeStack),
            StatusEffectId.Disarm =>
                new DisarmStatus(safeStack),
            StatusEffectId.Fracture =>
                new FractureStatus(safeStack),
            StatusEffectId.Protection =>
                new ProtectionStatus(safeStack),
            StatusEffectId.Rupture =>
                new RuptureStatus(safeStack),
            StatusEffectId.Heat =>
                new HeatStatus(safeStack),
            StatusEffectId.Swift =>
                new SwiftStatus(safeStack),
            StatusEffectId.Regeneration =>
                new RegenerationStatus(
                    safeDuration,
                    regenerationHealAmount > 0
                        ? regenerationHealAmount
                        : safeStack,
                    regenerationChannel),
            StatusEffectId.Pain =>
                // Legacy에서는 Stack 필드가 Pain의 남은 턴으로 쓰였다.
                new PainStatus(safeStack),
            StatusEffectId.OlafBloodWound =>
                new Bleeding(safeStack, -1),

            // 0922 append-only ids는 구 asset에 존재하지 않으므로 안전한 호환값만 제공한다.
            StatusEffectId.Stagnation =>
                new StagnationStatus(safeStack, safeDuration),
            StatusEffectId.Designation =>
                new YujinDesignationStatus(safeStack, safeDuration),
            StatusEffectId.Fear =>
                new OlafFearStatus(safeDuration),
            _ => null
        };
    }

    /// <summary>
    /// 0922 canonical generic authoring 생성 경로.
    /// Numeric = N/T independent, Presence = T-only, Bespoke = generic factory 사용 금지.
    /// duration &lt; 0 은 ∞다.
    /// </summary>
    public static StatusEffect CreateCanonical0922(
        StatusEffectId id,
        int value,
        int duration)
    {
        if (!IsCanonicalGenericAuthorable(id))
            return null;

        int safeValue =
            Mathf.Max(1, value);
        int safeDuration =
            NormalizeCanonicalDuration(duration);

        return id switch
        {
            StatusEffectId.Strength =>
                new StrengthStatus(safeValue, safeDuration),
            StatusEffectId.Weakness =>
                new WeaknessStatus(safeValue, safeDuration),
            StatusEffectId.Sturdy =>
                new SturdyStatus(safeValue, safeDuration),
            StatusEffectId.Disarm =>
                new DisarmStatus(safeValue, safeDuration),
            StatusEffectId.Fracture =>
                new FractureStatus(safeValue, safeDuration),
            StatusEffectId.Protection =>
                new ProtectionStatus(safeValue, safeDuration),
            StatusEffectId.Rupture =>
                new RuptureStatus(safeValue, safeDuration),
            StatusEffectId.Heat =>
                new HeatStatus(safeValue, safeDuration),
            StatusEffectId.Stagnation =>
                new StagnationStatus(safeValue, safeDuration),
            StatusEffectId.Swift =>
                new SwiftStatus(safeValue, safeDuration),
            StatusEffectId.Regeneration =>
                new RegenerationStatus(
                    safeDuration,
                    safeValue,
                    RegenerationRecoveryChannel.HitPoints),
            StatusEffectId.Designation =>
                new YujinDesignationStatus(
                    safeValue,
                    safeDuration),

            // Presence는 value를 gameplay에 사용하지 않는다.
            StatusEffectId.Pain =>
                new PainStatus(safeDuration),
            StatusEffectId.Fear =>
                new OlafFearStatus(safeDuration),
            _ => null
        };
    }

    /// <summary>
    /// ScriptableObject authoring용 안전한 단일 진입점.
    /// Legacy asset은 기존 의미를 그대로 사용하고, Canonical0922 opt-in asset만 새 N/T/∞ 문법을 사용한다.
    /// </summary>
    public static StatusEffect CreateForAuthoring(
        StatusEffectAuthoringSchema schema,
        StatusEffectId id,
        int value,
        int duration,
        int legacyRegenerationHealAmount = 0,
        RegenerationRecoveryChannel legacyRegenerationChannel =
            RegenerationRecoveryChannel.HitPoints)
    {
        return schema == StatusEffectAuthoringSchema.Canonical0922
            ? CreateCanonical0922(
                id,
                value,
                duration)
            : Create(
                id,
                value,
                duration,
                legacyRegenerationHealAmount,
                legacyRegenerationChannel);
    }

    public static StatusEffectStorageKind GetStorageKind(
        StatusEffectId id)
    {
        return id switch
        {
            StatusEffectId.Strength or
            StatusEffectId.Weakness or
            StatusEffectId.Sturdy or
            StatusEffectId.Disarm or
            StatusEffectId.Fracture or
            StatusEffectId.Protection or
            StatusEffectId.Rupture or
            StatusEffectId.Heat or
            StatusEffectId.Stagnation or
            StatusEffectId.Swift or
            StatusEffectId.Regeneration or
            StatusEffectId.Designation =>
                StatusEffectStorageKind.NumericTimed,

            StatusEffectId.Pain or
            StatusEffectId.Fear =>
                StatusEffectStorageKind.PresenceTimed,

            _ =>
                StatusEffectStorageKind.Bespoke
        };
    }

    /// <summary>
    /// Canonical generic Add Status authoring으로 생성해도 되는 상태인지 판정한다.
    /// Bleeding/Mark/Seal 같은 bespoke는 전용 effect/mechanic을 사용해야 한다.
    /// </summary>
    public static bool IsCanonicalGenericAuthorable(
        StatusEffectId id)
    {
        StatusEffectStorageKind kind =
            GetStorageKind(id);

        return kind == StatusEffectStorageKind.NumericTimed ||
               kind == StatusEffectStorageKind.PresenceTimed;
    }

    public static bool MatchesStatusId(
        StatusEffectId id,
        StatusEffect effect)
    {
        if (effect == null)
            return false;

        return id switch
        {
            StatusEffectId.Bleeding or
            StatusEffectId.OlafBloodWound =>
                effect is Bleeding,
            StatusEffectId.Burn =>
                effect is Burn,
            StatusEffectId.Stun =>
                effect is Stun,
            StatusEffectId.Strength =>
                effect is StrengthStatus,
            StatusEffectId.Weakness =>
                effect is WeaknessStatus,
            StatusEffectId.Sturdy =>
                effect is SturdyStatus,
            StatusEffectId.Disarm =>
                effect is DisarmStatus,
            StatusEffectId.Fracture =>
                effect is FractureStatus,
            StatusEffectId.Protection =>
                effect is ProtectionStatus,
            StatusEffectId.Rupture =>
                effect is RuptureStatus,
            StatusEffectId.Heat =>
                effect is HeatStatus,
            StatusEffectId.Stagnation =>
                effect is StagnationStatus,
            StatusEffectId.Regeneration =>
                effect is RegenerationStatus,
            StatusEffectId.Pain =>
                effect is PainStatus,
            StatusEffectId.Swift =>
                effect is SwiftStatus,
            StatusEffectId.Designation =>
                effect is YujinDesignationStatus,
            StatusEffectId.Fear =>
                effect is OlafFearStatus,
            _ => false
        };
    }

    /// <summary>
    /// 조건 API 1/3: raw presence.
    /// 상태 종류와 무관하게 해당 상태가 하나라도 살아 있는지만 본다.
    /// Presence의 legacy Stack=1을 gameplay 수치로 읽지 않는다.
    /// </summary>
    public static bool HasStatus(
        Character target,
        BodyPart part,
        StatusEffectId id,
        bool checkCharacterStatus = true,
        bool checkPartStatus = true)
    {
        if (target == null)
            return false;

        if (checkCharacterStatus &&
            target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (MatchesStatusId(id, effect))
                    return true;
            }
        }

        if (checkPartStatus &&
            part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (MatchesStatusId(id, effect))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 구 StatusStackRange asset 호환용. StorageKind와 무관하게 Stack을 더한다.
    /// 신규 canonical 조건은 GetNumericTotal을 사용해야 한다.
    /// </summary>
    public static int GetLegacyStackTotal(
        Character target,
        BodyPart part,
        StatusEffectId id,
        bool checkCharacterStatus = true,
        bool checkPartStatus = true)
    {
        if (target == null)
            return 0;

        int total = 0;

        if (checkCharacterStatus &&
            target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (MatchesStatusId(id, effect))
                    total += Mathf.Max(0, effect.Stack);
            }
        }

        if (checkPartStatus &&
            part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (MatchesStatusId(id, effect))
                    total += Mathf.Max(0, effect.Stack);
            }
        }

        return total;
    }

    /// <summary>
    /// 조건 API 2/3: numeric total.
    /// NumericTimed Entry의 N만 합산하며 Presence/Bespoke의 Stack marker는 절대 수치로 취급하지 않는다.
    /// </summary>
    public static int GetNumericTotal(
        Character target,
        BodyPart part,
        StatusEffectId id,
        bool checkCharacterStatus = true,
        bool checkPartStatus = true)
    {
        if (target == null ||
            GetStorageKind(id) != StatusEffectStorageKind.NumericTimed)
        {
            return 0;
        }

        int total = 0;

        if (checkCharacterStatus &&
            target.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
            {
                if (MatchesStatusId(id, effect) &&
                    effect.StorageKind == StatusEffectStorageKind.NumericTimed)
                {
                    total += Mathf.Max(0, effect.NumericValue);
                }
            }
        }

        if (checkPartStatus &&
            part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
            {
                if (MatchesStatusId(id, effect) &&
                    effect.StorageKind == StatusEffectStorageKind.NumericTimed)
                {
                    total += Mathf.Max(0, effect.NumericValue);
                }
            }
        }

        return total;
    }

    /// <summary>
    /// 조건 API 3/3: 실제 계산 축의 대수합.
    /// 저장된 반대 상태를 소모하지 않고 현재 살아 있는 Entry를 합산해서 계산한다.
    /// </summary>
    public static int GetEffectiveAxis(
        Character target,
        BodyPart part,
        StatusEffectEffectiveAxis axis,
        bool checkCharacterStatus = true,
        bool checkPartStatus = true)
    {
        if (target == null)
            return 0;

        return axis switch
        {
            StatusEffectEffectiveAxis.RollShift =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Strength,
                    checkCharacterStatus,
                    checkPartStatus) -
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Weakness,
                    checkCharacterStatus,
                    checkPartStatus) -
                (HasStatus(
                    target,
                    part,
                    StatusEffectId.Fear,
                    checkCharacterStatus,
                    checkPartStatus)
                    ? 1
                    : 0),

            StatusEffectEffectiveAxis.HpDamageTakenFlat =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Rupture,
                    checkCharacterStatus,
                    checkPartStatus) -
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Protection,
                    checkCharacterStatus,
                    checkPartStatus),

            StatusEffectEffectiveAxis.StaggerDamageTakenFlat =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Disarm,
                    checkCharacterStatus,
                    checkPartStatus) -
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Sturdy,
                    checkCharacterStatus,
                    checkPartStatus),

            StatusEffectEffectiveAxis.TurnEndPrestige =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Heat,
                    checkCharacterStatus,
                    checkPartStatus) -
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Stagnation,
                    checkCharacterStatus,
                    checkPartStatus),

            StatusEffectEffectiveAxis.SpeedMaximumIncrease =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Swift,
                    checkCharacterStatus,
                    checkPartStatus),

            StatusEffectEffectiveAxis.RollMaximumReduction =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Fracture,
                    checkCharacterStatus,
                    checkPartStatus),

            StatusEffectEffectiveAxis.Regeneration =>
                GetNumericTotal(
                    target,
                    part,
                    StatusEffectId.Regeneration,
                    checkCharacterStatus,
                    checkPartStatus),

            _ => 0
        };
    }

    public static int NormalizeCanonicalDuration(
        int duration)
    {
        return duration < 0
            ? StatusEffect.InfiniteDuration
            : Mathf.Max(1, duration);
    }
}
