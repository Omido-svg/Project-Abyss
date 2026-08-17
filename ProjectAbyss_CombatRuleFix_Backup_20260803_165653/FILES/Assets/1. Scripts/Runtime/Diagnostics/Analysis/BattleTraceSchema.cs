using System;
using System.Collections.Generic;

public static class BattleTraceSchema
{
    public const string Version = "2.0.0";
    public const string ConsolePrefix = "[PA_TRACE]";
}

[Serializable]
public sealed class BattleTraceRecord
{
    public string schemaVersion = BattleTraceSchema.Version;
    public string sessionId;
    public long sequence;
    public string utcTimestamp;
    public int frame;
    public float realtime;

    public string eventType;
    public string source;
    public int turn;
    public string phase;
    public int momentum;
    public string message;

    public BattleTraceRuleSnapshot rules;
    public BattleTraceAction action;
    public BattleTraceAction otherAction;
    public BattleTraceClash clash;
    public BattleTraceDamage damage;
    public BattleTraceStatus status;
    public BattleTraceResource resource;
    public BattleTraceStateSnapshot snapshot;
}

[Serializable]
public sealed class BattleTraceRuleSnapshot
{
    public int momentumMinimum;
    public int momentumMaximum;
    public int lastStandThreshold;
    public int disadvantageThreshold;
    public int advantageThreshold;
    public int overwhelmThreshold;

    public float lastStandMultiplier;
    public float disadvantageMultiplier;
    public float balanceMultiplier;
    public float advantageMultiplier;
    public float overwhelmMultiplier;
    public float maximumOverwhelmMultiplier;

    public int hitShift;
    public int duelExchangeShift;
    public int lastStandHitShiftMultiplier;

    public int defaultExchangeRollCount;
    public int speedWeight;
    public int maxTieRerolls;
    public int maxCharacterRerollsPerExchange;

    public int prestigeExchangeParticipant;
    public int prestigeOneSidedParticipant;
    public bool prestigeExcludesPreparation;
}

[Serializable]
public sealed class BattleTraceStateSnapshot
{
    public int momentum;
    public List<BattleTraceCharacterState> characters = new();
    public List<BattleTraceAction> plannedActions = new();
}

[Serializable]
public sealed class BattleTraceCharacterState
{
    public string characterId;
    public string name;
    public string side;
    public bool initialized;
    public bool dead;
    public bool usesBodyParts;

    public int hp;
    public int maxHp;
    public int prestige;
    public int maxPrestige;
    public int energy;
    public int maxEnergy;

    public List<BattleTraceBodyPartState> parts = new();
    public List<BattleTraceStatusState> statuses = new();
}

[Serializable]
public sealed class BattleTraceBodyPartState
{
    public string part;
    public string state;
    public int hp;
    public int maxHp;
    public int revision;
    public List<BattleTraceStatusState> statuses = new();
}

[Serializable]
public sealed class BattleTraceStatusState
{
    public string name;
    public string type;
    public int stack;
    public int duration;
    public string ownerPart;
}

[Serializable]
public sealed class BattleTraceAction
{
    public long actionId;
    public long targetActionId;
    public int actionIndex;

    public string ownerId;
    public string ownerName;
    public string ownerSide;
    public string ownerPart;

    public string targetId;
    public string targetName;
    public string targetSide;
    public string targetPart;

    public string skill;
    public string actionType;
    public string phase;

    public int speed;
    public int purePower;
    public int clashPower;
    public int speedModifier;
    public int momentumModifier;
    public int preparationModifier;
    public bool critical;
    public int exchangeRollCount;
    public int energyCost;
}

[Serializable]
public sealed class BattleTraceRoll
{
    public string resolver;
    public int basePower;
    public int rawValue;
    public int modifiedValue;
    public int externalModifier;
    public int finalPower;
    public int speedModifier;
    public int momentumModifier;
    public int preparationModifier;
    public int clashPower;
    public bool isMax;
    public bool critical;
    public bool reused;
    public string display;
    public string chinchiroCombination;
    public int chinchiroBonus;
    public int chinchiroSelfDamage;
    public List<int> diceValues = new();
    public List<bool> coinFaces = new();
    public List<int> coinValues = new();
}

[Serializable]
public sealed class BattleTraceClash
{
    public bool isClash;
    public bool draw;
    public string winnerOwnerId;
    public string loserOwnerId;
    public int firstExchangeWins;
    public int secondExchangeWins;
    public int pairedExchangeCount;
    public int oneSidedHitCount;
    public int totalDamage;
    public int momentumShift;
    public int prestigeGain;
    public List<BattleTraceExchange> exchanges = new();
}

[Serializable]
public sealed class BattleTraceExchange
{
    public int index;
    public bool oneSided;
    public bool tie;
    public bool cancelled;
    public int firstClashPower;
    public int secondClashPower;
    public string winnerOwnerId;
    public string loserOwnerId;
    public int damage;
    public int momentumShift;
    public int firstPrestigeGain;
    public int secondPrestigeGain;
    public BattleTraceRoll firstRoll;
    public BattleTraceRoll secondRoll;
}

[Serializable]
public sealed class BattleTraceDamage
{
    public string damageType;

    public int rawPower;
    public float skillMultiplier;
    public float momentumMultiplier;

    public int baseDamage;
    public int attackerModifiedDamage;
    public bool critical;

    public int guardBefore;
    public int guardAbsorbed;
    public int guardAfter;
    public int damageAfterGuard;

    public int targetModifiedDamage;

    public int protectionValue;
    public int protectionAbsorbed;
    public int damageAfterProtection;

    public int finalDamage;
    public int appliedDamage;
    public int hpDamage;
    public int partDamage;
    public int directHpDamage;

    public int targetHpBefore;
    public int targetHpAfter;

    public bool hasPartSnapshot;
    public int targetPartHpBefore;
    public int targetPartHpAfter;
    public string targetPartStateBefore;
    public string targetPartStateAfter;

    public bool weakened;
    public bool broken;
    public bool killed;
    public bool directHpRoute;

    public List<BattleTraceDamageStage> stages = new();
}

[Serializable]
public sealed class BattleTraceDamageStage
{
    public string stage;
    public int damage;
    public int targetHp;
    public int targetPartHp;
    public int guardValue;
    public int protectionValue;
    public bool hasTargetPart;
    public string targetPartState;
}

[Serializable]
public sealed class BattleTraceStatus
{
    public string targetId;
    public string targetName;
    public string targetPart;
    public string effect;
    public string effectType;
    public string applyKind;
    public string timing;
    public string removeReason;
    public int stackBefore;
    public int stackAfter;
    public int durationBefore;
    public int durationAfter;
    public bool transferred;
}

[Serializable]
public sealed class BattleTraceResource
{
    public string ownerId;
    public string ownerName;
    public string ownerSide;
    public string key;
    public int before;
    public int after;
    public int maximum;
    public int delta;
    public string reason;
    public long sourceActionId;
    public string sourceSkill;
}

[Serializable]
public sealed class BattleTraceSessionSummary
{
    public string schemaVersion = BattleTraceSchema.Version;
    public string sessionId;
    public string startedUtc;
    public string endedUtc;
    public string outcome;
    public int completedTurns;
    public int totalEvents;
    public int playerDamageDealt;
    public int playerDamageTaken;
    public int playerActions;
    public int enemyActions;
    public int clashes;
    public int playerKills;
    public int enemyKills;
    public int playerEnergySpent;
    public int enemyEnergySpent;
    public string eventsFile;
}
