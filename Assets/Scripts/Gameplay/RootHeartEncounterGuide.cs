using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary navigation cue created by GameFlowManager. Does not activate a
/// pickup or change progression. Has its own non-interactive overlay canvas.
/// Replace its presentation with authored world cues after testing the route.
/// </summary>
[DisallowMultipleComponent]
public class RootHeartEncounterGuide : MonoBehaviour
{
    private GameFlowManager owner;
    private CurseObjectiveController objective;
    private Transform heart;
    private Camera viewingCamera;
    private GameObject canvasObject;
    private Canvas canvas;
    private TextMeshProUGUI label;
    private bool warnedFont;

    public void Bind(GameFlowManager manager, CurseObjectiveController curseObjective,
        Transform heartTransform, Camera cameraOverride)
    {
        owner = manager;
        objective = curseObjective;
        heart = heartTransform;
        viewingCamera = cameraOverride;
        EnsureDisplay();
    }

    public void ClearGuide()
    {
        objective = null;
        heart = null;
        if (canvas != null) canvas.enabled = false;
    }

    private void EnsureDisplay()
    {
        if (canvas != null) return;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            if (!warnedFont) Debug.LogWarning("Root Heart guide needs the default TMP font. Import TMP Essential Resources.", this);
            warnedFont = true;
            return;
        }

        canvasObject = new GameObject("RootHeartEncounterGuide_Canvas", typeof(RectTransform));
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("HeartGuideText", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 23f;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = false;
        label.raycastTarget = false;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        label.rectTransform.sizeDelta = new Vector2(260f, 100f);
        canvas.enabled = false;
        // No GraphicRaycaster: the marker cannot block gameplay UI/input.
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        if (owner == null || !owner.isActiveAndEnabled || !owner.GameActive ||
            objective == null || !objective.isActiveAndEnabled || objective.IsCompleted)
        {
            canvas.enabled = false;
            return;
        }

        bool carrying = objective.HasRootHeart;
        Transform destination = carrying ? objective.AnchorTransform : heart;
        if (viewingCamera == null) viewingCamera = Camera.main;
        if (destination == null || viewingCamera == null || !viewingCamera.isActiveAndEnabled)
        {
            canvas.enabled = false;
            return;
        }

        string title = carrying ? "CURSE ANCHOR" : "ROOT HEART";
        string action = carrying ? "Place Root Heart" :
            (objective.HasReleasedHeart ? "Ready to collect" : "Clear wave to release");
        label.color = carrying || objective.HasReleasedHeart
            ? new Color(0.55f, 1f, 0.7f) : new Color(1f, 0.78f, 0.35f);

        Vector3 projected = viewingCamera.WorldToViewportPoint(destination.position + Vector3.up * 1.5f);
        Vector2 direction = new Vector2((projected.x - 0.5f) * Screen.width,
            (projected.y - 0.5f) * Screen.height);
        bool behind = projected.z <= 0f;
        if (behind) direction = direction * -1f;

        float halfWidth = Mathf.Max(1f, Screen.width * 0.5f - Mathf.Min(Screen.width * 0.3f, 145f * canvas.scaleFactor));
        float halfHeight = Mathf.Max(1f, Screen.height * 0.5f - Mathf.Min(Screen.height * 0.3f, 65f * canvas.scaleFactor));
        bool atEdge = behind || Mathf.Abs(direction.x) > halfWidth || Mathf.Abs(direction.y) > halfHeight;
        if (atEdge)
        {
            // Directly behind the camera has no stable horizontal projection.
            // Put that indicator at the bottom, explicitly labelled Behind you.
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) < 0.01f)
                direction = new Vector2(0f, -1f);
            float multiplier = Mathf.Min(halfWidth / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                halfHeight / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            direction = direction * multiplier;
            if (behind) action = "Behind you - " + action;
            else if (Mathf.Abs(direction.x) / halfWidth >= Mathf.Abs(direction.y) / halfHeight)
                title = direction.x < 0f ? "< " + title : title + " >";
            else title = direction.y > 0f ? "^ " + title : "v " + title;
        }

        label.text = title + "\n" + action;
        label.rectTransform.position = new Vector3(Screen.width * 0.5f + direction.x,
            Screen.height * 0.5f + direction.y, 0f);
        canvas.enabled = true;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }
}
