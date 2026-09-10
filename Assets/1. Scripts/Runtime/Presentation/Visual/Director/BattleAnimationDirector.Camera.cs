using UnityEngine;

public partial class BattleAnimationDirector : MonoBehaviour
{
    // Timeline camera impact/shake event routing moved to SkillTimelineEventRouter.
    // The Director remains the playback/session owner; this partial is retained to preserve
    // the existing Unity script asset/GUID boundary for future camera lifecycle helpers.
}
