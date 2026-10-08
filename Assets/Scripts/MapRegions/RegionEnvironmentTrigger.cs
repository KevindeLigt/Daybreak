using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides a completed region's scenery after the player crosses the exit.
/// Local +Z points away from the region. Entering the trigger restores the
/// scenery before a returning player can step onto its geometry.
/// This never changes region, objective, or wave progress.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class RegionEnvironmentTrigger : MonoBehaviour
{
    [Header("Completed Region")]
    [Tooltip("The region whose scenery should hide. For the first exit, assign Region_Church.")]
    [SerializeField] private MapRegion completedRegion;

    [Header("Environment To Hide")]
    [Tooltip("Dedicated scenery roots only. Keep the player, managers, region objects, shared navigation, and exit walkway outside these roots.")]
    [SerializeField] private GameObject[] environmentRoots = new GameObject[0];

    private readonly List<RootState> savedStates = new List<RootState>();

    public bool IsEnvironmentHidden => savedStates.Count > 0;

    private struct RootState
    {
        public GameObject root;
        public bool wasActive;

        public RootState(GameObject root)
        {
            this.root = root;
            wasActive = root.activeSelf;
        }
    }

    private void Reset()
    {
        ConfigurePhysics();
        gameObject.layer = 2; // Unity's built-in Ignore Raycast layer.

        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(6f, 3f, 3f);
    }

    private void Awake()
    {
        ConfigurePhysics();

        if (completedRegion == null || environmentRoots == null || environmentRoots.Length == 0)
        {
            Debug.LogWarning("RegionEnvironmentTrigger needs a Completed Region and at least one Environment Root.", this);
        }
    }

    private void ConfigurePhysics()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || GetPlayerController(other) == null)
            return;

        // Restore on entry, rather than after crossing back onto hidden flooring.
        RestoreEnvironment();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isActiveAndEnabled)
            return;

        CharacterController player = GetPlayerController(other);
        if (player == null)
            return;

        float exitSide = Vector3.Dot(player.transform.position - transform.position, transform.forward);
        if (exitSide <= 0f)
        {
            RestoreEnvironment();
            return;
        }

        RegionManager manager = RegionManager.Instance;
        if (completedRegion == null || !completedRegion.IsCompleted || manager == null ||
            manager.CurrentRegion == null || manager.CurrentRegion == completedRegion)
        {
            return;
        }

        // A different combat region must already be active. This also works
        // after progressing beyond the immediately adjacent region.
        HideEnvironment();
    }

    private static CharacterController GetPlayerController(Collider other)
    {
        // Ignore zombie colliders, pickups, and extra colliders on the weapon.
        CharacterController controller = other as CharacterController;
        if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            return null;

        return controller.GetComponentInParent<PlayerHealth>() != null ? controller : null;
    }

    private void HideEnvironment()
    {
        if (IsEnvironmentHidden || environmentRoots == null)
            return;

        HashSet<GameObject> captured = new HashSet<GameObject>();
        foreach (GameObject root in environmentRoots)
        {
            if (root == null || !captured.Add(root))
                continue;

            if (!IsSafeEnvironmentRoot(root))
            {
                Debug.LogWarning($"RegionEnvironmentTrigger skipped '{root.name}': use a scenery-only root outside the player, managers, region objects, and trigger.", this);
                continue;
            }

            // Capture every state before changing any parent. Inactive roots
            // stay inactive when restored; completed curse effects stay off.
            savedStates.Add(new RootState(root));
        }

        foreach (RootState state in savedStates)
        {
            if (state.root != null)
                state.root.SetActive(false);
        }
    }

    private bool IsSafeEnvironmentRoot(GameObject root)
    {
        return !transform.IsChildOf(root.transform) &&
            root.GetComponentInChildren<PlayerHealth>(true) == null &&
            root.GetComponentInChildren<RegionManager>(true) == null &&
            root.GetComponentInChildren<MapRegion>(true) == null &&
            root.GetComponentInChildren<GameFlowManager>(true) == null;
    }

    private void RestoreEnvironment()
    {
        foreach (RootState state in savedStates)
        {
            if (state.root != null)
                state.root.SetActive(state.wasActive);
        }

        savedStates.Clear();
    }

    private void OnDisable()
    {
        // Turning off/removing this helper must not strand the scene hidden.
        RestoreEnvironment();
    }
}
