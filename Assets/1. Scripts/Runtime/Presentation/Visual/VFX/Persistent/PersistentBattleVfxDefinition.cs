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

    [Header("Pooling")]
    public bool UsePooling = true;
    [Min(0)] public int MaxPoolSize = 4;

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
    [Min(0f)] public float DestroyDelay = 0.5f;

    private void OnValidate()
    {
        MaxPoolSize = Mathf.Max(0, MaxPoolSize);
        DestroyDelay = Mathf.Max(0f, DestroyDelay);

        if (LocalScale == Vector3.zero)
            LocalScale = Vector3.one;
    }
}
