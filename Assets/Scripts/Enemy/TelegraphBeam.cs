using UnityEngine;

public class TelegraphBeam : MonoBehaviour
{
    private Transform origin;
    private Transform target;
    private float duration;

    [Header("Thickness")]
    public float startThickness = 0.5f;
    public float endThickness = 0.1f;

    private float timer;
    private Transform beam; // child that visually stretches

    public void Initialize(Transform origin, Transform target, float duration)
    {
        this.origin = origin;
        this.target = target;
        this.duration = duration;

        beam = transform.GetChild(0); // "BeamMesh"
    }

    void Update()
    {
        if (!origin || !target)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;
        float t = timer / duration;

        // Position root at bow
        transform.position = origin.position;

        // Rotate root toward player
        Vector3 dir = target.position - origin.position;
        transform.rotation = Quaternion.LookRotation(dir);

        float distance = dir.magnitude;

        // Thickness change over time
        float thickness = Mathf.Lerp(startThickness, endThickness, t);

        // Now stretch the beam mesh FORWARD from the pivot
        beam.localScale = new Vector3(thickness, thickness, distance);

        if (timer >= duration)
            Destroy(gameObject);
    }
}
