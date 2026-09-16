using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Progression/Phase E Content Manifest",
    fileName = "PhaseEContentManifest")]
public sealed class PhaseEContentManifest : ScriptableObject
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion = CurrentSchemaVersion;
    public PhaseEEnemyContentCatalog EnemyContent;
    public EmotionAugmentCatalog EmotionCatalog;
    public RunItemCatalog RunItemCatalog;

    [Header("Expected canonical counts")]
    public int ExpectedEmotionCount = 63;
    public int ExpectedCommonItemCount = 30;
    public int ExpectedClassItemCountPerCharacter = 10;
    public int ExpectedPlayableCharacterCount = 3;

    [Header("Migration state")]
    public bool HifumiPokerFaceSynced;
    public bool UpgradeDecompositionComplete;
    public bool OlafSkillPoolComplete;
    public bool YujinSkillPoolComplete;

    [Header("C-44 Roll Texture Audit")]
    public int RollTextureScanned;
    public int RollTexturePassed;
    public int RollTexturePending;
    public int RollTextureViolationCount;
    public List<string> RollTextureIssues = new();

    [Header("Source gaps intentionally left Pending")]
    public List<string> PendingReasons = new();
}
