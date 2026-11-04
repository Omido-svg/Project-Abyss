// Compatibility shell retained intentionally.
//
// Phase E-1 v1 stored ProvisionalEmotionEffectDefinition in this file.
// In v2 the ScriptableObject was moved to ProvisionalEmotionEffectDefinition.cs,
// which is the correct Unity serialization layout.  Keep this old source path free of
// runtime classes so old MonoScript GUIDs cannot resolve to an unrelated non-Unity class.
// ContentDefinitionMigration v3 recreates migration-owned prototype effect assets with the
// correct standalone script reference.
