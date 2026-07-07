using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Visual/Skill Visual Definition",
    fileName = "NewSkillVisualDefinition")]
public class SkillVisualDefinition : ScriptableObject
{
    [Header("Camera")]
    public bool UsesTargetCamera = true;
    public bool ReturnCameraAfterAction = true;
    public float CameraArriveTimeout = 1.5f;

    [Header("Movement")]
    public bool MovesToTarget = true;
    public bool ReturnPositionAfterAction = true;

    [Header("Facing")]
    public bool FaceEachOther = true;
    public bool ReturnFacingAfterAction = true;

    [Header("Clash")]
    public bool ShowsClashPower = false;
    public Color ClashColor = Color.yellow;
    public Color TieColor = new Color(1f, 0.45f, 0.1f);

    [Header("Hit / Damage")]
    public bool HasHitFrameDamage = true;
    public bool ApplyDamageIfNoHitFrame = false;

    [Header("Timing")]
    public float BeforeActionDelay = 0.15f;
    public float AfterActionDelay = 0.35f;

    [Header("Camera Shake")]
    public bool UseHitCameraShake = false;
    public BattleCameraShakeSettings HitShake = new BattleCameraShakeSettings();
}