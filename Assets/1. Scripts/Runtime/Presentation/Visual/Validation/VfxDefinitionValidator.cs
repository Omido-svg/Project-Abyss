using UnityEngine;
using UnityEngine.VFX;

public static class VfxDefinitionValidator
{
    public static bool Validate(
        BattleVfxDefinition definition,
        Object context = null,
        bool logWarnings = true)
    {
        if (definition == null)
        {
            if (logWarnings)
                Debug.LogWarning("[VFX VALIDATION] Definition이 없습니다.", context);

            return false;
        }

        bool valid = true;
        GameObject prefab = definition.EffectPrefab;

        if (prefab == null)
        {
            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[VFX VALIDATION] Prefab 누락 / Definition={definition.name}",
                    context);
            }
        }
        else
        {
            if (prefab.GetComponentInChildren<VisualEffect>(true) == null)
            {
                valid = false;

                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[VFX VALIDATION] VisualEffect 컴포넌트가 없는 Prefab입니다. " +
                        $"Definition={definition.name}, Prefab={prefab.name}",
                        context);
                }
            }

            if (ContainsLegacyEmitter(prefab))
            {
                valid = false;

                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[VFX VALIDATION] Legacy particle emitter가 포함된 Prefab은 사용할 수 없습니다. " +
                        $"Definition={definition.name}, Prefab={prefab.name}",
                        context);
                }
            }
        }

        if (definition.DestroyAfterLifetime && definition.Lifetime <= 0f)
        {
            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[VFX VALIDATION] Lifetime이 0 이하입니다. / Definition={definition.name}",
                    context);
            }
        }

        if (definition.UsePooling && !definition.DestroyAfterLifetime && logWarnings)
        {
            Debug.LogWarning(
                $"[VFX VALIDATION] 풀링 VFX인데 자동 반환이 꺼져 있습니다. " +
                $"custom player가 PooledInstance.Release()를 호출해야 합니다. / Definition={definition.name}",
                context);
        }

        if (definition.UsePooling && definition.MaxPoolSize <= 0)
        {
            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[VFX VALIDATION] Pooling이 켜졌지만 MaxPoolSize가 0입니다. / Definition={definition.name}",
                    context);
            }
        }

        return valid;
    }

    private static bool ContainsLegacyEmitter(GameObject prefab)
    {
        if (prefab == null)
            return false;

        Component[] components = prefab.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (component.GetType().FullName == "UnityEngine." + "Particle" + "System")
                return true;
        }

        return false;
    }
}
