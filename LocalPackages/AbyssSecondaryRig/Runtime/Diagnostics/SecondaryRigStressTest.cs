using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    public enum SecondaryRigStressPreset
    {
        Gentle = 0,
        Strong = 1,
        Extreme = 2
    }

    [DefaultExecutionOrder(8500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Project Abyss/Secondary Rig/Diagnostics/Stress Test")]
    public sealed class SecondaryRigStressTest : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform motionTarget;
        [SerializeField] private SecondaryRigController secondaryRigController;

        [Header("Playback")]
        [SerializeField] private bool runOnPlay = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool restorePoseWhenStopped = true;
        [SerializeField] private bool resetSecondaryRigWhenStarting = true;

        [Header("Translation")]
        [SerializeField] private bool enableTranslation = true;
        [SerializeField] private Vector3 translationAmplitude = new(0.12f, 0.03f, 0.08f);
        [SerializeField, Min(0f)] private float translationFrequency = 1.15f;

        [Header("Rotation")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private Vector3 rotationAmplitude = new(8f, 35f, 12f);
        [SerializeField, Min(0f)] private float rotationFrequency = 1.35f;

        private Vector3 baselineLocalPosition;
        private Quaternion baselineLocalRotation;
        private bool baselineCaptured;
        private bool running;
        private float elapsed;

        public Transform MotionTarget
        {
            get => motionTarget != null ? motionTarget : transform;
            set
            {
                motionTarget = value;
                baselineCaptured = false;
            }
        }

        public SecondaryRigController Controller
        {
            get => secondaryRigController;
            set => secondaryRigController = value;
        }

        public bool IsRunning => running;

        private void Reset()
        {
            motionTarget = transform;
            secondaryRigController = GetComponent<SecondaryRigController>();
            ApplyPreset(SecondaryRigStressPreset.Strong);
        }

        private void Start()
        {
            if (motionTarget == null)
                motionTarget = transform;

            if (secondaryRigController == null)
                secondaryRigController = GetComponentInParent<SecondaryRigController>();

            CaptureBaseline();

            if (runOnPlay)
                BeginTest(false);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (restorePoseWhenStopped)
                RestoreBaseline();

            if (secondaryRigController != null)
                secondaryRigController.ResetSimulation();

            running = false;
        }

        private void LateUpdate()
        {
            if (!running)
                return;

            Transform target = MotionTarget;
            if (target == null)
                return;

            if (!baselineCaptured)
                CaptureBaseline();

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += Mathf.Max(0f, deltaTime);

            Vector3 position = baselineLocalPosition;
            Quaternion rotation = baselineLocalRotation;

            if (enableTranslation && translationFrequency > 0f)
            {
                float phase = elapsed * translationFrequency * Mathf.PI * 2f;
                position += new Vector3(
                    Mathf.Sin(phase) * translationAmplitude.x,
                    Mathf.Sin(phase * 1.37f + 0.7f) * translationAmplitude.y,
                    Mathf.Cos(phase * 0.83f + 0.35f) * translationAmplitude.z);
            }

            if (enableRotation && rotationFrequency > 0f)
            {
                float phase = elapsed * rotationFrequency * Mathf.PI * 2f;
                Vector3 euler = new(
                    Mathf.Sin(phase * 0.71f + 0.3f) * rotationAmplitude.x,
                    Mathf.Sin(phase) * rotationAmplitude.y,
                    Mathf.Cos(phase * 0.83f + 0.6f) * rotationAmplitude.z);

                rotation = baselineLocalRotation * Quaternion.Euler(euler);
            }

            target.SetLocalPositionAndRotation(position, rotation);
        }

        public void BeginTest(bool recaptureBaseline = true)
        {
            if (recaptureBaseline || !baselineCaptured)
                CaptureBaseline();

            elapsed = 0f;
            running = true;

            if (resetSecondaryRigWhenStarting && secondaryRigController != null)
                secondaryRigController.ResetSimulation();
        }

        public void StopTest(bool restorePose = true)
        {
            running = false;

            if (restorePose)
                RestoreBaseline();

            if (secondaryRigController != null)
                secondaryRigController.ResetSimulation();
        }

        public void CaptureBaseline()
        {
            Transform target = MotionTarget;
            if (target == null)
                return;

            baselineLocalPosition = target.localPosition;
            baselineLocalRotation = target.localRotation;
            baselineCaptured = true;
        }

        public void RestoreBaseline()
        {
            if (!baselineCaptured)
                return;

            Transform target = MotionTarget;
            if (target == null)
                return;

            target.SetLocalPositionAndRotation(baselineLocalPosition, baselineLocalRotation);
        }

        public void ApplyPreset(SecondaryRigStressPreset preset)
        {
            switch (preset)
            {
                case SecondaryRigStressPreset.Gentle:
                    translationAmplitude = new Vector3(0.04f, 0.01f, 0.025f);
                    translationFrequency = 0.8f;
                    rotationAmplitude = new Vector3(4f, 15f, 5f);
                    rotationFrequency = 0.9f;
                    break;

                case SecondaryRigStressPreset.Extreme:
                    translationAmplitude = new Vector3(0.2f, 0.05f, 0.14f);
                    translationFrequency = 1.75f;
                    rotationAmplitude = new Vector3(15f, 55f, 20f);
                    rotationFrequency = 2f;
                    break;

                default:
                    translationAmplitude = new Vector3(0.12f, 0.03f, 0.08f);
                    translationFrequency = 1.15f;
                    rotationAmplitude = new Vector3(8f, 35f, 12f);
                    rotationFrequency = 1.35f;
                    break;
            }
        }

        private void OnValidate()
        {
            translationFrequency = Mathf.Max(0f, translationFrequency);
            rotationFrequency = Mathf.Max(0f, rotationFrequency);
        }
    }
}
