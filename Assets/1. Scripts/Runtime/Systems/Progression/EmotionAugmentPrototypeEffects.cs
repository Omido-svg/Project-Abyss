// Compatibility shell retained intentionally.
//
// Phase E-1 v1 stored EmotionAugmentPrototypeEffectDefinition in this file.
// In v2 the ScriptableObject was moved to EmotionAugmentPrototypeEffectDefinition.cs,
// which is the correct Unity serialization layout.  Keep this old source path free of
// runtime classes so old MonoScript GUIDs cannot resolve to an unrelated non-Unity class.
// PhaseEContentMigration v3 recreates migration-owned prototype effect assets with the
// correct standalone script reference.
