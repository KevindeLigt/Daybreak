using UnityEngine;

/// <summary>Cross this trigger after clearing its source region to continue the run in another scene.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class LevelExit : MonoBehaviour
{
    public MapRegion sourceRegion;
    public string destinationScene = "Level_02_Bellsgrave";
    [Min(0f)] public float fadeDuration = 0.3f;

    private void Reset()
    {
        gameObject.layer = 2; // Ignore Raycast: this invisible crossing should not block pellets.
        ConfigureTrigger();
    }
    private void Awake() => ConfigureTrigger();

    private void ConfigureTrigger()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || SceneLoader.IsInputBlocked || sourceRegion == null || !sourceRegion.IsCompleted)
            return;
        // Ignore weapons and extra child hitboxes; the CharacterController is the player's crossing.
        CharacterController controller = other as CharacterController;
        if (controller == null || !controller.enabled) return;
        PlayerHealth player = controller.GetComponentInParent<PlayerHealth>();
        if (player == null || player.currentHealth <= 0f) return;
        if (GameFlowManager.Instance == null || !GameFlowManager.Instance.GameActive ||
            RegionManager.Instance == null || RegionManager.Instance.CurrentRegion != sourceRegion) return;

        SceneLoader.TryContinueRun(destinationScene, player, fadeDuration);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
