using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유진 고유 패시브 — 환형의 흐름.
/// 환형으로 예약한 무기 변경이 다음 턴 시작에 실제 완료되었을 때
/// 살수의 감을 획득한다.
///
/// TODO2에서 정확한 보상 수치가 확정되지 않았기 때문에 기본값은 +1이며,
/// 데이터 에셋에서 교체할 수 있도록 직렬화한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Augment/Yujin/Hwanhyeong Flow",
    fileName = "Yujin_HwanhyeongFlow")]
public sealed class YujinHwanhyeongFlowPassive :
    CharacterAugment
{
    [SerializeField, Min(0)]
    private int senseGainOnWeaponSwitch = 1;

    public int SenseGainOnWeaponSwitch =>
        Mathf.Max(0, senseGainOnWeaponSwitch);

    public override bool CanApplyTo(
        Character owner)
    {
        return owner is Yujin;
    }

    public override void CreateMechanics(
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        if (output == null ||
            !context.IsValid ||
            !CanApplyTo(context.Owner) ||
            SenseGainOnWeaponSwitch <= 0)
        {
            return;
        }

        output.Add(
            new YujinHwanhyeongFlowPassiveMechanic(
                SenseGainOnWeaponSwitch));
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

        yujinMechanic.GrantSense(
            senseGain);
    }
}
