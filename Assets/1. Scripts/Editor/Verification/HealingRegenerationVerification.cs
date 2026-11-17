#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>1117 §1.7 / §11.2: actual healing, status ownership, tick lifetime and card dispatch.</summary>
public static class HealingRegenerationVerification
{
    public static string LastReport { get; private set; }

    private static SkillDefinition NativeSource(string id)
    {
        var graph=Resources.LoadAll<OlafNativeAbilityDefinition>("").First(d=>d.StableId==id);
        return AssetDatabase.FindAssets(graph.SourceAssetName+" t:SkillDefinition")
            .Select(guid=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(d=>d!=null&&d.SkillId==id&&OlafNativeAbilityRuntimeBinding.IsApprovedSource(d));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleContext Battle;
        public readonly Yujin Actor;
        public readonly List<UnityEngine.Object> Objects = new();
        private readonly UnityEngine.SceneManagement.Scene scene;
        private readonly UnityEngine.Random.State random;
        private long nextId = 810000;
        public BodyPart Left => Actor.GetBodyPart(PartType.LEFT_HAND);
        public BodyPart Right => Actor.GetBodyPart(PartType.RIGHT_HAND);

        public Fixture()
        {
            random=UnityEngine.Random.state;
            scene=EditorSceneManager.NewPreviewScene();
            Battle=new BattleContext { SuppressPresentation=true, RunProgression=new RunProgressionState() };
            Battle.Services=new BattleRuntimeServices();Battle.EffectResolver=new BattleEffectResolver(Battle);
            Battle.Services.OlafAbilities=new OlafNativeAbilityRuntimeBinding();
            Battle.Services.MomentumManager=new MomentumManager(Battle);
            Battle.Services.ActionManager=new ActionManager();
            Battle.Services.DamageManager=new DamageManager(Battle,Battle.Services.MomentumManager);
            Actor=Spawn<Yujin>("Yujin_Test");Battle.Player=Actor;
            Parts(Actor);Actor.RuntimeStatus.currentHP=100;
        }

        public T Spawn<T>(string name) where T : Character
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/"+name+".prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);Objects.Add(go);
            var actor=go.GetComponent<T>();var data=UnityEngine.Object.Instantiate(actor.Data);Objects.Add(data);
            actor.ConfigureAuthoringCore(data,actor.Data.CombatLoadout);actor.Initialize(Battle);
            if(Battle.Player!=null)Battle.Enemies.Add(actor);
            return actor;
        }

        public void Parts(Character actor)
        {
            foreach(var part in actor.BodyParts)part.SetDebugState(80,100,false,false);
            actor.GetBodyPart(PartType.LEFT_HAND).SetDebugState(50,100,false,false);
            actor.GetBodyPart(PartType.RIGHT_HAND).SetDebugState(10,100,false,false);
        }

        public BattleAction Action(Character actor,SkillDefinition definition=null)
        {
            if(definition==null)
            {
                definition=ScriptableObject.CreateInstance<SkillDefinition>();Objects.Add(definition);
                definition.name="HealingFixture";definition.ActionType=ActionType.NormalAttack;
                var serialized=new SerializedObject(definition);serialized.FindProperty("skillId").stringValue="verify.healing."+nextId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var skill=actor.CreateRuntimeSkillForLoadout(definition);skill.Initialize(actor,Battle._battleEvent);
            return new BattleAction { Slot=new ActionSlot { Owner=actor,Part=actor.GetBodyPart(PartType.LEFT_HAND),
                Skill=skill,ActionId=++nextId,TargetCharacter=actor,TargetPart=actor.GetBodyPart(PartType.LEFT_HAND) } };
        }

        public void Dispose()
        {
            Battle.DisposeAbilities();
            Battle.Services.OlafAbilities.Dispose();
            foreach(var item in Objects.AsEnumerable().Reverse())if(item!=null)UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;
        }
    }

    [MenuItem("Tools/Project Abyss/Verification/Healing and Regeneration")]
    public static void Run()
    {
        int passed=0,failed=0;var lines=new List<string>();
        void Check(string id,bool ok){lines.Add((ok?"PASS ":"FAIL ")+id);if(ok)passed++;else failed++;}
        void Case(string id,Action<Fixture> run)
        {try{using var fixture=new Fixture();run(fixture);}catch(Exception e){Check(id+": "+e.GetBaseException().Message,false);}}

        Case("preferred healing",f=>
        {
            f.Actor.RestoreCurrentHP(7,f.Left);
            Check("card source part wins over a lower-HP part",f.Left.PartHP==57&&f.Right.PartHP==10);
            Check("whole HP receives the same amount without splitting",f.Actor.CurrentHP==107);
            f.Left.SetDebugState(100,100,false,false);f.Actor.RestoreCurrentHP(7,f.Left);
            Check("full source part does not spill into another part",f.Left.PartHP==100&&f.Right.PartHP==10&&f.Actor.CurrentHP==114);
        });
        Case("source-less and foreign healing",f=>
        {
            f.Actor.RestoreCurrentHP(5);
            Check("source-less healing selects current lowest living part",f.Right.PartHP==15&&f.Left.PartHP==50);
            var other=f.Spawn<Yujin>("Yujin_Test");var foreign=other.GetBodyPart(PartType.LEFT_HAND);float before=foreign.PartHP;
            f.Actor.RestoreCurrentHP(4,foreign);
            Check("foreign source part falls back inside the recipient",f.Right.PartHP==19&&foreign.PartHP==before);
            f.Left.SetDebugState(0,100,false,true);f.Actor.RestoreCurrentHP(3,f.Left);
            Check("destroyed source falls back and is never revived",f.Left.IsBroken&&f.Left.PartHP==0&&f.Right.PartHP==22);
        });
        Case("ties and state",f=>
        {
            foreach(var part in f.Actor.BodyParts)part.SetDebugState(20,100,false,false);
            var expected=f.Actor.BodyParts.First(p=>p.Type!=PartType.HEAD);
            Check("equal HP uses index order with head last",f.Actor.ResolveHealingPart()==expected);
            f.Left.SetDebugState(1,100,true,false);f.Actor.RestoreCurrentHP(9,f.Left);
            Check("healing raises weakened part HP without clearing its state",f.Left.IsWeakened&&f.Left.PartHP==10);
        });
        Case("single HP target",f=>
        {
            var normal=f.Spawn<NormalEnemy>("NormalEnemy_Test");normal.RuntimeStatus.currentHP=normal.MaxCombatHP-20;
            normal.RestoreCurrentHP(5,f.Left);
            Check("part-less target receives whole healing",normal.CurrentHP==normal.MaxCombatHP-15&&normal.ResolveHealingPart()==null);
            normal.AddStatus(new RegenerationStatus(1,4),normal);normal.TurnEnd();
            Check("part-less regeneration still works and expires",normal.CurrentHP==normal.MaxCombatHP-11&&!normal.HasStatus<RegenerationStatus>());
        });
        Case("status application boundary",f=>
        {
            var action=f.Action(f.Actor);var regeneration=new RegenerationStatus(2,4);
            bool applied=f.Battle.EffectResolver.ApplyCharacterStatus(EffectRequest.CharacterStatus(f.Actor,f.Actor,regeneration,action));
            Check("character-style card request stores regeneration on its source part",applied&&regeneration.OwnerPart==f.Left&&
                f.Left.StatusEffects.Contains(regeneration)&&!f.Actor.StatusEffects.Contains(regeneration));
            f.Right.SetDebugState(2,100,false,false);f.Actor.TurnEnd();
            Check("regeneration retains its part when a different part becomes lower",f.Left.PartHP==54&&f.Right.PartHP==2&&regeneration.Duration==1);
        });
        Case("source-less regeneration anchor",f=>
        {
            var regeneration=new RegenerationStatus(2,3);f.Actor.AddStatus(regeneration,f.Actor);
            Check("source-less regeneration fixes the lowest part on application",regeneration.OwnerPart==f.Right);
            f.Left.SetDebugState(2,100,false,false);f.Actor.TurnEnd();
            Check("anchor is not reselected every turn",f.Left.PartHP==2&&f.Right.PartHP==13);
        });
        Case("independent parts and durations",f=>
        {
            var first=new RegenerationStatus(2,4);var second=new RegenerationStatus(1,7);
            f.Actor.AddPartStatus(f.Left,first,f.Actor);f.Actor.AddPartStatus(f.Right,second,f.Actor);
            var stagger=f.Actor.GetMechanic<StaggerGaugeMechanic>();stagger.SacrificeGauge(30);int gauge=stagger.CurrentGauge;
            f.Actor.TurnEnd();
            Check("different parts receive their own sum",f.Left.PartHP==54&&f.Right.PartHP==17&&f.Actor.CurrentHP==111);
            Check("regeneration still recovers stagger",stagger.CurrentGauge==gauge+11);
            Check("duration-one regeneration applies before expiring",!f.Right.StatusEffects.Contains(second)&&first.Duration==1);
            f.Actor.TurnEnd();
            Check("remaining duration heals only its own part",f.Left.PartHP==58&&f.Right.PartHP==17&&f.Actor.CurrentHP==115&&!f.Left.StatusEffects.Contains(first));
        });
        Case("same-part sum and healing modifiers",f=>
        {
            f.Actor.AddStatus(new PainStatus(2),f.Actor);
            f.Actor.AddPartStatus(f.Left,new RegenerationStatus(1,3),f.Actor);
            f.Actor.AddPartStatus(f.Left,new RegenerationStatus(2,5),f.Actor);
            var stagger=f.Actor.GetMechanic<StaggerGaugeMechanic>();stagger.SacrificeGauge(20);int gauge=stagger.CurrentGauge;
            f.Actor.TurnEnd();
            Check("same-part entries sum before pain rounding",f.Left.PartHP==54&&f.Actor.CurrentHP==104);
            Check("pain still affects HP and stagger healing",stagger.CurrentGauge==gauge+4);
        });
        Case("broken regeneration survives",f=>
        {
            var regeneration=new RegenerationStatus(2,6);f.Actor.AddPartStatus(f.Left,regeneration,f.Actor);
            f.Actor.ForceBreakPart(f.Left);
            Check("real part destruction retains the regeneration entry",f.Left.IsBroken&&f.Left.StatusEffects.Contains(regeneration));
            f.Actor.TurnEnd();
            Check("broken anchor redirects healing and ticks its remaining duration",f.Right.PartHP==16&&f.Left.PartHP==0&&regeneration.Duration==1);
            f.Actor.TurnEnd();
            Check("broken anchor regeneration expires normally",f.Right.PartHP==22&&!f.Left.StatusEffects.Contains(regeneration));
        });
        Case("status cleanup and already-broken application",f=>
        {
            var regeneration=new RegenerationStatus(2,6);f.Actor.AddStatus(regeneration,f.Actor);
            f.Actor.RemoveStatus(regeneration);
            Check("original AddStatus callers can remove their part-owned regeneration",!f.Right.StatusEffects.Contains(regeneration));
            f.Left.SetDebugState(0,100,false,true);
            var redirected=new RegenerationStatus(2,4);
            bool applied=f.Battle.EffectResolver.ApplyBodyPartStatus(EffectRequest.BodyPartStatus(f.Actor,f.Actor,f.Left,redirected));
            Check("regeneration applied to a broken source selects a living part immediately",applied&&redirected.OwnerPart==f.Right);
            f.Actor.AddPartStatus(f.Right,new StrengthStatus(2,2),f.Actor);
            f.Actor.RemoveAllPartStatuses(f.Right,StatusEffectRemoveReason.PartBroken);
            Check("part-break cleanup retains only active regeneration",f.Right.StatusEffects.Contains(redirected)&&!f.Right.StatusEffects.OfType<StrengthStatus>().Any());
            f.Actor.RemoveStatus(redirected);f.Actor.TurnEnd();
            Check("explicit cleanup prevents future regeneration healing",f.Right.PartHP==10&&f.Actor.CurrentHP==100);
        });
        Case("turn-end survival",f=>
        {
            f.Actor.RuntimeStatus.currentHP=2;
            f.Actor.AddPartStatus(f.Left,new Bleeding(5),f.Actor);
            f.Actor.AddPartStatus(f.Left,new RegenerationStatus(1,10),f.Actor);
            f.Actor.TurnEnd();
            Check("DOT settles before regeneration and death is deferred",!f.Actor.IsDead&&f.Actor.CurrentHP==7);
            Check("last-turn regeneration heals its own part after DOT",f.Left.PartHP==55&&f.Right.PartHP==10);
        });
        Case("deferred regeneration",f=>
        {
            var action=f.Action(f.Actor);
            f.Battle.EffectResolver.ApplyCharacterStatus(EffectRequest.CharacterStatus(f.Actor,f.Actor,
                new DeferredStatusEffect(StatusEffectId.Regeneration,5,1),action));
            Check("reservation is part-owned but does not heal early",f.Left.StatusEffects.OfType<DeferredStatusEffect>().Any()&&f.Left.PartHP==50);
            f.Battle.TurnStartEffects.Drain();
            Check("materialization preserves the reserved source part",f.Left.StatusEffects.OfType<RegenerationStatus>().Any()&&!f.Actor.HasStatus<RegenerationStatus>());
            f.Actor.TurnEnd();
            Check("materialized regeneration heals on turn end",f.Left.PartHP==55&&f.Right.PartHP==10);
        });
        Case("composable rule dispatch",f=>
        {
            var action=f.Action(f.Actor);var definition=SkillRules.CreateDraft(action.Skill.Definition,"healing-fixture-guid");f.Objects.Add(definition);
            definition.Rules=new[]{SkillRules.OnUse("regeneration",AbilityEffects.AddStatus(StatusEffectId.Regeneration,4,1)).To(AbilityTargets.Self)};
            f.Battle.Abilities.Register(definition);f.Battle._battleEvent.RaiseActionStart(action);
            action.Skill.NotifyRollUsing(action,null,0,false,true);
            Check("RuleSet status operation carries source part through central resolver",f.Left.StatusEffects.OfType<RegenerationStatus>().Any());
            f.Actor.TurnEnd();Check("RuleSet regeneration uses source instead of lowest part",f.Left.PartHP==54&&f.Right.PartHP==10);
        });
        Case("native preparation healing",f=>
        {
            var olaf=f.Spawn<Olaf>("Olaf_Test");f.Parts(olaf);olaf.RuntimeStatus.currentHP=40;
            olaf.GetMechanic<OlafMadnessMechanic>().AddMadness(2);
            var source=NativeSource(OlafSkillIds.CalmMadness);
            var action=f.Action(olaf,source);action.Skill.Execute(action);
            Check("actual Olaf healing preparation heals the used slot",olaf.GetBodyPart(PartType.LEFT_HAND).PartHP==80&&olaf.GetBodyPart(PartType.RIGHT_HAND).PartHP==10&&olaf.CurrentHP==70);
        });
        Case("blood oath repeat restriction",f=>
        {
            var olaf=f.Spawn<Olaf>("Olaf_Test");f.Parts(olaf);olaf.RuntimeStatus.currentHP=40;
            var left=olaf.GetBodyPart(PartType.LEFT_HAND);
            olaf.AddPartStatus(left,new RegenerationStatus(1,1),olaf);
            var source=NativeSource(OlafSkillIds.BloodOathPreparation);
            var first=f.Action(olaf,source);first.Skill.Execute(first);
            Check("blood oath stores its regeneration beside other part regeneration",left.StatusEffects.OfType<RegenerationStatus>().Count()==2);
            olaf.TurnEnd();var second=f.Action(olaf,source);second.Skill.Execute(second);
            Check("blood oath cannot repeat while its own part regeneration remains",left.StatusEffects.OfType<RegenerationStatus>().Count()==1);
            olaf.TurnEnd();olaf.TurnEnd();var third=f.Action(olaf,source);third.Skill.Execute(third);
            Check("blood oath can repeat after its own regeneration expires",left.StatusEffects.OfType<RegenerationStatus>().Count()==1);
        });

        LastReport="PASS="+passed+" FAIL="+failed+"\n"+string.Join("\n",lines);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/HealingRegenerationVerification.txt",LastReport);
        if(failed>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }
}
#endif
