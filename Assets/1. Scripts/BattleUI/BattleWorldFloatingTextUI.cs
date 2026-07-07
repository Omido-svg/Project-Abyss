using System.Collections;
using TMPro;
using UnityEngine;

public class BattleWorldFloatingTextUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private Camera worldCamera;

    private Transform followTarget;
    private Vector3 worldOffset;
    private float cameraRightOffset;

    private void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (text != null)
        {
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    private void LateUpdate()
    {
        FollowTarget();
        FaceCamera();
    }

    public void Initialize(
        Transform target,
        Camera camera,
        Vector3 offset,
        float rightOffset,
        string initialText,
        Color color,
        int sortingOrder)
    {
        followTarget = target;
        worldCamera = camera;
        worldOffset = offset;
        cameraRightOffset = rightOffset;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        SetText(initialText);
        SetColor(color);

        FollowTarget();
        FaceCamera();
    }

    public void SetText(string value)
    {
        if (text == null)
            return;

        text.text = value;
    }

    public void SetColor(Color color)
    {
        if (text == null)
            return;

        text.color = color;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
    }

    public IEnumerator RollToValue(
        int finalValue,
        float rollDuration,
        float tickInterval,
        int randomMin,
        int randomMax)
    {
        SetAlpha(1f);

        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;

                int randomValue =
                    Random.Range(randomMin, randomMax + 1);

                SetText(randomValue.ToString());
            }

            yield return null;
        }

        SetText(finalValue.ToString());
    }

    public IEnumerator FadeAndDestroy(float fadeDuration)
    {
        float fadeElapsed = 0f;

        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(fadeElapsed / fadeDuration);

            SetAlpha(1f - t);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void FollowTarget()
    {
        if (followTarget == null)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        Vector3 cameraRight =
            worldCamera != null
                ? worldCamera.transform.right
                : Vector3.right;

        transform.position =
            followTarget.position +
            worldOffset +
            cameraRight * cameraRightOffset;
    }

    private void FaceCamera()
    {
        if (!faceCamera)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return;

        transform.rotation =
            worldCamera.transform.rotation *
            Quaternion.Euler(rotationOffset);
    }
}