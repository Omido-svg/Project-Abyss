#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// R7-8: the one 1029 fully numeric counter soldier retaliation.
/// Deterministic one-roll, six power, one-sided, no skill effect graph.
/// </summary>
public static class EnemyCounterAssetVerification
{
    public const string Path=
        "Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029/"+
        "enemy1029.stage1.counter_guard.revenge.asset";


    public static void Verify()
    {
        var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(Path);
        if(d==null||d.SkillId!=EnemyCounterReaction.CounterSkillId||
           d.ActionType!=ActionType.NormalAttack||d.EffectiveRollCount!=1||
           d.BasePowerRule!=SkillBasePowerRule.FixedBase||d.BasePower!=6||
           d.DiceMin!=0||d.DiceMax!=0||d.EnergyCost!=0||
           d.EffectEntries.Count!=0||d.HasEffectEntries||
           AssetDatabase.AssetPathToGUID(Path)?.Length!=32||
           EditorUtility.IsDirty(d))
            throw new InvalidOperationException("Invalid R7-8 counter skill source");
        var skill=d.CreateRuntimeSkill();
        if(skill==null||skill.ExchangeRollCount!=1||
           skill.MinPower!=6||skill.MaxPower!=6||
           skill.CanClash)
            throw new InvalidOperationException("Counter power not one-sided 6");
        // Verify actual NormalEnemy factory, which usually replaces Dice with D8.
        var factory=typeof(NormalEnemy).Assembly.GetType(
            "NormalEnemyRuntimeSkill")?.GetMethod("Create",
            System.Reflection.BindingFlags.Public|
            System.Reflection.BindingFlags.Static);
        var enemyRuntime=factory?.Invoke(null,new object[]{d}) as Skill;
        if(enemyRuntime==null||enemyRuntime.MinPower!=6||
           enemyRuntime.MaxPower!=6||enemyRuntime.ExchangeRollCount!=1||
           enemyRuntime.CanClash)
            throw new InvalidOperationException(
                "Counter runtime factory changed exact six-power roll");
        Debug.Log("[R7-8 Counter] PASS 1 asset 1 roll 6 power new GUID");
    }
}
#endif