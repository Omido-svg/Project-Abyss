using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    [Serializable]
    public sealed class AbilityRunStateEntry
    {
        public string OwnerId,Key;
        public long Value;
    }
    [Serializable]
    public sealed class AbilityRunStateSnapshot
    {
        public int SchemaVersion=1;
        public List<AbilityRunStateEntry> Entries=new();
    }
}
