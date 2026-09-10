using UnityEngine;

/// <summary>
/// v4.2 compatibility component.
///
/// The legacy world-space StatusPopup UI has been retired.
/// The one-time v4.2 installer removes this component and its
/// per-character Canvas from authored battle scenes.
///
/// This no-op type remains temporarily so old scenes can deserialize
/// cleanly before the installer removes the component.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class StatusPopup : MonoBehaviour
{
    public static void SetDetailCameraOverrideActive(
        bool active)
    {
    }

    public static void SuspendAllWithoutCameraReturn()
    {
    }

    public void Show()
    {
    }
}
