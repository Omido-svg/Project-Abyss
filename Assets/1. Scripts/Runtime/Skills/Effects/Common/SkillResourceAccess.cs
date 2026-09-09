using UnityEngine;

/// <summary>
/// Skill Effect가 Character의 정식 자원 API만 사용하도록 연결한다.
/// Reflection/fallback 저장소를 두지 않아 Energy 변경 이벤트와
/// CombatResourceBank의 clamp/max 정책을 우회하지 않는다.
/// </summary>
public static class SkillResourceAccess
{
    public static int Get(
        Character owner,
        string key)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        return owner.GetCustomResource(key);
    }

    public static int Set(
        Character owner,
        string key,
        int value)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        owner.SetCustomResource(
            key,
            Mathf.Max(0, value));

        return owner.GetCustomResource(key);
    }

    public static int Modify(
        Character owner,
        string key,
        int amount,
        int minimum = 0,
        int maximum = int.MaxValue)
    {
        if (owner == null ||
            string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        int safeMinimum =
            Mathf.Max(
                0,
                Mathf.Min(minimum, maximum));

        int safeMaximum =
            Mathf.Max(
                safeMinimum,
                maximum);

        long next =
            (long)Get(owner, key) +
            amount;

        int bounded =
            next > safeMaximum
                ? safeMaximum
                : next < safeMinimum
                    ? safeMinimum
                    : (int)next;

        return Set(
            owner,
            key,
            bounded);
    }
}
