using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R7-8 enemy-source identity and canonical 1029 normal-enemy role policy.
/// Approved identities only; no resurrection of legacy SkillEffect.Apply.
/// Numerical 12-species card authoring outside this contract stays pending.
/// </summary>
public static class EnemyCombatNativeContract
{
    public readonly struct Identity
    {
        public readonly string Id;
        public readonly string AssetName;
        public readonly string SourceGuid;
        public readonly ActionType ActionType;
        public readonly int LegacyEntries;
        public readonly bool Preparation;
        public Identity(string id,string name,string guid,
            ActionType type,int entries,bool preparation=false)
        {
            Id=id;AssetName=name;SourceGuid=guid;
            ActionType=type;LegacyEntries=entries;Preparation=preparation;
        }
    }

    private static readonly Identity[] Approved =
    {
        new Identity("313842626a60416f97ac13a24ca171e3",
            "난폭한 공격(결투)","defb1cbfdaadbd2478772e699b7a47aa",
            ActionType.Duel,1),
        new Identity("356e72e97ac84a3a8757d1a188237f43",
            "전투 태세(도사림)","f9c5728c648630849a9ba0afe641d02a",
            ActionType.Preparation,3,true),
        new Identity("35a5b1e98c8547218682cc24566c91c0",
            "피의 포식 (위세)","4f3c8894b4617e14fb6b1001afa17417",
            ActionType.Prestige,2),
        new Identity("5a239ec7d4944c458ac5329511091871",
            "정예병의 참격(일반공격)","6a7df3e678fffd4439fd494c51209239",
            ActionType.NormalAttack,1),
        new Identity("5fb3b00e68684389a86c92f3e0345061",
            "정예병의 압박(결투)","4b80d9ad43d0de14bb655a20d16b1cb4",
            ActionType.Duel,1),
        new Identity("c09f804f95004e70a9864e0d02ae4509",
            "처형자의 일격(위세)","5ac5af8c409583342b1bd34c2572f671",
            ActionType.Prestige,2),
        new Identity("f8c31ca52c544f93b8d961acab0c9611",
            "물어뜯기(일반공격)","03ee92c875632be45a2c019cd1bc8cf8",
            ActionType.NormalAttack,1)
    };

    public static IReadOnlyList<Identity> All => Approved;

    public static bool HasKnownSkillId(string id)
    {
        if(string.IsNullOrEmpty(id))return false;
        foreach(var source in Approved)
            if(source.Id==id)return true;
        return false;
    }

    public static bool IsApprovedSource(SkillDefinition skill)
    {
        if(skill==null)return false;
        foreach(var source in Approved)
            if(skill.SkillId==source.Id &&
               skill.name==source.AssetName &&
               skill.ActionType==source.ActionType &&
               skill.EffectEntries?.Count==source.LegacyEntries)
                return true;
        return false;
    }

    /// <summary>
    /// The 1029 encounter HP is per ROLE, not per 3/4-member roster.
    /// </summary>
    public static int RoleHp(EnemyRole0922 role) => role switch
    {
        EnemyRole0922.Buffer=>70,
        EnemyRole0922.Debuffer=>80,
        EnemyRole0922.Attacker=>90,
        EnemyRole0922.Duelist=>90,
        EnemyRole0922.Tank=>105,
        _=>90
    };

    /// <summary>
    /// One independent decision per enemy. No global Duel quota.
    /// The role probability is still a legacy provisional policy; 1029
    /// confirms independence but not exact percentages for each species.
    /// Has a Normal fallback for missing/unaffordable Duel authoring.
    /// </summary>
    public static bool ChooseIndependentDuel(float probability,float sample01,
        bool hasDuel,bool hasNormal,bool mustSupport)
    {
        if(mustSupport||!hasDuel)return false;
        if(!hasNormal)return true;
        return Mathf.Clamp01(sample01)<Mathf.Clamp01(probability);
    }
}