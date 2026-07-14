using System.Reflection;
using UnityEngine;

// 올라프 고유 메커닉에서 발생하는 추가 피해와 자해도
// 가능한 한 표준 DamageManager 파이프라인을 사용하게 한다.
public static class OlafCombatPipeline
{
    public static DamageContext ApplyDamage(
        Character owner,
        DamageRequest request)
    {
        DamageManager manager =
            ResolveDamageManager(owner);

        if (manager != null)
        {
            return manager.ApplyDamageContext(
                request);
        }

        ApplyFallback(
            owner,
            request);

        return null;
    }

    public static DamageManager ResolveDamageManager(
        Character owner)
    {
        object battleManager =
            owner?.BattleContext?.battleManager;

        if (battleManager == null)
            return null;

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        System.Type type =
            battleManager.GetType();

        PropertyInfo property =
            type.GetProperty(
                "DamageManager",
                flags);

        if (property?.GetValue(battleManager)
            is DamageManager propertyManager)
        {
            return propertyManager;
        }

        FieldInfo field =
            type.GetField(
                "damageManager",
                flags);

        return field?.GetValue(battleManager)
            as DamageManager;
    }

    private static void ApplyFallback(
        Character owner,
        DamageRequest request)
    {
        BattleEffectResolver resolver =
            owner?.BattleContext?.EffectResolver;

        if (resolver == null ||
            request.TargetCharacter == null ||
            request.Damage <= 0)
        {
            return;
        }

        Debug.LogWarning(
            "[OlafCombatPipeline] DamageManager를 찾지 못해 " +
            "호환용 직접 적용 경로를 사용합니다.");

        if (request.Type == DamageType.StatusPart &&
            request.TargetPart != null)
        {
            resolver.ApplyStatusPartDamage(
                EffectRequest.StatusPartDamage(
                    request.SourceCharacter,
                    request.TargetCharacter,
                    request.TargetPart,
                    request.Damage,
                    request.SourceEffect));

            return;
        }

        if (request.Type == DamageType.True ||
            request.Type == DamageType.SelfCost)
        {
            resolver.ApplyTrueDamage(
                EffectRequest.TrueDamage(
                    request.SourceCharacter,
                    request.TargetCharacter,
                    request.Damage,
                    request.SourceEffect));

            return;
        }

        resolver.ApplyDirectDamage(
            EffectRequest.DirectDamage(
                request.SourceCharacter,
                request.TargetCharacter,
                request.Damage,
                request.SourceAction));
    }
}
