using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    /// <summary>
    /// Optional game-side ownership contract. The framework never requires a game-specific Character type.
    /// </summary>
    public interface IWeaponOwner
    {
        GameObject WeaponOwnerObject { get; }
        Transform WeaponRoot { get; }
    }
}
