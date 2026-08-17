public struct DamageRequest
{
    public DamageType Type;

    public Character SourceCharacter;
    public BattleAction SourceAction;

    public Character TargetCharacter;
    public BodyPart TargetPart;

    // Action 기반 피해에서는 합 결과로 확정된 피해 기준 위력이다.
    // 외부 효과 기반 피해에서는 계산 시작값이다.
    public int Damage;
    public int RawPower;

    public PhysicalDamageType PhysicalType;
    public bool ApplyPhysicalResistance;

    // ActionType 보정이 아니다.
    // 표준 행동은 항상 1이며, Attack Weight 보조 대상이나 명시적 Custom 요청만
    // 필요한 경우 별도 계수를 전달한다.
    public float DamageCoefficient;

    public bool CanBreakPart;
    public bool WasCritical;

    public bool IsClashDamage;
    public bool TargetLostClash;
    public bool IsPrestigeClash;

    public bool ApplyMomentum;
    public bool ApplyAttackerModifiers;
    public bool ApplyGuard;
    public bool ApplyTargetModifiers;

    public StatusEffect SourceEffect;

    public static DamageRequest FromAction(
        BattleAction action,
        DamageType damageType,
        bool canBreakPart,
        bool isClashDamage,
        bool targetLostClash)
    {
        return new DamageRequest
        {
            Type = damageType,

            SourceCharacter = action?.Owner,
            SourceAction = action,

            TargetCharacter = action?.Target,
            TargetPart = action?.TargetPart,

            Damage = action?.RolledPower ?? 0,
            RawPower = action?.RolledPower ?? 0,

            PhysicalType = PhysicalDamageResolver.Resolve(action),
            ApplyPhysicalResistance = action != null,

            // 일반공격/결투/도사림/위세 같은 ActionType으로 피해를 보정하지 않는다.
            DamageCoefficient = 1f,

            CanBreakPart = canBreakPart,
            WasCritical = action?.Critical == true,

            IsClashDamage = isClashDamage,
            TargetLostClash = targetLostClash,
            IsPrestigeClash =
                isClashDamage &&
                action?.ActionType == ActionType.Prestige,

            ApplyMomentum = true,
            ApplyAttackerModifiers = true,
            ApplyGuard = true,
            ApplyTargetModifiers = true,

            SourceEffect = null
        };
    }

    public static DamageRequest SkillPart(
        BodyPart targetPart,
        int damage,
        bool canBreakPart)
    {
        return CreateExternal(
            DamageType.SkillPart,
            null,
            targetPart?.Owner,
            targetPart,
            damage,
            canBreakPart,
            null,
            applyGuard: true);
    }

    public static DamageRequest SkillPart(
        BattleAction action,
        int damage,
        bool canBreakPart)
    {
        DamageRequest request =
            FromAction(
                action,
                action?.TargetPart == null
                    ? DamageType.Direct
                    : DamageType.SkillPart,
                canBreakPart,
                false,
                false);

        request.Damage = damage;
        request.RawPower = damage;
        return request;
    }

    public static DamageRequest StatusPart(
        BodyPart targetPart,
        int damage,
        StatusEffect sourceEffect)
    {
        return CreateExternal(
            DamageType.StatusPart,
            null,
            targetPart?.Owner,
            targetPart,
            damage,
            false,
            sourceEffect,
            applyGuard: false);
    }

    public static DamageRequest Direct(
        int damage)
    {
        return CreateExternal(
            DamageType.Direct,
            null,
            null,
            null,
            damage,
            false,
            null,
            applyGuard: false);
    }

    public static DamageRequest Direct(
        Character source,
        Character target,
        int damage,
        BattleAction sourceAction = null)
    {
        DamageRequest request =
            CreateExternal(
                DamageType.Direct,
                source,
                target,
                null,
                damage,
                false,
                null,
                applyGuard: true);

        request.SourceAction = sourceAction;
        request.PhysicalType = PhysicalDamageResolver.Resolve(sourceAction);
        request.ApplyPhysicalResistance = sourceAction != null;
        request.WasCritical =
            sourceAction?.Critical == true;
        request.ApplyMomentum =
            sourceAction != null;
        request.ApplyAttackerModifiers =
            sourceAction != null;
        request.ApplyTargetModifiers = true;

        return request;
    }

    public static DamageRequest True(
        int damage,
        StatusEffect sourceEffect)
    {
        return CreateExternal(
            DamageType.True,
            null,
            null,
            null,
            damage,
            false,
            sourceEffect,
            applyGuard: false);
    }

    public static DamageRequest True(
        Character source,
        Character target,
        int damage,
        StatusEffect sourceEffect)
    {
        return CreateExternal(
            DamageType.True,
            source,
            target,
            null,
            damage,
            false,
            sourceEffect,
            applyGuard: false);
    }

    public static DamageRequest SelfCost(
        Character owner,
        int damage)
    {
        DamageRequest request =
            CreateExternal(
                DamageType.SelfCost,
                owner,
                owner,
                null,
                damage,
                false,
                null,
                applyGuard: false);

        request.ApplyTargetModifiers = false;
        return request;
    }

    public static DamageRequest Counter(
        BattleAction action,
        int rawPower,
        bool canBreakPart)
    {
        DamageRequest request =
            FromAction(
                action,
                DamageType.Counter,
                canBreakPart,
                false,
                false);

        request.Damage = rawPower;
        request.RawPower = rawPower;
        return request;
    }

    public static DamageRequest Custom(
        DamageType type,
        Character source,
        Character target,
        BodyPart targetPart,
        int rawPower,
        float damageCoefficient,
        bool canBreakPart,
        bool applyMomentum,
        bool applyGuard,
        BattleAction sourceAction = null,
        StatusEffect sourceEffect = null)
    {
        return new DamageRequest
        {
            Type = type,

            SourceCharacter = source,
            SourceAction = sourceAction,

            TargetCharacter = target,
            TargetPart = targetPart,

            Damage = rawPower,
            RawPower = rawPower,

            PhysicalType = PhysicalDamageResolver.Resolve(sourceAction),
            ApplyPhysicalResistance = sourceAction != null,

            DamageCoefficient =
                UnityEngine.Mathf.Max(
                    0f,
                    damageCoefficient),

            CanBreakPart = canBreakPart,
            WasCritical =
                sourceAction?.Critical == true,

            ApplyMomentum = applyMomentum,
            ApplyAttackerModifiers =
                sourceAction != null,
            ApplyGuard = applyGuard,
            ApplyTargetModifiers = true,

            SourceEffect = sourceEffect
        };
    }

    private static DamageRequest CreateExternal(
        DamageType type,
        Character source,
        Character target,
        BodyPart targetPart,
        int damage,
        bool canBreakPart,
        StatusEffect sourceEffect,
        bool applyGuard)
    {
        return new DamageRequest
        {
            Type = type,

            SourceCharacter = source,
            SourceAction = null,

            TargetCharacter = target,
            TargetPart = targetPart,

            Damage = damage,
            RawPower = damage,

            PhysicalType = PhysicalDamageType.Cut,
            ApplyPhysicalResistance = false,

            DamageCoefficient = 1f,

            CanBreakPart = canBreakPart,
            WasCritical = false,

            IsClashDamage = false,
            TargetLostClash = false,
            IsPrestigeClash = false,

            ApplyMomentum = false,
            ApplyAttackerModifiers = false,
            ApplyGuard = applyGuard,
            ApplyTargetModifiers = false,

            SourceEffect = sourceEffect
        };
    }
}
