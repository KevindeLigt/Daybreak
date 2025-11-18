using UnityEngine;

public class Tracer3D : MonoBehaviour
{
    public float baseLifetime = 0.06f;   // short shotgun burst
    public float lifetimePerMeter = 0.002f;

    private float totalLifetime;
    private float t = 0f;

    private Renderer rend;
    private Color originalColor;

    public void Initialize(float distance)
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend)
        {
            originalColor = rend.material.GetColor("_BaseColor");
        }

        // Lifetime scales with distance
        totalLifetime = baseLifetime + distance * lifetimePerMeter;
    }

    void Update()
    {
        t += Time.deltaTime;

        if (rend)
        {
            float fade = 1f - (t / totalLifetime);

            Color c = originalColor;
            c.a = fade;
            rend.material.SetColor("_BaseColor", c);
        }

        if (t >= totalLifetime)
            Destroy(gameObject);
    }
}
