/// <summary>
/// Explicit flat, AFTER-resistance extra Stagger damage, per consumed RED
/// attack roll, before Sturdy/Disarm target flat algebra.
/// Does not replace IAdditionalStaggerDamageProvider (which is pre-resist).
/// </summary>
public interface IAdditionalFinalStaggerDamageProvider20261103
{
    int GetAdditionalFinalStaggerDamage(BattleAction action,Character target);
}