using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFlashEffect : MonoBehaviour
{
    [Header("References")]
    public Image flashImage;

    [Header("Defaults")]
    public float defaultDuration = 0.2f;
    public float defaultMaxAlpha = 0.4f;
    public Color defaultColor = Color.red;  // red = damage

    private Coroutine flashRoutine;

    /// <summary>
    /// Play a flash using the default color/alpha/duration.
    /// </summary>
    public void FlashDefault()
    {
        Flash(defaultColor, defaultMaxAlpha, defaultDuration);
    }

    /// <summary>
    /// Play a colored flash with default alpha/duration.
    /// </summary>
    public void Flash(Color color)
    {
        Flash(color, defaultMaxAlpha, defaultDuration);
    }

    /// <summary>
    /// Full control flash: color, alpha, and duration.
    /// </summary>
    public void Flash(Color color, float maxAlpha, float duration)
    {
        if (!isActiveAndEnabled || flashImage == null) return;
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = null;
        if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
        {
            ClearFlash();
            return;
        }
        flashRoutine = StartCoroutine(FlashRoutine(color, Mathf.Clamp01(maxAlpha), duration));
    }

    private IEnumerator FlashRoutine(Color color, float maxAlpha, float duration)
    {
        float timer = 0f;

        while (timer < duration && flashImage != null)
        {
            // Game-over pauses gameplay time; the screen must still clear.
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, timer / duration);

            flashImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        ClearFlash();
        flashRoutine = null;
    }

    private void ClearFlash()
    {
        if (flashImage == null) return;
        Color color = flashImage.color;
        color.a = 0f;
        flashImage.color = color;
    }

    private void OnDisable()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
        ClearFlash();
    }
}
