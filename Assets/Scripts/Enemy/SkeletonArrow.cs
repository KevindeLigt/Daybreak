using UnityEngine;

public class SkeletonArrow : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 40f;
    public float lifetime = 5f;

    public GameObject hitVFX;

    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        hasHit = true;

        if (other.TryGetComponent(out PlayerHealth player))
        {
            player.TakeDamage(damage);
        }

        if (hitVFX)
            Instantiate(hitVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
