public struct DamageRequest
{
    public DamageType Type;

    public Character SourceCharacter;
    public BattleAction SourceAction;

    public Character TargetCharacter;
    public BodyPart TargetPart;

    // Action 기반 피해에서는 순수 위력이다.
    // 외부 효과 기반 피해에서는 계산 시작값이다.
    public int Damage;
    public int RawPower;

    public float SkillMultiplier;
    public float CriticalMultiplier;

    public bool CanBreakPart;
    public bool WasCritical;

    public bool IsClashDamage;
    public bool TargetLostClash;
    public bool IsPrestigeClash;

    public bool ApplyFlatDamageBonus;
    public bool ApplyOwnerMultiplier;
    public bool ApplyMomentum;
    public bool ApplyAttackerModifiers;
    public bool ApplyDefense;
    public bool ApplyGuard;
    public bool ApplyTargetModifiers;
    public bool ApplyProtection;

    public StatusEffect SourceEffect;

    public static DamageRequest FromAction(
        BattleAction action,
        DamageType damageType,
        float skillMultiplier,
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

            SkillMultiplier = skillMultiplier,
            CriticalMultiplier = 1f,

            CanBreakPart = canBreakPart,
            WasCritical = action?.Critical == true,

            IsClashDamage = isClashDamage,
            TargetLostClash = targetLostClash,
            IsPrestigeClash =
                isClashDamage &&
                action?.ActionType == ActionType.Prestige,

            ApplyFlatDamageBonus = true,
            ApplyOwnerMultiplier = true,
            ApplyMomentum = true,
            ApplyAttackerModifiers = true,
            ApplyDefense = true,
            ApplyGuard = true,
            ApplyTargetModifiers = true,
            ApplyProtection = true,

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
            applyDefense: true,
            applyGuard: true,
            applyProtection: true);
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
                1f,
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
            applyDefense: false,
            applyGuard: false,
            applyProtection: false);
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
            applyDefense: false,
            applyGuard: false,
            applyProtection: false);
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
                applyDefense: true,
                applyGuard: true,
                applyProtection: true);

        request.SourceAction = sourceAction;
        request.WasCritical =
            sourceAction?.Critical == true;
        request.ApplyFlatDamageBonus =
            sourceAction != null;
        request.ApplyOwnerMultiplier =
            sourceAction != null;
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
            applyDefense: false,
            applyGuard: false,
            applyProtection: false);
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
            applyDefense: false,
            applyGuard: false,
            applyProtection: false);
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
                applyDefense: false,
                applyGuard: false,
                applyProtection: false);

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
                1f,
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
        float skillMultiplier,
        bool canBreakPart,
        bool applyMomentum,
        bool applyDefense,
        bool applyGuard,
        bool applyProtection,
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

            SkillMultiplier = skillMultiplier,
            CriticalMultiplier = 1f,

            CanBreakPart = canBreakPart,
            WasCritical =
                sourceAction?.Critical == true,

            ApplyFlatDamageBonus =
                sourceAction != null,
            ApplyOwnerMultiplier =
                sourceAction != null,
            ApplyMomentum = applyMomentum,
            ApplyAttackerModifiers =
                sourceAction != null,
            ApplyDefense = applyDefense,
            ApplyGuard = applyGuard,
            ApplyTargetModifiers = true,
            ApplyProtection = applyProtection,

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
        bool applyDefense,
        bool applyGuard,
        bool applyProtection)
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

            SkillMultiplier = 1f,
            CriticalMultiplier = 1f,

            CanBreakPart = canBreakPart,
            WasCritical = false,

            IsClashDamage = false,
            TargetLostClash = false,
            IsPrestigeClash = false,

            ApplyFlatDamageBonus = false,
            ApplyOwnerMultiplier = false,
            ApplyMomentum = false,
            ApplyAttackerModifiers = false,
            ApplyDefense = applyDefense,
            ApplyGuard = applyGuard,
            ApplyTargetModifiers = false,
            ApplyProtection = applyProtection,

            SourceEffect = sourceEffect
        };
    }
}