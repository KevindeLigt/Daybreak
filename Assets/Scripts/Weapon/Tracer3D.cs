using UnityEngine;

public class Tracer3D : MonoBehaviour
{
    public float lifetime = 0.15f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
