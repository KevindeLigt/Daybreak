using UnityEngine;

public class SkeletonArrow : MonoBehaviour
{
    public float damage = 10f;
    public float speed = 40f;
    public float lifetime = 5f;
    private Vector3 moveDir;

    public GameObject hitVFX;

    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += moveDir * speed * Time.deltaTime;
    }

    public void SetDirection(Vector3 dir)
    {
        moveDir = dir;
    }



    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        hasHit = true;

        if (other.TryGetComponent(out PlayerHealth player))
        {
            player.TakeDamage(damage);
            PlayerStatsManager.Instance.AddDamageDealt(damage);
        }

        if (hitVFX)
            Instantiate(hitVFX, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
