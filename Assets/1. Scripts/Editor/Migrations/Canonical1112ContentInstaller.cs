#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Canonical1112ContentInstaller
{
    public const string Root = "Assets/Resources/ProjectAbyss/Canonical1112";
    [MenuItem("Tools/Project Abyss/1112/Install Confirmed Stage One Content")]
    public static void InstallFromMenu() => Debug.Log(Install());

    public static string Install()
    {
        int count = 0;
        foreach (StageOneEnemyKind1112 kind in Enum.GetValues(typeof(StageOneEnemyKind1112)))
        {
            if (kind == StageOneEnemyKind1112.None) continue;
            string dir = Root + "/Enemies/" + kind;
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
            var normal = new List<SkillDefinition>(); var duel = new List<SkillDefinition>();
            SkillDefinition Card(string key, int rolls, int light, int power,
                PhysicalDamageType type, ActionType action, bool blue = false)
            {
                string path = dir + "/" + key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (existing != null) { Add(existing); return existing; }
                var d = ScriptableObject.CreateInstance<SkillDefinition>();
                d.name = key; d.SkillName = kind + " " + key; d.ActionType = action;
                d.Color = blue ? SkillColor.Blue : SkillColor.Red;
                d.OverrideEnergyCost = true; d.EnergyCost = light;
                d.BasePowerRule = SkillBasePowerRule.LegacyExplicit; d.BasePower = power;
                d.ExchangeRollCount = rolls; d.ResolverType = SkillResolverType.Dice;
                d.DiceMin = 1; d.DiceMax = 8; d.PhysicalType = type;
                d.Description = "전투 시스템 규칙(1112) §12.5/12.6 확정값. 적 고유 효과는 StageOneEnemy1112Mechanic에서 처리.";
                var serialized = new SerializedObject(d);
                serialized.FindProperty("skillId").stringValue = "enemy1112.stage1." + kind.ToString().ToLowerInvariant() + "." + key.ToLowerInvariant();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(d, path); count++; Add(d); return d;
                void Add(SkillDefinition value)
                { if (value.ActionType == ActionType.NormalAttack) normal.Add(value); else if (value.ActionType == ActionType.Duel) duel.Add(value); }
            }
            const PhysicalDamageType Cut = PhysicalDamageType.Cut, Blunt = PhysicalDamageType.Blunt, Pierce = PhysicalDamageType.Pierce;
            const ActionType N = ActionType.NormalAttack, D = ActionType.Duel;
            if (kind == StageOneEnemyKind1112.Fanatic)
            { Card("A",2,0,12,Pierce,N); Card("B",1,1,22,Cut,D); Card("C",2,0,12,Pierce,N); Card("D",2,2,14,Cut,D); Card("E",1,0,22,Pierce,D); }
            if (kind == StageOneEnemyKind1112.MachineElite)
            { Card("A",2,0,12,Blunt,N); Card("B",3,0,11,Cut,D); Card("C",2,1,14,Blunt,N,true); Card("D",3,2,14,Cut,D); Card("E",1,0,12,Blunt,D); }
            if (kind == StageOneEnemyKind1112.Inquisitor)
            { Card("A",3,0,11,Pierce,N); Card("B",2,1,14,Cut,N); Card("Confess",1,0,24,Pierce,D); Card("Seal",2,1,14,Pierce,D); Card("Break",2,2,16,Cut,D); }
            if (kind == StageOneEnemyKind1112.MachineBoss)
            { Card("A",3,0,11,Blunt,N); Card("B",2,1,14,Cut,D); Card("C",2,1,14,Blunt,N,true); Card("D",2,2,16,Cut,D); Card("Rampage",4,0,13,Blunt,D); }
            var preparation = Card("Preparation",1,0,0,Blunt,ActionType.Preparation);
            var loadout = AssetDatabase.LoadAssetAtPath<CharacterCombatLoadout>(dir + "/Loadout.asset");
            if (loadout == null)
            {
                loadout = ScriptableObject.CreateInstance<CharacterCombatLoadout>();
                loadout.NormalSkills = normal; loadout.NormalSkillPool = new List<SkillDefinition>(normal);
                loadout.DuelSkills = duel; loadout.DuelSkillPool = new List<SkillDefinition>(duel);
                loadout.PreparationSkills.Add(preparation); loadout.CharacterPreparationPool.Add(preparation);
                AssetDatabase.CreateAsset(loadout, dir + "/Loadout.asset");
            }
            bool human = kind == StageOneEnemyKind1112.Fanatic || kind == StageOneEnemyKind1112.Inquisitor;
            bool boss = kind == StageOneEnemyKind1112.Inquisitor || kind == StageOneEnemyKind1112.MachineBoss;
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(dir + "/Character.asset");
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                data.CharacterName = kind switch { StageOneEnemyKind1112.Fanatic => "광신도", StageOneEnemyKind1112.MachineElite => "기계형", StageOneEnemyKind1112.Inquisitor => "이단 심문관", _ => "기계 보스(임시명)" };
                data.CombatantTier = boss ? CombatantTier.Boss : CombatantTier.EliteEnemy;
                data.maxEnergy = 3; data.maxPrestige = 0; data.minSpeed = human ? 3 : 2; data.maxSpeed = human ? 6 : 5;
                data.TurnStartEnergyPolicy = TurnStartEnergyPolicy.GainFlat; data.TurnStartEnergyAmount = 1;
                data.EnableStaggerGauge = true; data.OverrideMaxStaggerGauge = true; data.MaxStaggerGauge = boss ? 360 : 240;
                data.CombatLoadout = loadout;
                if (human) data.PhysicalResistances.BluntMultiplier = 2; else data.PhysicalResistances.PierceMultiplier = 2;
                for (int i = 0; i < 5; i++) data.ActionSlots.Add(new CharacterSlotConfig
                { SlotId = "canonical1112_slot_" + i, DisplayName = "부위 " + (i + 1),
                    LinkedPartId = "canonical1112_part_" + (i + 1), TargetingPolicy = AITargetingPolicy.RandomValid });
                AssetDatabase.CreateAsset(data, dir + "/Character.asset");
            }
            string prefabPath = dir + "/" + kind + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset("Assets/2. Data/TestEncounters/Prefabs/EliteEnemy_Test.prefab", prefabPath))
                    throw new InvalidOperationException("Cannot copy elite visual prefab.");
                var go = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    go.name = data.CharacterName;
                    var enemy = go.GetComponent<EliteEnemy>();
                    enemy.CanonicalStageOneKind = kind;
                    enemy.ConfigureAuthoringCore(data, loadout);
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
        }
        string temporaryDir = Root + "/Temporary";
        Directory.CreateDirectory(temporaryDir); AssetDatabase.Refresh();
        foreach (string key in new[] { "Cooling", "Confession" })
        {
            string path = temporaryDir + "/" + key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<SkillDefinition>(path) != null) continue;
            var d = ScriptableObject.CreateInstance<SkillDefinition>();
            d.name = key; d.SkillName = key == "Cooling" ? "냉각" : "고해";
            d.ActionType = ActionType.Preparation; d.PreparationTiming = PreparationActivationTiming.Immediate;
            d.OverrideEnergyCost = true; d.EnergyCost = key == "Cooling" ? 1 : 0;
            d.OverrideCanClash = true; d.CanClashValue = false; d.GainPrestige = false;
            d.Description = key == "Cooling" ? "보스 부위 하나의 오버클록 해제. 충전은 유지. 전투 한정 임시 카드." :
                "자신의 고른 부위 의심을 모두 지우고 봉인된 스킬을 복구. 낙인은 유지.";
            var serialized = new SerializedObject(d);
            serialized.FindProperty("skillId").stringValue = "temporary1112." + key.ToLowerInvariant();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(d, path);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        return "1112 stage-one assets installed; new skills=" + count;
    }
}
#endif
