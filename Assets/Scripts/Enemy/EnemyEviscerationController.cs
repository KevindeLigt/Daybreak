using UnityEngine;
using System.Collections.Generic;

public class EnemyEviscerationController : MonoBehaviour
{
    [System.Serializable]
    public class GibSpawnDefinition
    {
        [Header("Gib")]
        public GameObject gibPrefab;

        [Tooltip("Bone or helper transform where this body part should spawn.")]
        public Transform spawnAnchor;

        [Tooltip("Always attempt to spawn this piece before optional pieces.")]
        public bool alwaysSpawn = false;

        [Range(0f, 1f)]
        [Tooltip("Chance for this piece to spawn when it is not marked Always Spawn.")]
        public float spawnChance = 0.65f;

        [Header("Physics")]
        [Tooltip("Multiplier applied to the base evisceration impulse.")]
        public float forceMultiplier = 1f;

        [Tooltip("Multiplier applied to the random spin.")]
        public float torqueMultiplier = 1f;

        [Header("Blood Trail")]
        [Tooltip("Attach the shared blood-trail prefab to this gib.")]
        public bool attachBloodTrail = false;
    }

    [Header("Original Zombie Body")]
    [Tooltip("Assign the zombie skin, clothes, and accessory renderers that should disappear.")]
    [SerializeField] private Renderer[] originalBodyRenderers;

    [Header("Gib Spawns")]
    [SerializeField] private GibSpawnDefinition[] gibSpawns;

    [Min(1)]
    [Tooltip("Maximum number of physical body parts spawned by one evisceration.")]
    [SerializeField] private int maxGibsToSpawn = 4;

    [Header("Blood Effects")]
    [Tooltip("Large one-shot blood burst spawned at the centre of the zombie.")]
    [SerializeField] private GameObject bloodBurstPrefab;

    [Tooltip("Optional anchor for the central blood burst. Falls back to this object's position.")]
    [SerializeField] private Transform bloodBurstAnchor;

    [Tooltip("Optional short blood trail attached to selected gib pieces.")]
    [SerializeField] private GameObject bloodTrailPrefab;

    [Header("Evisceration Audio")]
    [SerializeField] private AudioClip[] eviscerationClips;

    [Range(0f, 1f)]
    [SerializeField] private float eviscerationVolume = 1f;

    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    [Min(0.1f)]
    [SerializeField] private float audioMinDistance = 2f;

    [Min(1f)]
    [SerializeField] private float audioMaxDistance = 30f;

    [Header("Gib Force")]
    [Tooltip("Minimum impulse used when the supplied force is very small.")]
    [SerializeField] private float minimumBaseImpulse = 10f;

    [Tooltip("Random multiplier applied to each body's launch force.")]
    [SerializeField] private Vector2 forceVariation = new Vector2(0.8f, 1.25f);

    [Range(0f, 1.5f)]
    [Tooltip("Adds random sideways variation to each launch direction.")]
    [SerializeField] private float directionRandomness = 0.45f;

    [Range(0f, 1.5f)]
    [Tooltip("Adds an upward bias so pieces do not only travel horizontally.")]
    [SerializeField] private float upwardBias = 0.35f;

    [Tooltip("Random angular impulse applied to each gib.")]
    [SerializeField] private float torqueStrength = 8f;

    [Header("Lifetime")]
    [Min(0.1f)]
    [SerializeField] private float gibLifetime = 8f;

    [Min(0f)]
    [Tooltip("Random amount added or subtracted from gib lifetime.")]
    [SerializeField] private float gibLifetimeVariation = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool hasEviscerated;

    public bool HasEviscerated => hasEviscerated;

    /// <summary>
    /// Compatibility overload for an existing death path that only supplies force.
    /// </summary>
    public void Eviscerate(Vector3 force)
    {
        Vector3 direction = force.sqrMagnitude > 0.001f
            ? force.normalized
            : transform.forward;

        Vector3 estimatedSourcePosition = transform.position - direction;
        Eviscerate(estimatedSourcePosition, force);
    }

    /// <summary>
    /// Hides the original zombie and replaces it with physical body-part prefabs,
    /// blood effects, and a localised evisceration sound.
    /// Call EnemyRagdollController.DisableBodyForEvisceration() before this.
    /// </summary>
    public void Eviscerate(Vector3 sourcePosition, Vector3 force)
    {
        if (hasEviscerated)
            return;

        hasEviscerated = true;

        DisableOriginalBodyRenderers();
        SpawnBloodBurst();
        PlayEviscerationSound();

        int spawnedCount = SpawnGibs(sourcePosition, force);

        if (debugLogs)
        {
            Debug.Log(
                $"{name}: Eviscerated and spawned {spawnedCount} gib(s).",
                this
            );
        }
    }

    private void DisableOriginalBodyRenderers()
    {
        if (originalBodyRenderers == null)
            return;

        foreach (Renderer bodyRenderer in originalBodyRenderers)
        {
            if (bodyRenderer != null)
                bodyRenderer.enabled = false;
        }
    }

    private void SpawnBloodBurst()
    {
        if (bloodBurstPrefab == null)
            return;

        Vector3 spawnPosition = bloodBurstAnchor != null
            ? bloodBurstAnchor.position
            : transform.position + Vector3.up;

        Quaternion spawnRotation = bloodBurstAnchor != null
            ? bloodBurstAnchor.rotation
            : Quaternion.identity;

        Instantiate(bloodBurstPrefab, spawnPosition, spawnRotation);
    }

    private int SpawnGibs(Vector3 sourcePosition, Vector3 force)
    {
        if (gibSpawns == null || gibSpawns.Length == 0)
            return 0;

        int spawnedCount = 0;

        for (int i = 0; i < gibSpawns.Length; i++)
        {
            if (spawnedCount >= maxGibsToSpawn)
                break;

            GibSpawnDefinition definition = gibSpawns[i];

            if (definition == null || !definition.alwaysSpawn)
                continue;

            if (TrySpawnGib(definition, sourcePosition, force))
                spawnedCount++;
        }

        if (spawnedCount >= maxGibsToSpawn)
            return spawnedCount;

        List<int> optionalIndices = new List<int>();

        for (int i = 0; i < gibSpawns.Length; i++)
        {
            GibSpawnDefinition definition = gibSpawns[i];

            if (definition != null && !definition.alwaysSpawn)
                optionalIndices.Add(i);
        }

        Shuffle(optionalIndices);

        foreach (int index in optionalIndices)
        {
            if (spawnedCount >= maxGibsToSpawn)
                break;

            GibSpawnDefinition definition = gibSpawns[index];

            if (Random.value > definition.spawnChance)
                continue;

            if (TrySpawnGib(definition, sourcePosition, force))
                spawnedCount++;
        }

        return spawnedCount;
    }

    private bool TrySpawnGib(
        GibSpawnDefinition definition,
        Vector3 sourcePosition,
        Vector3 force)
    {
        if (definition.gibPrefab == null || definition.spawnAnchor == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"{name}: Gib entry is missing a prefab or spawn anchor.",
                    this
                );
            }

            return false;
        }

        GameObject gib = Instantiate(
            definition.gibPrefab,
            definition.spawnAnchor.position,
            definition.spawnAnchor.rotation
        );

        EnableGibCollision(gib);

        Vector3 baseDirection = force.sqrMagnitude > 0.001f
            ? force.normalized
            : (definition.spawnAnchor.position - sourcePosition).normalized;

        if (baseDirection.sqrMagnitude < 0.001f)
            baseDirection = transform.forward;

        Vector3 randomDirection =
            baseDirection +
            Random.insideUnitSphere * directionRandomness +
            Vector3.up * upwardBias;

        randomDirection.Normalize();

        float baseImpulse = Mathf.Max(minimumBaseImpulse, force.magnitude);

        float finalImpulse =
            baseImpulse *
            Mathf.Max(0f, definition.forceMultiplier) *
            Random.Range(forceVariation.x, forceVariation.y);

        Rigidbody gibBody = gib.GetComponentInChildren<Rigidbody>();

        if (gibBody != null)
        {
            gibBody.detectCollisions = true;
            gibBody.isKinematic = false;
            gibBody.useGravity = true;

            gibBody.AddForce(
                randomDirection * finalImpulse,
                ForceMode.Impulse
            );

            gibBody.AddTorque(
                Random.insideUnitSphere *
                torqueStrength *
                Mathf.Max(0f, definition.torqueMultiplier),
                ForceMode.Impulse
            );

            gibBody.WakeUp();
        }
        else if (debugLogs)
        {
            Debug.LogWarning(
                $"{gib.name}: Spawned gib has no Rigidbody.",
                gib
            );
        }

        if (definition.attachBloodTrail && bloodTrailPrefab != null)
        {
            GameObject trail = Instantiate(
                bloodTrailPrefab,
                gib.transform.position,
                Quaternion.identity,
                gib.transform
            );

            trail.transform.localPosition = Vector3.zero;
        }

        float lifetime = Mathf.Max(
            0.1f,
            gibLifetime + Random.Range(-gibLifetimeVariation, gibLifetimeVariation)
        );

        Destroy(gib, lifetime);

        return true;
    }

    private static void EnableGibCollision(GameObject gib)
    {
        Collider[] colliders = gib.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            col.enabled = true;
            col.isTrigger = false;
        }
    }

    private void PlayEviscerationSound()
    {
        if (eviscerationClips == null || eviscerationClips.Length == 0)
            return;

        AudioClip clip =
            eviscerationClips[Random.Range(0, eviscerationClips.Length)];

        if (clip == null)
            return;

        GameObject audioObject = new GameObject("EviscerationAudio");
        audioObject.transform.position = transform.position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = eviscerationVolume;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = audioMinDistance;
        source.maxDistance = audioMaxDistance;
        source.playOnAwake = false;

        source.Play();

        float safePitch = Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        Destroy(audioObject, clip.length / safePitch + 0.1f);
    }

    private static void Shuffle(List<int> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            int temp = values[i];
            values[i] = values[randomIndex];
            values[randomIndex] = temp;
        }
    }

    private void OnValidate()
    {
        maxGibsToSpawn = Mathf.Max(1, maxGibsToSpawn);
        minimumBaseImpulse = Mathf.Max(0f, minimumBaseImpulse);
        torqueStrength = Mathf.Max(0f, torqueStrength);
        gibLifetime = Mathf.Max(0.1f, gibLifetime);
        gibLifetimeVariation = Mathf.Max(0f, gibLifetimeVariation);

        if (forceVariation.x > forceVariation.y)
        {
            float temp = forceVariation.x;
            forceVariation.x = forceVariation.y;
            forceVariation.y = temp;
        }

        if (pitchRange.x > pitchRange.y)
        {
            float temp = pitchRange.x;
            pitchRange.x = pitchRange.y;
            pitchRange.y = temp;
        }
    }
}
