using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponPoint : MonoBehaviour
    {
        [SerializeField] private string pointId = "Point";
        [SerializeField] private WeaponPointType pointType = WeaponPointType.Custom;
        [SerializeField] private bool drawGizmo = true;
        [SerializeField, Min(0.001f)] private float gizmoSize = 0.025f;

        public string PointId => pointId;
        public WeaponPointType PointType => pointType;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            float size = Mathf.Max(0.001f, gizmoSize);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireSphere(Vector3.zero, size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size * 2.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string newPointId, WeaponPointType newPointType)
        {
            pointId = newPointId;
            pointType = newPointType;
        }
#endif
    }
}
