using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class CharacterVerificationScenarioTools
{
    private const BindingFlags Flags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    public static List<SkillDefinition> GetDefinitions(
        CharacterAuthoringBundle bundle)
    {
        Dictionary<string, SkillDefinition> result =
            new Dictionary<string, SkillDefinition>(
                StringComparer.Ordinal);

        if (bundle == null)
            return result.Values.ToList();

        foreach (SkillDefinition definition
                 in bundle.EnumerateSkillDefinitions())
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.SkillId))
            {
                continue;
            }

            result[definition.SkillId] = definition;
        }

        return result.Values
            .OrderBy(item => item.ActionType)
            .ThenBy(item => item.SkillName)
            .ToList();
    }

    public static SkillDefinition FindDefinition(
        CharacterAuthoringBundle bundle,
        string skillId)
    {
        if (bundle == null ||
            string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        return GetDefinitions(bundle)
            .FirstOrDefault(
                item => string.Equals(
                    item.SkillId,
                    skillId,
                    StringComparison.Ordinal));
    }

    public static Skill FindRuntimeSkill(
        Character owner,
        SkillDefinition definition)
    {
        if (owner == null || definition == null)
            return null;

        Skill existing = owner.RuntimeSkills?
            .FirstOrDefault(
                item =>
                    item?.Definition != null &&
                    string.Equals(
                        item.Definition.SkillId,
                        definition.SkillId,
                        StringComparison.Ordinal));

        if (existing != null)
            return existing;

        Skill created =
            owner.CreateRuntimeSkillForLoadout(definition) ??
            definition.CreateRuntimeSkill();

        created?.Initialize(
            owner,
            owner.BattleContext?._battleEvent);

        return created;
    }

    public static BodyPart GetUsablePart(
        Character character)
    {
        if (character?.BodyParts == null)
            return null;

        BodyPart normal = character.BodyParts
            .FirstOrDefault(
                item => item != null &&
                        !item.IsBroken &&
                        !item.IsWeakened);

        return normal ?? character.BodyParts
            .FirstOrDefault(item => item != null);
    }

    public static BattleAction CreateAction(
        Character owner,
        BodyPart ownerPart,
        Skill skill,
        Character target,
        BodyPart targetPart,
        long actionId = 900001)
    {
        ActionSlot slot = new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = ownerPart,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = $"VERIFY:{actionId}",
            Phase = skill?.DefaultPhase ?? ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = targetPart
        };

        return new BattleAction
        {
            Slot = slot
        };
    }

    public static void FillResources(
        Character owner,
        SkillDefinition definition,
        Skill skill)
    {
        if (owner == null)
            return;

        int missingEnergy =
            Mathf.Max(0, owner.MaxEnergy - owner.CurrentEnergy);

        if (missingEnergy > 0)
            owner.AddEnergy(missingEnergy);

        if (owner.RuntimeStatus != null)
        {
            int maximumPrestige =
                Mathf.Max(
                    100,
                    owner.CurrentStatus?.maxPrestige ?? 0);

            owner.RuntimeStatus.currentPrestige = maximumPrestige;
        }

        if (definition != null &&
            !string.IsNullOrWhiteSpace(
                definition.CustomResourceKey))
        {
            SkillResourceAccess.Set(
                owner,
                definition.CustomResourceKey,
                Mathf.Max(
                    100,
                    definition.CustomResourceCost));
        }
    }

    public static void ResetCombatState(
        Character character)
    {
        if (character == null)
            return;

        StatusEffect[] characterStatuses =
            character.StatusEffects?
                .Where(item => item != null)
                .ToArray() ??
            Array.Empty<StatusEffect>();

        foreach (StatusEffect status in characterStatuses)
        {
            character.RemoveStatus(
                status,
                StatusEffectRemoveReason.Manual);
        }

        if (character.BodyParts != null)
        {
            foreach (BodyPart part in character.BodyParts)
            {
                if (part == null)
                    continue;

                character.RemoveAllPartStatuses(
                    part,
                    StatusEffectRemoveReason.Manual);
                part.Recover();
            }
        }

        if (character.RuntimeStatus != null)
        {
            character.RuntimeStatus.currentHP =
                Mathf.Max(1, character.MaxCombatHP);
            character.RuntimeStatus.currentBlock = 0;
            character.RuntimeStatus.currentPrestige = 0;
        }
    }

    public static string CaptureFingerprint(
        Character owner,
        Character target,
        string customResourceKey = null)
    {
        StringBuilder builder = new StringBuilder();
        AppendCharacter(builder, "OWNER", owner, customResourceKey);
        AppendCharacter(builder, "TARGET", target, customResourceKey);
        return builder.ToString();
    }

    private static void AppendCharacter(
        StringBuilder builder,
        string label,
        Character character,
        string customResourceKey)
    {
        if (character == null)
        {
            builder.Append(label).Append("=NULL;");
            return;
        }

        builder.Append(label)
            .Append(" HP=").Append(character.CurrentHP)
            .Append(" E=").Append(character.CurrentEnergy)
            .Append(" P=")
            .Append(character.RuntimeStatus?.currentPrestige ?? 0)
            .Append(" B=")
            .Append(character.RuntimeStatus?.currentBlock ?? 0)
            .Append(" C=").Append(character.TurnClashPowerBonus);

        if (!string.IsNullOrWhiteSpace(customResourceKey))
        {
            builder.Append(" R[")
                .Append(customResourceKey)
                .Append("]=")
                .Append(SkillResourceAccess.Get(
                    character,
                    customResourceKey));
        }

        if (character.BodyParts != null)
        {
            foreach (BodyPart part in character.BodyParts)
            {
                if (part == null)
                    continue;

                builder.Append(" PART[")
                    .Append(part.Type)
                    .Append("]=")
                    .Append(Mathf.CeilToInt(part.PartHP))
                    .Append('/')
                    .Append(part.State)
                    .Append('/');

                AppendStatuses(
                    builder,
                    character,
                    part);
            }
        }

        builder.Append(" STATUS=");
        foreach (StatusEffect status in character.StatusEffects)
        {
            if (status == null)
                continue;

            builder.Append(status.GetType().Name)
                .Append(':')
                .Append(status.Stack)
                .Append(':')
                .Append(status.Duration)
                .Append(',');
        }

        foreach (CombatMechanic mechanic in character.Mechanics)
        {
            if (mechanic == null)
                continue;

            builder.Append(" M[")
                .Append(mechanic.GetType().Name)
                .Append("]=");

            foreach (PropertyInfo property in
                     mechanic.GetType().GetProperties(Flags))
            {
                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0 ||
                    !IsSimple(property.PropertyType))
                {
                    continue;
                }

                try
                {
                    builder.Append(property.Name)
                        .Append(':')
                        .Append(property.GetValue(mechanic))
                        .Append(',');
                }
                catch
                {
                }
            }

            Type mechanicType = mechanic.GetType();
            while (mechanicType != null &&
                   mechanicType != typeof(CombatMechanic))
            {
                foreach (FieldInfo field in
                         mechanicType.GetFields(Flags))
                {
                    if (!IsSimple(field.FieldType))
                        continue;

                    try
                    {
                        builder.Append("_")
                            .Append(field.Name)
                            .Append(':')
                            .Append(field.GetValue(mechanic))
                            .Append(',');
                    }
                    catch
                    {
                    }
                }

                mechanicType = mechanicType.BaseType;
            }
        }

        builder.Append(';');
    }

    private static void AppendStatuses(
        StringBuilder builder,
        Character character,
        BodyPart part)
    {
        StatusEffect[] statuses =
        {
            character.GetPartStatus<Bleeding>(part),
            character.GetPartStatus<Burn>(part),
            character.GetPartStatus<Stun>(part)
        };

        foreach (StatusEffect status in statuses)
        {
            if (status == null)
                continue;

            builder.Append(status.GetType().Name)
                .Append(':')
                .Append(status.Stack)
                .Append(':')
                .Append(status.Duration)
                .Append(',');
        }
    }

    private static bool IsSimple(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal);
    }

    public static bool TryClearPrivateCollection(
        object target,
        string name)
    {
        if (!TryGetPrivateField(
                target,
                name,
                out object value))
        {
            return false;
        }

        switch (value)
        {
            case IDictionary dictionary:
                dictionary.Clear();
                return true;

            case IList list:
                list.Clear();
                return true;
        }

        MethodInfo clear = value?.GetType().GetMethod(
            "Clear",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            Type.EmptyTypes,
            null);

        if (clear == null)
            return false;

        clear.Invoke(value, null);
        return true;
    }

    public static bool TryGetPrivateField(
        object target,
        string name,
        out object value)
    {
        value = null;

        if (target == null || string.IsNullOrWhiteSpace(name))
            return false;

        FieldInfo field = FindField(target.GetType(), name);
        if (field == null)
            return false;

        value = field.GetValue(target);
        return true;
    }

    public static bool TrySetPrivateField(
        object target,
        string name,
        object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(name))
            return false;

        FieldInfo field = FindField(
            target.GetType(),
            name);

        if (field == null)
            return false;

        field.SetValue(target, value);
        return true;
    }

    private static FieldInfo FindField(
        Type type,
        string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, Flags);
            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}
