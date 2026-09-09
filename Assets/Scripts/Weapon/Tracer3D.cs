using UnityEngine;

public class Tracer3D : MonoBehaviour
{
    public float baseLifetime = 0.06f;
    public float lifetimePerMeter = 0.002f;

    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float totalLifetime;
    private float elapsed;
    private Renderer tracerRenderer;
    private MaterialPropertyBlock properties;
    private Color originalColor;
    private int colorPropertyId;
    private bool canFade;
    private bool initialized;

    public void Initialize(float distance)
    {
        elapsed = 0f;
        totalLifetime = Mathf.Max(0.001f,
            baseLifetime + Mathf.Max(0f, distance) * lifetimePerMeter);

        tracerRenderer = GetComponentInChildren<Renderer>();
        canFade = false;

        if (tracerRenderer != null)
        {
            // Read the asset without creating a separate material instance.
            Material material = tracerRenderer.sharedMaterial;
            if (material != null && TryGetColorProperty(material, out colorPropertyId))
            {
                originalColor = material.GetColor(colorPropertyId);
                if (properties == null)
                    properties = new MaterialPropertyBlock();
                canFade = true;
                ApplyFade(1f);
            }
        }

        // Even a renderer with an unsupported shader still expires normally.
        initialized = true;
    }

    private static bool TryGetColorProperty(Material material, out int propertyId)
    {
        if (material.HasProperty(UnlitColorId))
            propertyId = UnlitColorId; // HDRP/Unlit
        else if (material.HasProperty(BaseColorId))
            propertyId = BaseColorId;  // HDRP/Lit and many URP shaders
        else if (material.HasProperty(ColorId))
            propertyId = ColorId;      // Built-in shaders
        else
        {
            propertyId = 0;
            return false;
        }

        return true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;
        ApplyFade(Mathf.Clamp01(1f - elapsed / totalLifetime));

        if (elapsed >= totalLifetime)
            Destroy(gameObject);
    }

    private void ApplyFade(float fade)
    {
        if (!canFade || tracerRenderer == null)
            return;

        // Use a Transparent material with Alpha blending for a visible fade.
        Color color = originalColor;
        color.a *= fade;
        tracerRenderer.GetPropertyBlock(properties);
        properties.SetColor(colorPropertyId, color);
        tracerRenderer.SetPropertyBlock(properties);
    }
}
