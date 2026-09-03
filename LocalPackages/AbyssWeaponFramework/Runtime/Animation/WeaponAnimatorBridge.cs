using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        public Animator Animator => animator != null ? animator : GetComponent<Animator>();
        private void Reset() => animator = GetComponent<Animator>();

        public void SetTrigger(string parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter) && Animator != null) Animator.SetTrigger(parameter);
        }
        public void ResetTrigger(string parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter) && Animator != null) Animator.ResetTrigger(parameter);
        }
        public void SetBool(string parameter, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameter) && Animator != null) Animator.SetBool(parameter, value);
        }
        public void SetFloat(string parameter, float value)
        {
            if (!string.IsNullOrWhiteSpace(parameter) && Animator != null) Animator.SetFloat(parameter, value);
        }
        public void SetInteger(string parameter, int value)
        {
            if (!string.IsNullOrWhiteSpace(parameter) && Animator != null) Animator.SetInteger(parameter, value);
        }
    }
}
