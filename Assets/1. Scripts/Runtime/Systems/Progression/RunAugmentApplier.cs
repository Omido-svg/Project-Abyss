using UnityEngine;

// 런 증강(전투 후 보상 · 스탯형)을 전투 빌드에 올리는 어댑터.
// 감정 증강(전투 한정)과 별개 층 — 런이 들고 있는 RunAugmentInventory를 전투 시작 때 읽는다.
//   속도 / 위세 획득량  → ModifyStatus  (CurrentStatus.minSpeed · maxSpeed · prestigeGainMultiplier)
//   최대체력 %           → ModifyBodyPart (BodyPart.IncreaseMaxHPPercent — 모든 부위)
//   매 턴 방어도 +N      → CreateMechanics (턴 시작마다 owner.AddBlock — 규칙서 10.3대로 누적, 전투 끝나면 초기화)
// 퍼센트는 합산(곱연산 금지). 위력은 건드리지 않는다.
public sealed class RunAugmentApplier : CharacterAugment
{
    private RunAugmentInventory source;

    public static RunAugmentApplier Create(RunAugmentInventory inventory)
    {
        var applier = CreateInstance<RunAugmentApplier>();
        applier.name = "RunAugmentApplier";
        applier.hideFlags = HideFlags.DontSave;
        applier.source = inventory;
        return applier;
    }

    public bool HasAnyCombatEffect =>
        source != null && (
            source.Sum(RunAugmentEffect.SpeedPlus) > 0 ||
            source.Sum(RunAugmentEffect.PrestigeGainPercent) > 0 ||
            source.Sum(RunAugmentEffect.MaxHpPercent) > 0 ||
            source.Sum(RunAugmentEffect.ArmorPerTurn) > 0);

    public override void ModifyStatus(Character owner, CurrentStatus status)
    {
        if (owner == null || status == null || source == null) return;

        int speed = source.Sum(RunAugmentEffect.SpeedPlus);
        if (speed > 0)
        {
            status.minSpeed += speed;
            status.maxSpeed += speed;
        }

        int prestigePct = source.Sum(RunAugmentEffect.PrestigeGainPercent);
        if (prestigePct > 0)
            status.prestigeGainMultiplier += prestigePct / 100f;   // 합산(1 + ΣN%)
    }

    public override void ModifyBodyPart(Character owner, BodyPart part)
    {
        if (owner == null || part == null || source == null) return;

        int hpPct = source.Sum(RunAugmentEffect.MaxHpPercent);
        if (hpPct > 0)
            part.IncreaseMaxHPPercent(hpPct / 100f, true);
    }

    public override void CreateMechanics(
        CharacterBuildMechanicContext context,
        System.Collections.Generic.List<CombatMechanic> output)
    {
        if (output == null || !context.IsValid || source == null) return;

        int armor = source.Sum(RunAugmentEffect.ArmorPerTurn);
        if (armor > 0)
            output.Add(new RunAugmentTurnBlockMechanic(armor));
    }
}

/// <summary>런 증강 「방어도 증가」 — 매 턴 시작 시 방어도 +N (감정 증강 신의 「턴 시작 방어도」와 같은 패턴).</summary>
public sealed class RunAugmentTurnBlockMechanic : CombatMechanic
{
    private readonly int amount;

    public RunAugmentTurnBlockMechanic(int amount) { this.amount = Mathf.Max(0, amount); }

    public override string MechanicName => "Run Augment / Turn Start Block";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead || amount <= 0) return;
        owner.AddBlock(amount);
    }
}
