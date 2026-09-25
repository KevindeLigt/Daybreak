using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Presentation only. Wire the objective's On Heart Deposited event to
/// PlayDeposit. For animation events, place this on the bell Animator's object.
/// This object must remain active after objective completion. Spawned VFX
/// prefabs own their playback and cleanup; reusable scene particles also work.
/// </summary>
[DisallowMultipleComponent]
public class RootHeartDepositFeedback : MonoBehaviour
{
    public enum ImpactTiming { Immediately, AfterDelay, AnimationEvent }

    [Header("References")]
    [SerializeField] private CurseObjectiveController objective;
    [SerializeField] private Animator bellAnimator;
    [SerializeField] private AudioSource audioSource;

    [Header("Every Deposit")]
    [Tooltip("An Animator Trigger parameter. Leave empty for particles and sound only.")]
    [SerializeField] private string depositTrigger = "Deposit";
    [Tooltip("Optional reusable scene Particle System. For a self-playing, self-destroying prefab, use Deposit VFX Prefab below instead.")]
    [SerializeField] private ParticleSystem depositParticles;
    [SerializeField] private AudioClip depositSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Final Deposit (optional overrides)")]
    [Tooltip("Empty fields reuse the ordinary deposit settings. A final deposit plays one sequence, not both.")]
    [SerializeField] private string finalDepositTrigger = "";
    [Tooltip("Optional reusable scene Particle System for the final deposit. For a spawned effect, use Final Deposit VFX Prefab below instead.")]
    [SerializeField] private ParticleSystem finalDepositParticles;
    [SerializeField] private AudioClip finalDepositSound;

    [Header("Spawned VFX Prefabs (optional)")]
    [Tooltip("Drag the whole prefab from the Project window. A fresh copy spawns at each ordinary impact. The prefab controls Play On Awake and its own destruction. Overrides Deposit Particles.")]
    [SerializeField] private GameObject depositVfxPrefab;
    [Tooltip("Optional final-heart prefab. Overrides Final Deposit Particles. Leave both final VFX fields empty to reuse the ordinary effect.")]
    [SerializeField] private GameObject finalDepositVfxPrefab;
    [Tooltip("World position and rotation for spawned effects. Empty uses this bell object's transform. Copies spawn at the scene root and keep the prefab's scale.")]
    [SerializeField] private Transform vfxSpawnPoint;

    [Header("Particles And Sound Timing")]
    [SerializeField] private ImpactTiming impactTiming = ImpactTiming.Immediately;
    [SerializeField, Min(0f)] private float impactDelay = 0.25f;
    [Tooltip("In Animation Event mode, add PlayImpact to the clip at the bell strike. If it never arrives, this timeout plays the effects once and warns. Set longer than the time to the strike.")]
    [SerializeField, Min(0.1f)] private float missingEventTimeout = 3f;

    [Header("Optional Extra Effects")]
    [Tooltip("Runs with every impact. Can call VisualEffect.Play, a light pulse, or another presentation script.")]
    public UnityEvent onImpact = new UnityEvent();
    [Tooltip("Additional effects for the final deposit only. Keep progression on CurseObjectiveController.")]
    public UnityEvent onFinalImpact = new UnityEvent();

    private CurseObjectiveController lastObjective;
    private int lastDepositCount = -1;
    private bool impactPending;
    private bool pendingFinalDeposit;
    private bool waitingForAnimationEvent;
    private float impactDeadline;

    private void Reset()
    {
        bellAnimator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (bellAnimator == null) bellAnimator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    /// <summary>Connect only to the objective's successful On Heart Deposited event.</summary>
    public void PlayDeposit()
    {
        if (!isActiveAndEnabled) return;
        if (objective == null)
        {
            Debug.LogWarning($"{name}: Assign the local Curse Objective to RootHeartDepositFeedback.", this);
            return;
        }

        int deposited = objective.HeartsDeposited;
        if (deposited <= 0 ||
            (lastObjective == objective && lastDepositCount == deposited)) return;

        // Duplicate Inspector listeners cannot play a deposit twice. Record it
        // before invoking any extra effects, which may themselves call scripts.
        lastObjective = objective;
        lastDepositCount = deposited;

        // Normally deposits are separated by an entire wave. If another arrives
        // while a previous effect is pending, finish that effect before replacing it.
        if (impactPending) EmitImpact();
        if (!isActiveAndEnabled) return;

        // The current objective completes before its deposit event is invoked.
        pendingFinalDeposit = objective.IsCompleted;
        impactPending = true;
        bool animationStarted = StartBellAnimation(pendingFinalDeposit);
        waitingForAnimationEvent = impactTiming == ImpactTiming.AnimationEvent;

        if (impactTiming == ImpactTiming.Immediately)
        {
            EmitImpact();
        }
        else if (waitingForAnimationEvent && !animationStarted)
        {
            Debug.LogWarning($"{name}: No bell animation could start. Playing deposit effects immediately.", this);
            EmitImpact();
        }
        else
        {
            impactDeadline = Time.unscaledTime + (waitingForAnimationEvent
                ? Mathf.Max(0.1f, missingEventTimeout) : Mathf.Max(0f, impactDelay));
        }
    }

    private void Update()
    {
        if (!impactPending || Time.unscaledTime < impactDeadline) return;
        if (waitingForAnimationEvent)
            Debug.LogWarning($"{name}: The bell animation did not send PlayImpact in time. " +
                "Check the clip event and timeout. Playing the effects once as a fallback.", this);
        EmitImpact();
    }

    /// <summary>
    /// Animation Event receiver. Also safe if a clip contains duplicate events:
    /// an accepted deposit has only one particles/sound impact.
    /// </summary>
    public void PlayImpact()
    {
        // Events left on a clip cannot override Immediately/After Delay timing.
        if (waitingForAnimationEvent) EmitImpact();
    }

    private void EmitImpact()
    {
        if (!isActiveAndEnabled || !impactPending) return;
        impactPending = false;
        waitingForAnimationEvent = false;
        bool final = pendingFinalDeposit;

        AudioClip sound = final && finalDepositSound != null
            ? finalDepositSound : depositSound;

        PlayDepositVfx(final);
        if (sound != null)
        {
            if (audioSource != null && audioSource.isActiveAndEnabled)
                audioSource.PlayOneShot(sound, Mathf.Clamp01(soundVolume));
            else Debug.LogWarning($"{name}: Assign an active AudioSource for the bell sound.", this);
        }

        onImpact?.Invoke();
        if (final) onFinalImpact?.Invoke();
    }

    private void PlayDepositVfx(bool final)
    {
        // Pick exactly one effect. A final-specific effect wins over the ordinary
        // one; within either choice, an explicit prefab wins over scene particles.
        if (final && finalDepositVfxPrefab != null)
            SpawnVfxPrefab(finalDepositVfxPrefab);
        else if (final && finalDepositParticles != null)
            PlayParticlesOrLegacyPrefab(finalDepositParticles);
        else if (depositVfxPrefab != null)
            SpawnVfxPrefab(depositVfxPrefab);
        else if (depositParticles != null)
            PlayParticlesOrLegacyPrefab(depositParticles);
    }

    private void SpawnVfxPrefab(GameObject prefab)
    {
        Transform origin = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
        // No parent: a later bell/region visual switch cannot hide the burst.
        // Do not Stop/Clear this clone: Stop Action = Destroy may delete it.
        // Play On Awake / OnEnable and self-destruction belong to the VFX prefab.
        GameObject instance = Instantiate(prefab, origin.position, origin.rotation);
        if (!instance.activeSelf) instance.SetActive(true);
    }

    private void PlayParticlesOrLegacyPrefab(ParticleSystem particles)
    {
        // Preserve assignments from the first script. An asset dragged into an
        // old Particle System field is now spawned instead of played as a scene object.
        if (!particles.gameObject.scene.IsValid())
        {
            SpawnVfxPrefab(particles.transform.root.gameObject);
            return;
        }

        if (particles.gameObject.activeInHierarchy)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }
        else Debug.LogWarning($"{name}: Deposit particles are on an inactive object. Keep reusable scene particles active, or assign a prefab in the VFX Prefab field.", this);
    }

    private bool StartBellAnimation(bool final)
    {
        string trigger = final && !string.IsNullOrEmpty(finalDepositTrigger)
            ? finalDepositTrigger : depositTrigger;
        if (string.IsNullOrEmpty(trigger)) return false;
        if (bellAnimator == null || !bellAnimator.isActiveAndEnabled ||
            bellAnimator.runtimeAnimatorController == null) return false;

        if (!HasTrigger(trigger))
        {
            Debug.LogWarning($"{name}: Bell Animator has no Trigger named '{trigger}'. Check the parameter name and type.", this);
            if (!final || trigger == depositTrigger || !HasTrigger(depositTrigger)) return false;
            trigger = depositTrigger;
        }

        // Clear the other deposit trigger so a previously queued transition
        // cannot play an ordinary deposit after the final one.
        string otherTrigger = trigger == depositTrigger ? finalDepositTrigger : depositTrigger;
        if (HasTrigger(otherTrigger)) bellAnimator.ResetTrigger(Animator.StringToHash(otherTrigger));
        int hash = Animator.StringToHash(trigger);
        bellAnimator.ResetTrigger(hash);
        bellAnimator.SetTrigger(hash);
        return true;
    }

    private bool HasTrigger(string trigger)
    {
        if (string.IsNullOrEmpty(trigger) || bellAnimator == null) return false;
        foreach (AnimatorControllerParameter parameter in bellAnimator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger)
                return true;
        return false;
    }

    private void OnDisable()
    {
        // Do not fire an old delayed effect when a region is enabled again.
        impactPending = false;
        waitingForAnimationEvent = false;
    }
}
