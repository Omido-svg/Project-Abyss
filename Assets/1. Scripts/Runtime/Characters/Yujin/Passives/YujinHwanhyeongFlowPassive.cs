using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy 환형 보조 Augment asset.
/// 0916 정본에서는 환형 완료 자체가 살수의 감 획득 원인이 아니므로
/// 런타임 메커닉을 생성하지 않는다. 직렬화 필드와 타입은 기존 asset 호환용으로 유지한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Augment/Yujin/Hwanhyeong Flow",
    fileName = "Yujin_HwanhyeongFlow")]
public sealed class YujinHwanhyeongFlowPassive :
    CharacterAugment
{
    [SerializeField, Min(0)]
    private int senseGainOnWeaponSwitch = 0;

    // 0916: 환형 완료 자체는 살수의 감 획득 원인이 아니다.
    // 필드는 구 asset 직렬화 호환용으로만 보존한다.
    public int SenseGainOnWeaponSwitch => 0;

    public override bool CanApplyTo(
        Character owner)
    {
        return owner is Yujin;
    }

    public override void CreateMechanics(
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        // 0916 Source of Truth: 살수의 감은 TurnStart/카드/파괴/처치로 획득한다.
        // 환형 완료 +1은 legacy rule이므로 더 이상 runtime mechanic을 생성하지 않는다.
    }
}
/// <summary>
/// ScriptableObject에는 런타임 상태를 두지 않고,
/// 실제 WeaponChanged 반응은 전투 메커닉 인스턴스가 소유한다.
/// </summary>
public sealed class YujinHwanhyeongFlowPassiveMechanic :
    CombatMechanic
{
    private readonly int senseGain;
    private YujinMechanic yujinMechanic;

    public override string MechanicName =>
        "Yujin Hwanhyeong Flow Passive";

    public int SenseGain => senseGain;

    public YujinHwanhyeongFlowPassiveMechanic(
        int senseGain)
    {
        this.senseGain = Mathf.Max(0, senseGain);
    }

    public override void OnRegister()
    {
        if (owner is not Yujin)
        {
            throw new System.InvalidOperationException(
                "환형의 흐름 패시브는 유진에게만 등록할 수 있습니다.");
        }

        yujinMechanic =
            owner.GetMechanic<YujinMechanic>();

        if (yujinMechanic == null)
        {
            throw new System.InvalidOperationException(
                "환형의 흐름 패시브가 YujinMechanic을 찾지 못했습니다.");
        }

        yujinMechanic.WeaponChanged +=
            HandleWeaponChanged;
    }

    public override void OnUnregister()
    {
        if (yujinMechanic != null)
        {
            yujinMechanic.WeaponChanged -=
                HandleWeaponChanged;
        }

        yujinMechanic = null;
    }

    private void HandleWeaponChanged(
        YujinWeaponType previous,
        YujinWeaponType current)
    {
        if (previous == current ||
            senseGain <= 0 ||
            yujinMechanic == null)
        {
            return;
        }

        // 0916 legacy compatibility: weapon switch no longer grants Sense.
    }
}
