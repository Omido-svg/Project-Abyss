using System;
using UnityEngine;

/// <summary>
/// 전투 연출 중 3D 캐릭터 선택, 상세 패널, 아웃라인 입력을 한 번에 잠근다.
/// </summary>
public static class BattlePresentationInteractionLock
{
    private static bool isLocked;

    public static bool IsLocked => isLocked;

    public static event Action<bool> LockChanged;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isLocked = false;
        LockChanged = null;
    }

    public static void SetLocked(bool value)
    {
        if (isLocked == value)
            return;

        isLocked = value;

        if (value)
            BattleCharacterPointerRouter.ClearPersistentSelection();

        OutlineController.SetGlobalSuppressed(value);

        try
        {
            LockChanged?.Invoke(value);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
