namespace ProjectAbyss.WeaponSystem
{
    /// <summary>
    /// Implement this in a game-side adapter to receive framework hit data.
    /// HP, statuses, body-parts, clash rules and damage formulas stay outside the package.
    /// </summary>
    public interface IWeaponDamageReceiver
    {
        void ReceiveWeaponHit(in WeaponHitInfo hit);
    }
}
