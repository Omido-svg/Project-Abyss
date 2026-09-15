public sealed class CharacterSkillMaintenanceService
{
    private readonly Character character;
    public CharacterSkillMaintenanceService(Character character) => this.character = character;

    public bool TryReplace(SkillDefinition equipped, SkillDefinition replacement, out string reason)
    {
        CharacterSkillLoadoutRuntime loadout = character?.CombatRulesRuntime?.Loadout;
        if (loadout == null)
        {
            reason = "캐릭터의 런타임 장착 데이터가 초기화되지 않았습니다.";
            return false;
        }
        return loadout.TryReplaceInMaintenance(equipped, replacement, out reason);
    }
}
