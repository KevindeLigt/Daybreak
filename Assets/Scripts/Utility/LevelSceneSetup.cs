using System.Collections;
using UnityEngine;

/// <summary>One per gameplay scene. Places and restores the player before its encounter can start.</summary>
[DisallowMultipleComponent]
public sealed class LevelSceneSetup : MonoBehaviour
{
    public static LevelSceneSetup Instance { get; private set; }
    [Header("Run Entry")]
    [Tooltip("Scene used by Restart after direct Editor Play. Menu-started runs remember their own first scene.")]
    public string firstLevelScene = "Level_01_Church";
    [Tooltip("Optional: player's feet/root position and facing direction on arrival. Keep outside exit triggers.")]
    public Transform arrivalPoint;
    [Header("Player In This Scene")]
    public PlayerHealth player;
    public WeaponManager weaponManager;
    public bool IsReady { get; private set; }
    public string InitializationError { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        IsReady = false;
    }

    private IEnumerator Start()
    {
        // WeaponManager.Start activates the shotgun; UI/GameFlow Start clear old HUD entries.
        // Restore only after all those initializers have run.
        yield return null;
        if (player == null) player = FindObjectOfType<PlayerHealth>();
        if (weaponManager == null && player != null)
            weaponManager = player.GetComponentInChildren<WeaponManager>(true);
        if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManager>();
        if (!RunState.GetOrCreate().RestoreLevel(gameObject.scene, firstLevelScene, player, weaponManager, out string error))
        {
            InitializationError = error;
            Debug.LogError("LevelSceneSetup: " + error, this);
            yield break;
        }
        if (arrivalPoint != null)
        {
            HunterMovement movement = player.GetComponent<HunterMovement>();
            if (movement != null) movement.PlaceAtLevelEntrance(arrivalPoint.position, arrivalPoint.rotation);
            else
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (wasEnabled) controller.enabled = false;
                player.transform.SetPositionAndRotation(arrivalPoint.position, arrivalPoint.rotation);
                if (wasEnabled) controller.enabled = true;
            }
        }
        Physics.SyncTransforms();
        IsReady = true;
        Debug.Log($"Level ready: {gameObject.scene.name}. Previous run wave: {RunState.Instance.GlobalWave}", this);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
