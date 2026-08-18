using System;
using UnityEngine;

[Serializable]
public sealed class SkillKeywordEntry
{
    public string Name;

    [TextArea(2, 6)]
    public string Description;
}