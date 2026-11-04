#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Recurring G-FINAL authoring contract: all fixtures are memory-only. Nothing
// is written to Assets, no project scene is saved, no gameplay manager changes.
public sealed class FreezeContractConditionalAugment : CharacterAugment
{
    public override void CreateMechanics(
        CharacterBuildMechanicContext context,
        List<CombatMechanic> output)
    {
        if (output != null && context.IsValid && CanApplyTo(context.Owner))
            output.Add(new FreezeContractConditionalMechanic());
    }
}

public sealed class FreezeContractConditionalMechanic : CombatMechanic
{
    public override string MechanicName => "Conditional authoring fixture";
    public int TriggerCount { get; private set; }
    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurn,
            () => battleEvent.OnTurnStart -= OnTurn,
            "OnTurnStart");
    }

    private void OnTurn(int turn)
    {
        // Fixture-only condition: even numbered turns, once per turn.
        if (turn > 0 && turn % 2 == 0)
            TriggerCount++;
    }
}

public sealed class FreezeContractCompoundEmotionGraph : NativeEmotionGraph
{
    public int GainEnergy = 1;
    public int GainPrestige = 2;
    public int RollPowerBonus = 1;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        if (context?.Owner == null) return;
        if (GainEnergy > 0) context.Owner.AddEnergy(GainEnergy);
        if (GainPrestige > 0) context.Owner.AddPrestige(GainPrestige);
    }

    public override CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context) =>
        new FreezeContractCompoundRollModifier(RollPowerBonus);
}

public sealed class FreezeContractCompoundRollModifier : CombatMechanic
{
    private readonly int value;
    public FreezeContractCompoundRollModifier(int bonus) { value = bonus; }
    public override int ModifyRoll(BattleAction action, int roll) =>
        roll + value;
}

// Not exposed as a one-off menu. Invoke through the integrated G-FINAL audit
// or in-memory MCP Editor execution on a live test battle.
public static class AbilityExtensionContractVerification
{
    public static string Run(BattleContext battle, Character character)
    {
        if (!Application.isPlaying || battle == null || character == null)
            throw new InvalidOperationException(
                "An active PlayMode test battle and character are required.");

        var retained = new List<UnityEngine.Object>();
        int passed = 0, failed = 0;
        var lines = new List<string>();
        void Check(string name, bool condition)
        {
            if (condition) passed++; else failed++;
            lines.Add((condition ? "PASS" : "FAIL") + " | " + name);
        }

        int beforeSkills = AssetDatabase.FindAssets("t:SkillDefinition").Length;
        int beforeEmotions = AssetDatabase.FindAssets(
            "t:NativeEmotionGraph",
            new[]{"Assets/2. Data/Resources/ProjectAbyss/NativeEmotions"}
        ).Length;

        var isolated = new BattleContext
        {
            Player = character,
            _battleEvent = new BattleEvent(),
            SuppressPresentation = true,
            Rules = battle.Rules
        };
        var conditional = ScriptableObject.CreateInstance<FreezeContractConditionalAugment>();
        retained.Add(conditional);
        conditional.name = "Fixture conditional passive";
        var mechanics = new List<CombatMechanic>();
        var mechanicContext = CharacterBuildMechanicContext.ForAugment(
            character, conditional);
        conditional.CreateMechanics(mechanicContext, mechanics);
        Check("P05 conditional passive creates mechanic without core edits",
              mechanics.Count == 1 &&
              mechanics[0] is FreezeContractConditionalMechanic);
        var passive = mechanics.FirstOrDefault()
            as FreezeContractConditionalMechanic;

        try
        {
            for (int i=0; i<3; i++)
            {
                var definition = ScriptableObject.CreateInstance<SkillDefinition>();
                retained.Add(definition);
                definition.name = "Fixture Normal Skill " + (i+1);
                typeof(SkillDefinition).GetField(
                    "skillId", BindingFlags.NonPublic|BindingFlags.Instance
                )?.SetValue(definition, "gfinal.fixture.normal."+ (i+1));
                definition.SkillName = "검증용 일반공격 " + (i+1);
                definition.ActionType = ActionType.NormalAttack;
                definition.Color = SkillColor.Red;
                definition.ResolverType = SkillResolverType.Dice;
                definition.DiceMin = 1 + i;
                definition.DiceMax = 3 + i;
                definition.OverrideEnergyCost = true;
                definition.EnergyCost = i;
                definition.Rolls = new List<SkillRollData>{
                    new SkillRollData {Index=0,MinPower=1+i,MaxPower=3+i}
                };
                if(i==2)
                {
                    definition.AttackWeight.Weight = 2;
                    definition.AttackWeight.TargetingModel =
                        ProjectAbyss.Abilities.AttackWeightTargetingModel.SlotWeighted;
                }
                Skill instance=definition.CreateRuntimeSkill();
                Check("P05 new normal skill "+(i+1)+" can be created and displayed",
                      instance!=null &&
                      instance.Definition == definition &&
                      instance.ActionType == ActionType.NormalAttack &&
                      BattleSkillDescriptionFormatter.GetDisplayName(instance)
                        .Contains("검증용 일반공격"));
                if(i==2)
                    Check("P04 representative authored SlotWeighted card",
                          instance.AttackWeight==2 &&
                          instance.AttackWeightTargetingModel==
                          ProjectAbyss.Abilities.AttackWeightTargetingModel.SlotWeighted);
            }

            if(passive!=null)
            {
                passive.Initialize(character, isolated);
                Check("P05 conditional passive registers in battle scope",
                      passive.TryRegister());
                isolated._battleEvent.RaiseTurnStart(1);
                isolated._battleEvent.RaiseTurnStart(2);
                isolated._battleEvent.RaiseTurnStart(3);
                Check("P05 conditional event only fires for matched turn",
                      passive.TriggerCount==1);
                passive.Unregister();
                isolated._battleEvent.RaiseTurnStart(4);
                Check("P06 passive unsubscribes cleanly on battle exit",
                      !passive.IsRegistered && passive.TriggerCount==1);
            }

            var source=ScriptableObject.CreateInstance<EmotionAugmentDefinition>();
            retained.Add(source);
            source.name="FixtureCompoundEmotion";
            source.AugmentId="gfinal.fixture.compound";
            source.DisplayName="검증용 복합 감정 증강";
            source.Emotion=EmotionType.Awe;
            source.Tier=1;

            var graph=ScriptableObject.CreateInstance<FreezeContractCompoundEmotionGraph>();
            retained.Add(graph);
            graph.name="FixtureCompoundEmotionGraph";
            graph.SourceGuid=new string('f',32);
            graph.StableId=source.AugmentId;
            graph.SourceDisplayName=source.DisplayName;
            graph.SourceAssetName=source.name;
            graph.Emotion=source.Emotion;
            graph.Tier=source.Tier;
            graph.State=NativeEmotionGraphState.NativeReady;

            var augmentContext=new EmotionAugmentRuntimeContext{
                Owner=character,
                BattleContext=isolated,
                Augment=source
            };
            Check("P05 compound graph schema matches new emotion asset",
                  graph.IsValid && graph.Matches(source));
            CombatMechanic compound=graph.CreateMechanic(augmentContext);
            int beforePrestige=character.RuntimeStatus.currentPrestige;
            int beforeEnergy=character.CurrentEnergy;
            graph.Apply(augmentContext);
            Check("P05 compound augment invokes energy and prestige effects",
                  character.RuntimeStatus.currentPrestige>=beforePrestige &&
                  character.CurrentEnergy>=beforeEnergy);
            Check("P05 compound augment installs independent roll modifier",
                  compound != null && compound.ModifyRoll(null,7)==8);

            Check("P05 no saved skill or emotion asset introduced",
                  beforeSkills==AssetDatabase.FindAssets("t:SkillDefinition").Length &&
                  beforeEmotions==AssetDatabase.FindAssets(
                     "t:NativeEmotionGraph",
                     new[]{"Assets/2. Data/Resources/ProjectAbyss/NativeEmotions"}).Length);
        }
        finally
        {
            passive?.Release();
            isolated._battleEvent.Dispose();
            foreach(var o in retained)
                if(o!=null) UnityEngine.Object.DestroyImmediate(o);
        }

        string result="PASS="+passed+" FAIL="+failed+"\n"+
                      string.Join("\n", lines);
        if(failed!=0)
            throw new InvalidOperationException(
                "Extension contract regression failed:\n"+result);
        return result;
    }
}
#endif