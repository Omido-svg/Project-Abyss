#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// R7-8: the one 1029 fully numeric counter soldier retaliation.
/// Deterministic one-roll, six power, one-sided, no skill effect graph.
/// </summary>
public static class R7Stage8EnemyCounterSkillMigration20261103
{
    public const string Path=
        "Assets/2. Data/Resources/ProjectAbyss/Stage1Enemy1029/"+
        "enemy1029.stage1.counter_guard.revenge.asset";
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("EditMode only");
        if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path)!=null||
           File.Exists(Path))
            throw new InvalidOperationException("Refusing overwrite: "+Path);
        var d=ScriptableObject.CreateInstance<SkillDefinition>();
        d.name="반격병 되받아치기";
        d.SkillName="반격병 되받아치기";
        d.ActionType=ActionType.NormalAttack;
        d.Color=SkillColor.Red;
        d.BasePowerRule=SkillBasePowerRule.FixedBase;
        d.BasePower=6;
        d.BasePowerFlatAdjustment=0;
        d.ExchangeRollCount=1;
        d.Rolls.Clear();
        d.ResolverType=SkillResolverType.Dice;
        d.DiceMin=0;
        d.DiceMax=0;
        d.OverrideEnergyCost=true;
        d.EnergyCost=0;
        d.OverrideCanClash=true;
        d.CanClashValue=false;
        d.GainPrestige=false;
        d.Description="1029 §12.3 반격병 되받아치기: 고정 위력 6, 단일 굴림,"+
            " 새 합이 없는 일방타격. R6 checkpoint에서만 삽입.";
        var so=new SerializedObject(d);
        so.FindProperty("skillId").stringValue=
            R7EnemyCounterReaction20261103.CounterSkillId;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(d,Path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[R7-8 Counter] Power6 retaliation skill created with new GUID");
    }

    public static void Verify()
    {
        var d=AssetDatabase.LoadAssetAtPath<SkillDefinition>(Path);
        if(d==null||d.SkillId!=R7EnemyCounterReaction20261103.CounterSkillId||
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