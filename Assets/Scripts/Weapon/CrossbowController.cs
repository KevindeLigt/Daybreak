using UnityEngine;
using System.Collections;

public class CrossbowController : WeaponBase
{
    [Header("Crossbow Settings")]
    public float baseDamage = 80f;
    public float reloadTime = 1.6f;
    public float fireCooldown = 0.2f;
    public float projectileSpeed = 120f;
    public bool boltLoaded = true;

    [Header("References")]
    public Transform boltSpawnPoint;
    public GameObject boltPrefab;
    public Camera fpsCamera;

    [Header("Audio")]
    public AudioClip fireSFX;
    public AudioClip reloadSFX;
    public float fireVolume = 1f;
    public float reloadVolume = 1f;

    [Header("Animation")]
    public Animator anim;
    public string fireTrigger = "CB_Fire";
    public string reloadTrigger = "CB_Reload";

    private bool isReloading = false;
    private bool onCooldown = false;

    private RecoilSystem recoil;
    private CameraShake camShake;

    void Start()
    {
        recoil = fpsCamera.GetComponent<RecoilSystem>();
        camShake = fpsCamera.GetComponent<CameraShake>();
    }

    // ----------------------------------------------------------------------
    // WEAPONBASE IMPLEMENTATION
    // ----------------------------------------------------------------------

    public override void OnFire()
    {
        if (!boltLoaded || isReloading || onCooldown)
            return;

        StartCoroutine(FireRoutine());
    }

    public override void OnReload()
    {
        if (!boltLoaded && !isReloading)
            StartCoroutine(ReloadRoutine());
    }

    public override void OnEquip()
    {
        gameObject.SetActive(true);
    }

    public override void OnUnequip()
    {
        gameObject.SetActive(false);
    }

    // ----------------------------------------------------------------------

    private IEnumerator FireRoutine()
    {
        onCooldown = true;
        boltLoaded = false;

        // Animation
        if (anim) anim.SetTrigger(fireTrigger);

        // Audio
        if (fireSFX)
            AudioSource.PlayClipAtPoint(fireSFX, transform.position, fireVolume);

        // Screen recoil
        if (recoil) recoil.FireRecoil();
        if (camShake) camShake.Shake(0.05f, 0.15f);

        // Spawn the bolt
        FireBolt();

        yield return new WaitForSeconds(fireCooldown);
        onCooldown = false;
    }

    private void FireBolt()
    {
        if (!boltPrefab || !boltSpawnPoint) return;

        GameObject bolt = Instantiate(boltPrefab, boltSpawnPoint.position, boltSpawnPoint.rotation);

        Rigidbody rb = bolt.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = boltSpawnPoint.forward * projectileSpeed;
        }

        // Provide the bolt with damage context
        CrossbowBolt boltComp = bolt.GetComponent<CrossbowBolt>();
        if (boltComp)
        {
            boltComp.damage = baseDamage;
            boltComp.shooter = this;
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        // Animation
        if (anim) anim.SetTrigger(reloadTrigger);

        // Audio
        if (reloadSFX)
            AudioSource.PlayClipAtPoint(reloadSFX, transform.position, reloadVolume);

        yield return new WaitForSeconds(reloadTime);

        boltLoaded = true;
        isReloading = false;
    }
}
