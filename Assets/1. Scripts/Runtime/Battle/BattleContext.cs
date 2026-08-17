using System.Collections.Generic;

public interface IBattleRule
{
    int ModifyStaggerDamage(
        Character source,
        Character target,
        int amount);
}

public class BattleContext
{
    public Character Player;

    public List<Character> Enemies = new();

    public BattleEvent _battleEvent = new();

    public BattleEffectResolver EffectResolver { get; set; }

    public BattleManager battleManager;

    // Character Validator의 격리 Fixture는 실제 BattleManager 없이도
    // 동일한 피해·기세 파이프라인을 사용해야 한다.
    public DamageManager VerificationDamageManager { get; set; }
    public MomentumManager VerificationMomentumManager { get; set; }

    // 격리 Character Validator는 실제 전투 계산만 실행하고,
    // Scene의 UI/VFX/Timeline 큐에는 요청을 보내지 않는다.
    public bool SuppressPresentation { get; set; }

    public DamageManager ResolveDamageManager() =>
        battleManager?.DamageManager ??
        VerificationDamageManager;

    public MomentumManager ResolveMomentumManager() =>
        battleManager?.MomentumManager ??
        VerificationMomentumManager;
    public readonly List<IBattleRule> BattleRules = new();

    public BattleRuleSettings Rules { get; set; } =
        new BattleRuleSettings();

    public List<Character> AllCharacters
    {
        get
        {
            List<Character> result = new();

            if (Player != null)
                result.Add(Player);

            if (Enemies != null)
                result.AddRange(Enemies);

            return result;
        }
    }
}