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

    /// <summary>
    /// 전투 Composition Root가 주입하는 런타임 서비스 모음.
    /// BattleContext는 BattleManager/Scene object를 직접 참조하지 않는다.
    /// </summary>
    public BattleRuntimeServices Services { get; set; }

    // Run/scene composition root가 선택적으로 주입한다. 미주입 시 전투 코어는 정상 동작하고
    // 감정 증강 제안만 생성하지 않는다.
    public EmotionType? SelectedEmotion { get; set; }
    public EmotionAugmentCatalog EmotionAugmentCatalog { get; set; }

    // 격리 Character Validator는 실제 전투 계산만 실행하고,
    // Scene의 UI/VFX/Timeline 큐에는 요청을 보내지 않는다.
    public bool SuppressPresentation { get; set; }

    public DamageManager ResolveDamageManager() =>
        Services?.DamageManager;

    public MomentumManager ResolveMomentumManager() =>
        Services?.MomentumManager;
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