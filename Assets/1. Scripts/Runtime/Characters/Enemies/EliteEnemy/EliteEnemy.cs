using System.Collections.Generic;
using UnityEngine;

public class EliteEnemy : Enemy, ICharacterAuthoringTarget
{

    [Header("Posture")]
    [SerializeField]
    private bool usePostureRotation = true;

    [SerializeField]
    private EnemyPostureSettings postureSettings = new();

    [Header("P0 D-06 — data-defined five body parts")]
    [SerializeField]
    private List<EnemyBodyPartDefinition> bodyPartDefinitions = new();

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts => bodyParts;
    public IReadOnlyList<EnemyBodyPartDefinition> BodyPartDefinitions => bodyPartDefinitions;

    public override bool SupportsLastStand => true;

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        if (bundle == null ||
            bundle.Kind != CharacterAuthoringKind.EliteEnemy ||
            bundle.CombatLoadout == null)
        {
            return false;
        }

        usePostureRotation = bundle.UseElitePostureRotation;
        postureSettings =
            bundle.ElitePostureSettings ??
            new EnemyPostureSettings();

        if (bundle.EliteBodyPartDefinitions != null && bundle.EliteBodyPartDefinitions.Count > 0)
        {
            bodyPartDefinitions = new List<EnemyBodyPartDefinition>(bundle.EliteBodyPartDefinitions);
        }

        return true;
    }

    //--------------------------------
    // EliteEnemy는 도사림 사용 허용
    //--------------------------------

    protected override bool AllowPreparationSkillAI => true;

    //--------------------------------
    // 부위 구성
    //--------------------------------

    protected override void BuildBodyParts()
    {
        bodyParts.Clear();

        IReadOnlyList<EnemyBodyPartDefinition> definitions = GetEffectiveBodyPartDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyBodyPartDefinition definition = definitions[i];
            if (definition != null)
                bodyParts.Add(definition.CreateRuntimePart(i));
        }
    }

    private IReadOnlyList<EnemyBodyPartDefinition> GetEffectiveBodyPartDefinitions()
    {
        if (bodyPartDefinitions != null && bodyPartDefinitions.Count == 5)
            return bodyPartDefinitions;

        // 기존 4부위 Prefab의 직렬화 호환만 위한 fallback.
        // 실제 적별 값은 Inspector/Authoring 데이터 5개로 저장해야 한다.
        Debug.LogWarning(
            $"[P0 D-06] {name}: Elite/Boss bodyPartDefinitions가 5개가 아닙니다. " +
            "호환용 5부위 템플릿을 사용합니다. 적별 부위 이름/역할/디버프를 데이터로 저장하세요.");

        return CreateCompatibilityFivePartTemplate();
    }

    private static List<EnemyBodyPartDefinition> CreateCompatibilityFivePartTemplate()
    {
        return new List<EnemyBodyPartDefinition>
        {
            new()
            {
                PartId = "head",
                DisplayName = "Head",
                LegacyType = PartType.HEAD,
                SlotRole = BodyPartSlotRole.Hybrid,
                MaxPartHP = 50f,
                BrokenEnergyMaxPenalty = 1
            },
            new() { PartId = "left_arm", DisplayName = "Left Arm", LegacyType = PartType.LEFT_HAND, SlotRole = BodyPartSlotRole.Attack, MaxPartHP = 50f, RollCountPenalty = 1 },
            new() { PartId = "right_arm", DisplayName = "Right Arm", LegacyType = PartType.RIGHT_HAND, SlotRole = BodyPartSlotRole.Attack, MaxPartHP = 50f, RollCountPenalty = 1 },
            new() { PartId = "legs", DisplayName = "Legs", LegacyType = PartType.LEGS, SlotRole = BodyPartSlotRole.Preparation, MaxPartHP = 50f, SpeedMaxPenalty = 1 },
            new() { PartId = "extra", DisplayName = "Extra", LegacyType = PartType.CUSTOM, SlotRole = BodyPartSlotRole.Hybrid, MaxPartHP = 50f }
        };
    }

    //--------------------------------
    // 메커닉 구성
    //--------------------------------

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new EliteEnemyMechanic());

        if (usePostureRotation)
        {
            AddMechanic(
                new EnemyPostureMechanic(postureSettings));
        }
    }

    public override int GetMaxCombatActionSlots()
    {
        EnemyPostureMechanic posture =
            GetMechanic<EnemyPostureMechanic>();

        return posture?.CurrentAttackSlotLimit ?? 3;
    }

    //--------------------------------
    // 약화 디버프
    //--------------------------------

    protected override StatusEffect CreateDisabledDebuff(
        BodyPart part)
    {
        if (part == null)
            return null;

        if (part.UsesDataDefinedRules)
            return new DataDrivenPartDisabled(part);

        return part.Type switch
        {
            PartType.HEAD => new HeadDisabled(),
            PartType.LEFT_HAND => new ArmDisabled(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new ArmDisabled(PartType.RIGHT_HAND),
            PartType.LEGS => new LegsDisabled(),
            _ => null
        };
    }

    //--------------------------------
    // 파괴 디버프
    //--------------------------------

    protected override StatusEffect CreateBrokenPartStatus(
        BodyPart part)
    {
        if (part == null)
            return null;

        if (part.UsesDataDefinedRules)
            return new DataDrivenBrokenPart(part);

        return part.Type switch
        {
            PartType.HEAD => new BrokenHead(),
            PartType.LEFT_HAND => new BrokenArm(PartType.LEFT_HAND),
            PartType.RIGHT_HAND => new BrokenArm(PartType.RIGHT_HAND),
            PartType.LEGS => new BrokenLegs(),
            _ => null
        };
    }


    public override void OnBodyPartBroken(
        BodyPart part,
        StatusEffect disabledDebuff)
    {
        base.OnBodyPartBroken(part, disabledDebuff);

        if (part == null || !part.UsesDataDefinedRules)
            return;

        StatusEffect broken = CreateBrokenPartStatus(part);
        if (broken != null)
            AddStatus(broken, this, part);

        GetMechanic<EnemyPostureMechanic>()
            ?.RefreshForbiddenPostures();
    }

    public bool IsPostureForbidden(EnemyPosture posture)
    {
        if (BodyParts == null)
            return false;

        EnemyPostureMask mask = posture.ToMask();
        if (mask == EnemyPostureMask.None)
            return false;

        for (int i = 0; i < BodyParts.Count; i++)
        {
            BodyPart part = BodyParts[i];
            if (part == null ||
                !part.IsBroken ||
                !part.UsesDataDefinedRules)
            {
                continue;
            }

            EnemyBodyPartDefinition definition =
                FindDefinitionForPart(part, i);

            if (definition != null &&
                (definition.BrokenForbiddenPostures & mask) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetBodyPartDefinition(
        BodyPart part,
        out EnemyBodyPartDefinition definition)
    {
        definition = null;
        if (part == null || bodyPartDefinitions == null)
            return false;

        for (int i = 0; i < bodyPartDefinitions.Count; i++)
        {
            EnemyBodyPartDefinition candidate = bodyPartDefinitions[i];
            if (candidate == null)
                continue;

            string definitionId = candidate.PartId?.Trim();
            if (!string.IsNullOrWhiteSpace(definitionId) &&
                string.Equals(
                    definitionId,
                    part.PartId,
                    System.StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    private EnemyBodyPartDefinition FindDefinitionForPart(
        BodyPart part,
        int fallbackIndex)
    {
        if (TryGetBodyPartDefinition(part, out EnemyBodyPartDefinition definition))
            return definition;

        return bodyPartDefinitions != null &&
               fallbackIndex >= 0 &&
               fallbackIndex < bodyPartDefinitions.Count
            ? bodyPartDefinitions[fallbackIndex]
            : null;
    }

    //--------------------------------

    public override void Die()
    {
        base.Die();
    }
}
