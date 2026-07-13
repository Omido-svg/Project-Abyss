using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/VFX/Persistent VFX Definition",
    fileName = "NewPersistentBattleVfxDefinition")]
public class PersistentBattleVfxDefinition : ScriptableObject
{
    [Header("Identity")]
    public string Key = "Aura";

    [Header("Prefab")]
    public GameObject Prefab;

    [Header("Anchor")]
    public CharacterPersistentVfxAnchorType AnchorType =
        CharacterPersistentVfxAnchorType.LookAtPoint;

    public PartType BodyPartType = PartType.HEAD;

    [Header("Transform")]
    public Vector3 LocalPositionOffset = Vector3.zero;
    public Vector3 LocalEulerOffset = Vector3.zero;
    public Vector3 LocalScale = Vector3.one;

    [Header("Lifetime")]
    public bool ParentToAnchor = true;
    public bool PlayOnSpawn = true;
    public bool StopParticleSystemsOnRemove = true;
    public float DestroyDelay = 0.5f;
}