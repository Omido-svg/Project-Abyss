#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class R6IbRealExchangeInsertionVerification20261030
{
    private sealed class VirtualSkill : NormalSkill
    {
        private readonly int rolls;
        public VirtualSkill(string name,Character owner,BattleEvent events,int basePower,int rolls)
        {
            this.rolls=rolls;
            SkillName=name;
            BasePower=basePower;
            Resolver=new DiceResolver(1,1);
            Initialize(owner,events);
        }
        public override int ExchangeRollCount => rolls;
        public override void Execute(BattleAction action){}
    }
    private sealed class Playback : IBattleActionPresentation, IBattleActionStepPresentation
    {
        public readonly List<long> Order=new List<long>();
        public readonly List<string> Timeline=new List<string>();
        public IEnumerator PlayClash(ClashResultContext result)
        {
            if(result?.FirstAction!=null)
            {
                Order.Add(result.FirstAction.ActionId);
                Timeline.Add("clash-"+result.FirstAction.ActionId);
            }
            yield break;
        }
        public IEnumerator PlayCommittedExchange(
            ClashResultContext root,ClashExchangeResult exchange)
        {
            if(root?.FirstAction!=null && exchange!=null)
            {
                Order.Add(root.FirstAction.ActionId);
                Timeline.Add("exchange-"+exchange.ExchangeIndex);
            }
            yield break;
        }
        public IEnumerator PlayStandalone(BattleAction a){yield break;}
    }
    private static void Drive(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();stack.Push(root);
        int steps=0;
        while(stack.Count>0)
        {
            if(++steps>10000)throw new InvalidOperationException("Coroutine guard.");
            var current=stack.Peek();
            if(!current.MoveNext()){stack.Pop();continue;}
            if(current.Current is IEnumerator nested)stack.Push(nested);
        }
    }
    private static ActionSlot Make(Character owner,Character target,long actionId,
        string skillName,int power,int rolls,BattleContext ctx)
    {
        var part=target.GetTargetPoints(false).FirstOrDefault(t=>t.IsValid).Part;
        var source=owner.GetTargetPoints(false).FirstOrDefault(t=>t.IsValid).Part;
        return new ActionSlot
        {
            ActionId=actionId,Owner=owner,TargetCharacter=target,
            TargetPart=part,Part=source,Phase=ActionPhase.COMBAT,
            SlotId="R6Ib-TEST-"+actionId,
            Skill=new VirtualSkill(skillName,owner,ctx._battleEvent,power,rolls)
        };
    }

    [MenuItem("Game System Verification/R6-Ib - Actual Mid-Exchange Insertion (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode only");
        var context=UnityEngine.Object.FindFirstObjectByType<BattleManager>()?.BattleContext;
        var player=context?.Player;
        var enemy=context?.Enemies?.FirstOrDefault(c=>c!=null&&!c.IsDead&&!c.UsesBodyParts);
        if(player==null||enemy==null||context.Services?.ClashManager==null)
            throw new InvalidOperationException("Battle Test Scene player/normal enemy missing");
        int pass=0,fail=0;
        void Check(string id,bool yes,string detail="")
        {
            if(yes){pass++;Debug.Log("[R6-Ib-Live] PASS "+id+" "+detail);}
            else{fail++;Debug.LogError("[R6-Ib-Live] FAIL "+id+" "+detail);}
        }

        var scheduler=new BattleReactionScheduler();
        var registry=new R6ReactionActionRegistry();
        var playback=new Playback();
        var resolver=new ActionResolver(context,context.Services.ClashManager,null,playback);
        resolver.EnableR6ReactionExecution(scheduler,registry);
        resolver.EnableR6StepwiseClashes();

        const long originalId=986020;
        const long opposingId=986021;
        const long reactionId=986022;
        var first=Make(player,enemy,originalId,"R6Ib-ROOT",7,3,context);
        var second=Make(enemy,player,opposingId,"R6Ib-OPPONENT",1,3,context);
        var reaction=Make(enemy,player,reactionId,"R6Ib-REACT",2,1,context);
        var request=new ReactionRequest(98501,originalId,originalId,
            98502,originalId,R4LiveSlotQuery.ActorRuntimeId(enemy),
            "R6Ib-REACT",ReactionTimingCheckpoint.AfterExchangeSettled,0,98503);
        var domainTrace=new List<string>();
        var enemyHpBefore=enemy.CurrentHP;
        var playerHpBefore=player.CurrentHP;
        int numberOfSubscriptions=0;
        bool registered=false;
        bool scheduled=false;
        bool checkpointCondition=false;
        bool reactionExecutedBeforeThird=false;
        resolver.R6ExchangeSettled += exchange =>
        {
            if(exchange.ExchangeIndex==2)
            {
                reactionExecutedBeforeThird=
                    resolver.ReactionExecutionTrace.Count==1 &&
                    resolver.ReactionExecutionTrace[0].Executed &&
                    resolver.ReactionExecutionTrace[0].ExecutedActionId==reactionId;
                if(reactionExecutedBeforeThird)
                    domainTrace.Add("reaction-executed");
            }
            domainTrace.Add("main-"+exchange.ExchangeIndex);
            numberOfSubscriptions++;
            if(exchange.ExchangeIndex==1 && !registered)
            {
                registered=registry.TryRegister(request,reaction,out var problem);
                scheduled=registered && scheduler.TrySchedule(request);
                checkpointCondition=scheduler.PendingCount==1;
                domainTrace.Add("reaction-requested");
            }
        };

        var queue=new ActionExecutionQueue();
        queue.ClashQueue.Enqueue(new ClashPair(first,second));
        Drive(resolver.Resolve(queue));
        foreach(var item in resolver.ReactionExecutionTrace)
            if(item.Executed)domainTrace.Add("executed-"+item.ExecutedActionId);
        Check("L01 exactly 3 original paired exchanges committed",
            numberOfSubscriptions==3 &&
            domainTrace.Count(x=>x.StartsWith("main-"))==3);
        Check("L02 second exchange registered a real independent reaction",
            registered && scheduled && checkpointCondition);
        Check("L03 one-shotted after-exchange reaction actually executed",
            resolver.ReactionExecutionTrace.Count==1 &&
            resolver.ReactionExecutionTrace[0].Executed &&
            resolver.ReactionExecutionTrace[0].ExecutedActionId==reactionId);
        Check("L04 completed exchange IDs remain in index order",
            domainTrace.Where(x=>x.StartsWith("main-")).SequenceEqual(
                new[]{"main-0","main-1","main-2"}));
        Check("L05 no queued reaction or open binding after resolve",
            scheduler.PendingCount==0 && registry.PendingBindings==0);
        Check("L06 main and reaction both reach real ClashManager result",
            playback.Order.Contains(originalId) &&
            playback.Order.Contains(reactionId));
        Check("L07 damage committed only by existing services",
            enemy.CurrentHP<=enemyHpBefore && player.CurrentHP<=playerHpBefore,
            "player="+playerHpBefore+"->"+player.CurrentHP+
            " enemy="+enemyHpBefore+"->"+enemy.CurrentHP);
        Check("L08 original ActionId distinct from reaction and opponent",
            originalId!=reactionId && reactionId!=opposingId);
        Check("L09 duplicate request replay rejected after dispatch",
            !scheduler.TrySchedule(request));
        Check("L10 independent reaction does not become extra paired exchange",
            numberOfSubscriptions==3 && queue.ClashQueue.Count==0);
        Check("L11 runtime factory remains on legacy stepwise-disabled mode",
            context.Services.ActionResolver!=resolver);
        Check("L12 original result playback recorded after domain resolution",
            playback.Order.LastOrDefault()==originalId);
        Check("L13 actual reaction occurred BEFORE original third exchange",
            reactionExecutedBeforeThird);
        Check("L14 committed sequence is original0/original1/reaction/original2",
            domainTrace.Take(5).SequenceEqual(
                new[]{"main-0","main-1","reaction-requested",
                    "reaction-executed","main-2"}));
        // Independent safety scenario in the SAME runtime scene: a very
        // strong reaction after exchange #2 kills the original defender.
        // Exchange #3 must never be rolled or damage a dead target.
        if(!enemy.IsDead)
        {
            var killQueue=new BattleReactionScheduler();
            var killRegistry=new R6ReactionActionRegistry();
            var killPlayback=new Playback();
            var killResolver=new ActionResolver(
                context,context.Services.ClashManager,null,killPlayback);
            killResolver.EnableR6ReactionExecution(killQueue,killRegistry);
            killResolver.EnableR6StepwiseClashes();
            const long killRootId=986030;
            const long killOtherId=986031;
            const long killReactionId=986032;
            var killA=Make(player,enemy,killRootId,"R6Ib-KILL-ROOT",4,3,context);
            var killB=Make(enemy,player,killOtherId,"R6Ib-KILL-OTHER",1,3,context);
            var killer=Make(player,enemy,killReactionId,"R6Ib-KILL-REACT",999,1,context);
            var req=new ReactionRequest(98511,killRootId,killRootId,
                98512,killRootId,R4LiveSlotQuery.ActorRuntimeId(player),
                "R6Ib-KILL-REACT",
                ReactionTimingCheckpoint.AfterExchangeSettled,0,98513);
            int committedOriginal=0;
            bool scheduledKill=false;
            killResolver.R6ExchangeSettled += exchange =>
            {
                committedOriginal++;
                if(exchange.ExchangeIndex==1)
                {
                    scheduledKill=killRegistry.TryRegister(req,killer,out var _) &&
                        killQueue.TrySchedule(req);
                }
            };
            var killActions=new ActionExecutionQueue();
            killActions.ClashQueue.Enqueue(new ClashPair(killA,killB));
            Drive(killResolver.Resolve(killActions));
            Check("L15 fatal reaction was scheduled exactly after second exchange",
                scheduledKill && killResolver.ReactionExecutionTrace.Count==1 &&
                killResolver.ReactionExecutionTrace[0].Executed);
            Check("L16 fatal reaction stops original third exchange",
                committedOriginal==2 && enemy.IsDead,
                "original_exchanges="+committedOriginal+
                " enemyHp="+enemy.CurrentHP);
            Check("L17 reaction does not replay any already committed exchange",
                killQueue.PendingCount==0 && killRegistry.PendingBindings==0 &&
                killPlayback.Order.Contains(killReactionId) &&
                killPlayback.Order.Contains(killRootId));
        }

        Debug.Log("[R6-Ib-Live] RESULT PASS="+pass+" FAIL="+fail);
        if(fail>0)throw new InvalidOperationException("R6-Ib live check failed="+fail);
    }
}
#endif
