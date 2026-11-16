namespace ProjectAbyss.Abilities
{
    /// <summary>Asset identity reference provided by a validated catalog.
    /// UnityEditor.AssetDatabase is intentionally NOT used in runtime code.</summary>
    public readonly struct SkillAssetRef
    {
        public readonly SkillDefinition Definition;
        public readonly string AssetGuid;
        public SkillAssetRef(SkillDefinition definition,string guid)
        {
            Definition=definition;AssetGuid=guid??string.Empty;
        }
    }

}
