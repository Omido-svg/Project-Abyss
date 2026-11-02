using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    // R2-K: Keyword IDs are stable content identities, not display strings,
    // skill tags, N/T status application IDs or actor runtime IDs.
    public enum R2KeywordStorageKind
    {
        NumericTimedStatus,
        PartStackLedger,
        CombatResource,
        CharacterMechanic,
        AbilityMemory
    }
    public enum R2KeywordCarrier { Actor, Part, Slot }

    public sealed class R2KeywordDefinition
    {
        public string KeywordId { get; }
        public string OwnerCharacterContentId { get; }
        public string DisplayName { get; }
        public string Tooltip { get; }
        public string IconRef { get; }
        public string BackingDefinitionRef { get; }
        public R2KeywordStorageKind StorageKind { get; }
        public R2KeywordCarrier Carrier { get; }
        public int Version { get; }
        public IReadOnlyList<string> GrantedAbilityRefs { get; }
        public IReadOnlyList<string> MechanicPolicyRefs { get; }

        public R2KeywordDefinition(string keywordId, string contentId,
            string displayName, R2KeywordStorageKind storage,
            R2KeywordCarrier carrier, string backingDefinitionRef = null,
            IEnumerable<string> grantedAbilities = null,
            IEnumerable<string> mechanicPolicies = null,
            string iconRef = null, string tooltip = null, int version = 1)
        {
            if (string.IsNullOrWhiteSpace(keywordId) ||
                string.IsNullOrWhiteSpace(contentId) || version < 1)
                throw new ArgumentException("Stable keyword and owner content IDs and version are required.");
            KeywordId = keywordId;
            OwnerCharacterContentId = contentId;
            DisplayName = displayName ?? string.Empty;
            StorageKind = storage;
            Carrier = carrier;
            BackingDefinitionRef = backingDefinitionRef ?? string.Empty;
            GrantedAbilityRefs = Array.AsReadOnly(
                new List<string>(grantedAbilities ?? Array.Empty<string>()).ToArray());
            MechanicPolicyRefs = Array.AsReadOnly(
                new List<string>(mechanicPolicies ?? Array.Empty<string>()).ToArray());
            IconRef = iconRef ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Version = version;
        }
    }

    /// <summary>
    /// Static per-character catalog only. No mutable HP/status/stack values
    /// may live here. Actual state stays in its existing native storage.
    /// </summary>
    public sealed class R2CharacterFeatureSet
    {
        private readonly Dictionary<string, R2KeywordDefinition> keywords =
            new Dictionary<string, R2KeywordDefinition>(StringComparer.Ordinal);
        public string CharacterContentId { get; }
        public IReadOnlyDictionary<string, R2KeywordDefinition> Keywords =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, R2KeywordDefinition>(keywords);
        public IReadOnlyList<string> ResourceRefs { get; }
        public IReadOnlyList<string> PassiveRefs { get; }
        public IReadOnlyList<string> RngPolicyRefs { get; }
        public IReadOnlyList<string> WeaponPolicyRefs { get; }
        public IReadOnlyList<string> ActionExtensionRefs { get; }

        public R2CharacterFeatureSet(string contentId,
            IEnumerable<R2KeywordDefinition> definitions,
            IEnumerable<string> resourceRefs = null,
            IEnumerable<string> passiveRefs = null,
            IEnumerable<string> rngPolicyRefs = null,
            IEnumerable<string> weaponPolicyRefs = null,
            IEnumerable<string> actionExtensionRefs = null)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentException("Character content ID is required.", nameof(contentId));
            CharacterContentId = contentId;
            foreach (var def in definitions ?? Array.Empty<R2KeywordDefinition>())
            {
                if (def == null || def.OwnerCharacterContentId != contentId)
                    throw new ArgumentException("Keyword ownership must match the feature set.");
                if (keywords.ContainsKey(def.KeywordId))
                    throw new ArgumentException("Duplicate keyword definition ID: " + def.KeywordId);
                keywords.Add(def.KeywordId, def);
            }
            ResourceRefs = Freeze(resourceRefs);
            PassiveRefs = Freeze(passiveRefs);
            RngPolicyRefs = Freeze(rngPolicyRefs);
            WeaponPolicyRefs = Freeze(weaponPolicyRefs);
            ActionExtensionRefs = Freeze(actionExtensionRefs);
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values) =>
            Array.AsReadOnly(new List<string>(values ?? Array.Empty<string>()).ToArray());

        public bool TryGet(string keywordId, out R2KeywordDefinition result)
        {
            result = null;
            return keywordId != null && keywords.TryGetValue(keywordId, out result);
        }
    }

    /// <summary>
    /// A keyword query must name both the OWNER that grants the keyword and
    /// the TARGET/part that currently carries it. Read-only evidence is
    /// delegated to the existing status/resource/mechanic source of truth.
    /// </summary>
    public readonly struct R2KeywordReadRequest
    {
        public readonly long GrantingActorRuntimeId;
        public readonly long CarrierActorRuntimeId;
        public readonly string CarrierPartId;
        public readonly string CarrierSlotId;
        public readonly string KeywordId;
        public R2KeywordReadRequest(long grantingActorId, long carrierActorId,
            string keywordId, string partId = null, string slotId = null)
        {
            GrantingActorRuntimeId = grantingActorId;
            CarrierActorRuntimeId = carrierActorId;
            KeywordId = keywordId ?? string.Empty;
            CarrierPartId = partId ?? string.Empty;
            CarrierSlotId = slotId ?? string.Empty;
        }
    }

    public interface IR2KeywordSnapshotSource
    {
        bool TryRead(in R2KeywordReadRequest request, out int value);
    }

    public interface IR2KeywordQuery
    {
        bool TryRead(in R2KeywordReadRequest request, out int value);
    }

    /// <summary>
    /// One battle-local registration per actor runtime ID. It never stores
    /// keyword stacks or statuses. A cast by Olaf on an enemy is valid:
    /// ownership checks the granting ACTOR, not the afflicted carrier.
    /// </summary>
    public sealed class R2CharacterFeatureRegistry : IR2KeywordQuery
    {
        private readonly Dictionary<long, R2CharacterFeatureSet> bindings =
            new Dictionary<long, R2CharacterFeatureSet>();
        private readonly IR2KeywordSnapshotSource source;
        private readonly Func<long, string> resolveActualContentId;
        public int BoundActorCount => bindings.Count;

        // The caller provides the authoritative CharacterData -> ContentId lookup.
        // Merely naming a feature set cannot impersonate a character's ownership.
        public R2CharacterFeatureRegistry(IR2KeywordSnapshotSource source,
            Func<long, string> actualCharacterContentIdResolver)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            resolveActualContentId = actualCharacterContentIdResolver ??
                throw new ArgumentNullException(nameof(actualCharacterContentIdResolver));
        }

        public bool TryBind(long actorId, R2CharacterFeatureSet features)
        {
            if (actorId <= 0 || features == null || bindings.ContainsKey(actorId))
                return false;
            string actualContentId = resolveActualContentId(actorId);
            if (!string.Equals(actualContentId, features.CharacterContentId,
                    StringComparison.Ordinal))
                return false;
            bindings.Add(actorId, features);
            return true;
        }

        public bool TryValidateGrant(long actorId, string keywordId,
            out R2KeywordDefinition definition)
        {
            definition = null;
            return bindings.TryGetValue(actorId, out var features) &&
                   features.TryGet(keywordId, out definition);
        }

        public bool TryRead(in R2KeywordReadRequest request, out int value)
        {
            value = 0;
            if (request.GrantingActorRuntimeId <= 0 ||
                request.CarrierActorRuntimeId <= 0 ||
                !TryValidateGrant(request.GrantingActorRuntimeId,
                    request.KeywordId, out var definition))
                return false;
            // Part and slot are explicit, not silently merged with actor data.
            if (definition.Carrier == R2KeywordCarrier.Part &&
                string.IsNullOrEmpty(request.CarrierPartId))
                return false;
            if (definition.Carrier == R2KeywordCarrier.Slot &&
                string.IsNullOrEmpty(request.CarrierSlotId))
                return false;
            return source.TryRead(in request, out value);
        }

        public bool Unbind(long actorId) => bindings.Remove(actorId);
        public void ClearForBattleEnd() => bindings.Clear();
    }
}
