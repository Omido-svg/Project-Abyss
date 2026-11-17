using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>Existing operation IDs in one place. These methods build data; they never apply an effect.</summary>
    public static class AbilityEffects
    {
        public static AbilityOperationData Energy(int amount) => Custom("resource.energy", amount: amount);
        public static AbilityOperationData Block(int amount) => Custom("resource.block", amount: amount);
        public static AbilityOperationData FixedDamage(int amount) => Custom("damage.fixed", amount: amount);
        public static AbilityOperationData Resource(string key, int amount) => Custom("resource.custom", amount: amount, key: key);

        public static AbilityOperationData AddStatus(StatusEffectId status, int stacks, int duration)
        {
            var effect = Custom("status.add", amount: stacks, key: status.ToString());
            effect.Duration = duration;
            return effect;
        }

        public static AbilityOperationData Custom(string typeId, ScriptableObject settings = null, int amount = 0, string key = null)
        {
            SkillRules.RequireId(typeId, nameof(typeId));
            return new AbilityOperationData { TypeId = typeId, Settings = settings, Amount = amount, Key = key };
        }
    }
}
