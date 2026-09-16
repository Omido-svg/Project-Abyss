using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Progression/Phase E Content Manifest",
    fileName = "PhaseEContentManifest")]
public sealed class PhaseEContentManifest : ScriptableObject
{
    public const int CurrentSchemaVersion = 4;
    public const string TempBalanceProfileId = "TEMP_BALANCE_V1";

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

    [Header("Temporary Phase E closure profile")]
    public bool TempBalanceActive;
    public string ActiveBalanceProfile;
    public int TempEmotionProxyCount;
    public int TempCommonItemCount;
    public int TempClassItemCount;
    public int OlafPoolCount;
    public int YujinPoolCount;
    public List<string> TempBalanceNotes = new();

    [Header("C-44 Roll Texture Audit")]
    public int RollTextureScanned;
    public int RollTexturePassed;
    public int RollTexturePending;
    public int RollTextureViolationCount;
    public List<string> RollTextureIssues = new();

    [Header("Source gaps intentionally left Pending")]
    public List<string> PendingReasons = new();
}