using UnityEngine;
using System.Collections;

public class DoubleBarrelShotgunController : WeaponBase
{
    [Header("Shotgun Settings")]
    public int pelletsPerShot = 12;
    public float spreadAngle = 6f;
    public float range = 40f;
    public float damagePerPellet = 10f;
    public float forcePerPellet = 3f;

    [Header("Reloading")]
    public float openTime = 0.4f;
    public float loadShellTime = 0.6f;
    public float closeTime = 0.4f;

    [Header("Audio Clips")]
    public AudioClip shotgunFireSFX;
    public AudioClip dryFireSFX;
    public AudioClip shotgunOpenSFX;
    public AudioClip shotgunInsertShellSFX;
    public AudioClip shotgunCloseSFX;
    public AudioClip pelletHitSFX;

    [Header("Audio Settings")]
    public float fireVolume = 1f;
    public float hitVolume = 0.6f;
    public float reloadVolume = 0.8f;
    public float dryFireVolume = 1f;

    [Header("VFX Prefabs")]
    public GameObject hitParticlePrefab;
    public GameObject muzzleFlashPrefab;

    [Header("References")]
    public Camera fpsCamera;
    public Transform leftBarrel;
    public Transform rightBarrel;
    public GameObject tracerPrefab;
    public LayerMask hitMask;

    private int shellsLoaded = 2;
    private bool isReloading = false;
    private bool isFiring = false;
    private bool fireLeftNext = true;

    private RecoilSystem recoil;
    private CameraShake cameraShake;
    private WeaponSway weaponSway;

    [Header("Animation")]
    private Animator anim;
    [Header("Animation Triggers")]
    public string fireTrigger = "FireTrigger";
    public string reloadOpenTrigger = "ReloadOpenTrigger";
    public string insertShellTrigger = "InsertShellTrigger";
    public string reloadCloseTrigger = "ReloadCloseTrigger";
    public string idleState = "SG_Idle";

    void Start()
    {
        recoil = fpsCamera.GetComponent<RecoilSystem>();
        cameraShake = fpsCamera.GetComponent<CameraShake>();
        weaponSway = GetComponentInChildren<WeaponSway>();
        anim = GetComponentInChildren<Animator>();
    }

    // ----------------------------------------------------------------------
    // WEAPONBASE OVERRIDES
    // ----------------------------------------------------------------------

    public override void OnFire()
    {
        TryFire();
    }

    public override void OnReload()
    {
        if (!isReloading)
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

    private void TryFire()
    {
        if (isReloading) return;

        if (shellsLoaded <= 0)
        {
            if (dryFireSFX)
                AudioSource.PlayClipAtPoint(dryFireSFX, transform.position, dryFireVolume);

            Debug.Log("CLICK! Empty!");
            return;
        }

        StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        isFiring = true;

        Transform muzzle = fireLeftNext ? leftBarrel : rightBarrel;
        fireLeftNext = !fireLeftNext;

        if (anim) anim.SetTrigger(fireTrigger);
        PlayerStatsManager.Instance.AddShot("Shotgun");

        if (muzzleFlashPrefab)
            Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation);

        FirePellets(muzzle);

        if (recoil) recoil.FireRecoil();
        if (cameraShake) cameraShake.Shake(0.1f, 0.25f);
        if (weaponSway) weaponSway.Kick();

        if (shotgunFireSFX != null)
            AudioSource.PlayClipAtPoint(shotgunFireSFX, muzzle.position, fireVolume);

        shellsLoaded--;
        UIManager.Instance.UpdateShotgunAmmo(shellsLoaded);

        yield return new WaitForSeconds(0.1f);
        isFiring = false;
    }

    private void FirePellets(Transform muzzle)
    {
        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector3 direction = GetSpreadDirection();

            if (Physics.Raycast(fpsCamera.transform.position, direction, out RaycastHit hit, range, hitMask))
            {
                SpawnTracer(muzzle.position, hit.point);

                EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
                if (health != null)
                {
                    float finalDamage = PlayerWeaponStats.Instance.CalculatePelletDamage(damagePerPellet);
                    Vector3 force = direction.normalized * forcePerPellet;
                    health.TakeDamage(finalDamage, force);
                    PlayerStatsManager.Instance.AddDamageDealt(finalDamage);

                    EnemyHitReaction reaction = health.GetComponent<EnemyHitReaction>();
                    if (reaction != null)
                        reaction.OnHit(direction.normalized, forcePerPellet);

                    if (hitParticlePrefab != null)
                        Instantiate(hitParticlePrefab, hit.point, Quaternion.LookRotation(hit.normal));

                    PlayClipAtPointWithPitch(pelletHitSFX, hit.point, hitVolume, 0.9f, 1.1f);
                }

            }
            else
            {
                SpawnTracer(muzzle.position, fpsCamera.transform.position + direction * range);
            }
        }
    }

    private Vector3 GetSpreadDirection()
    {
        float x = Random.Range(-spreadAngle, spreadAngle);
        float y = Random.Range(-spreadAngle, spreadAngle);
        return Quaternion.Euler(x, y, 0) * fpsCamera.transform.forward;
    }

    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (tracerPrefab == null) return;

        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);

        GameObject tracer = Instantiate(tracerPrefab, start, Quaternion.LookRotation(direction));
        tracer.transform.localScale = new Vector3(
            tracer.transform.localScale.x,
            tracer.transform.localScale.y,
            distance * 0.5f
        );
        tracer.transform.position = start + direction * (distance * 0.5f);

        Tracer3D tracerComp = tracer.GetComponent<Tracer3D>();
        tracerComp?.Initialize(distance);
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (anim) anim.SetTrigger(reloadOpenTrigger);
        if (shotgunOpenSFX)
            AudioSource.PlayClipAtPoint(shotgunOpenSFX, transform.position, reloadVolume);

        yield return new WaitForSeconds(openTime);

        while (shellsLoaded < 2)
        {
            if (anim) anim.SetTrigger(insertShellTrigger);
            shellsLoaded++;
            UIManager.Instance.UpdateShotgunAmmo(shellsLoaded);
            yield return new WaitForSeconds(loadShellTime);
        }

        if (shotgunCloseSFX)
            AudioSource.PlayClipAtPoint(shotgunCloseSFX, transform.position, reloadVolume);
        if (anim) anim.SetTrigger(reloadCloseTrigger);

        yield return new WaitForSeconds(closeTime);

        isReloading = false;
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float volume, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        if (clip == null) return;

        GameObject go = new GameObject("OneShotAudio");
        go.transform.position = position;

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.pitch = Random.Range(minPitch, maxPitch);

        src.Play();
        Destroy(go, clip.length / Mathf.Max(src.pitch, 0.01f));
    }
}
