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
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(color, maxAlpha, duration));
    }

    private IEnumerator FlashRoutine(Color color, float maxAlpha, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, timer / duration);

            flashImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        flashImage.color = new Color(color.r, color.g, color.b, 0f);
        flashRoutine = null;
    }
}
