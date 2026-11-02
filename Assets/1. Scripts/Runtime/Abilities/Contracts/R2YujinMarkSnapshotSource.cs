using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Read-only R2-K bridge to the EXISTING YujinMechanic per-part Mark
    /// dictionary. It owns no mark values and never writes Character state.
    /// The character feature registry performs owner/keyword authorization.
    /// </summary>
    public sealed class R2YujinMarkSnapshotSource : IR2KeywordSnapshotSource
    {
        public const string StableKeywordId = "yujin.mark";
        private readonly Dictionary<long,Yujin> sources = new Dictionary<long,Yujin>();
        private readonly Dictionary<long,Character> carriers = new Dictionary<long,Character>();

        public bool TryRegisterYujin(Yujin actor)
        {
            if(actor?.YujinMechanic == null)return false;
            long id=R4LiveSlotQuery.ActorRuntimeId(actor);
            if(id<=0 || sources.ContainsKey(id))return false;
            sources.Add(id,actor);
            return true;
        }

        public bool TryRegisterCarrier(Character actor)
        {
            if(actor == null)return false;
            long id=R4LiveSlotQuery.ActorRuntimeId(actor);
            if(id<=0 || carriers.ContainsKey(id))return false;
            carriers.Add(id,actor);
            return true;
        }

        public bool TryRead(in R2KeywordReadRequest request,out int value)
        {
            value=0;
            if(!string.Equals(request.KeywordId,StableKeywordId,StringComparison.Ordinal) ||
               !sources.TryGetValue(request.GrantingActorRuntimeId,out var source) ||
               !carriers.TryGetValue(request.CarrierActorRuntimeId,out var target) ||
               source==null || source.YujinMechanic==null || target==null)
                return false;
            BodyPart part=null;
            if(!string.IsNullOrEmpty(request.CarrierPartId))
            {
                if(target.BodyParts!=null)
                    foreach(var candidate in target.BodyParts)
                        if(candidate!=null &&
                           string.Equals(candidate.PartId,request.CarrierPartId,StringComparison.Ordinal))
                        {
                            part=candidate;break;
                        }
                if(part==null)return false;
            }
            value=source.YujinMechanic.GetMark(target,part);
            return true;
        }

        public void ClearForBattleEnd()
        {
            sources.Clear();
            carriers.Clear();
        }
    }
}
