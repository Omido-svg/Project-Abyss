using System.Collections.Generic;
using UnityEngine;

public static class AnimatorStateValidator
{
    public static bool Validate(
        Animator animator,
        IReadOnlyList<string> requiredStates,
        Object context = null,
        bool logWarnings = true)
    {
        if (requiredStates == null || requiredStates.Count == 0)
            return true;

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            if (logWarnings)
                Debug.LogWarning("[ANIMATOR VALIDATION] Animator 또는 Controller가 없습니다.", context);

            return false;
        }

        bool valid = true;

        for (int i = 0; i < requiredStates.Count; i++)
        {
            string stateName = requiredStates[i];

            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int hash = Animator.StringToHash(stateName);
            bool found = false;

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (!animator.HasState(layer, hash))
                    continue;

                found = true;
                break;
            }

            if (found)
                continue;

            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[ANIMATOR VALIDATION] State 누락 / Animator={animator.name}, State={stateName}",
                    context != null ? context : animator);
            }
        }

        return valid;
    }
}
