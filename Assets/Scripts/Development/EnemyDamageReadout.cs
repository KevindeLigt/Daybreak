using TMPro;
using UnityEngine;

/// <summary>
/// Test-scene health display. Attach beside EnemyHealth on the enemy root.
/// Creates its own world-space TMP text at runtime; no Canvas or prefab required.
/// In training mode reads per-target shotgun reports, including overkill,
/// connecting-shot count and simulated defeat/reset.
/// On normal enemies samples net HP lost once per LateUpdate, combining the current shotgun's
/// synchronous pellets. This is not a weapon-damage event or a shot counter:
/// overkill is excluded, multiple hits in a frame merge, and evisceration/lunge
/// crash deaths also count as HP lost. Same-frame healing can offset damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyDamageReadout : MonoBehaviour
{
    [Header("References (optional)")]
    [Tooltip("Leave empty to use Camera.main. Assign the gameplay camera if needed.")]
    public Camera viewingCamera;

    [Tooltip("Leave empty to use the default TextMesh Pro font.")]
    public TMP_FontAsset font;

    [Header("Placement")]
    [Tooltip("World-space offset from the enemy root. Increase Y for taller enemies.")]
    public Vector3 worldOffset = new Vector3(0f, 2.4f, 0f);

    [Min(0.1f)] public float textWidth = 5f;
    [Min(0.1f)] public float healthFontSize = 3f;
    [Min(0.1f)] public float damageFontSize = 4f;

    [Header("Popup")]
    [Min(0.1f)] public float popupDuration = 1.2f;
    [Min(0f)] public float popupRise = 0.6f;
    public Color healthColor = Color.white;
    public Color damageColor = new Color(1f, 0.35f, 0.2f, 1f);
    public Color deadColor = new Color(1f, 0.55f, 0.45f, 1f);

    [Header("Debug")]
    public bool logToConsole = true;

    private EnemyHealth health;
    private GameObject displayRoot;
    private TextMeshPro healthText;
    private TextMeshPro damageText;
    private float previousHealth;
    private float previousMaxHealth;
    private bool previousDead;
    private float popupStartedAt = float.NegativeInfinity;
    private bool initialized;
    private string enemyLabel;
    private int previousTrainingRevision = -1;

    private void Start()
    {
        health = GetComponent<EnemyHealth>();
        TMP_FontAsset selectedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        if (selectedFont == null)
        {
            Debug.LogWarning(
                "EnemyDamageReadout needs a TMP font. Assign Font in the Inspector " +
                "or import TMP Essential Resources.", this);
            enabled = false;
            return;
        }

        enemyLabel = gameObject.name;
        // Start runs after EnemyHealth.Awake has initialized health.
        previousHealth = health.CurrentHealth;
        previousMaxHealth = health.maxHealth;
        previousDead = health.IsDead;

        // Keep generated renderers outside the enemy hierarchy so health flash,
        // evisceration and ragdoll scripts cannot pick them up as body renderers.
        displayRoot = new GameObject(enemyLabel + "_DamageReadout");
        healthText = CreateText("Health", selectedFont, healthFontSize);
        damageText = CreateText("HealthLost", selectedFont, damageFontSize);
        damageText.gameObject.SetActive(false);
        initialized = true;
        RefreshHealthText();
        PositionDisplay();
    }

    private TextMeshPro CreateText(string label, TMP_FontAsset selectedFont, float size)
    {
        GameObject textObject = new GameObject(label);
        textObject.transform.SetParent(displayRoot.transform, false);
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.font = selectedFont;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.1f, textWidth), 1f);
        text.raycastTarget = false;
        return text;
    }

    private void LateUpdate()
    {
        if (!initialized || health == null || displayRoot == null)
            return;

        float current = health.CurrentHealth;
        float lost = previousHealth - current;
        if (health.IsTrainingDummy)
        {
            if (previousTrainingRevision != health.TrainingRevision)
            {
                previousTrainingRevision = health.TrainingRevision;
                RefreshHealthText();
                if (health.TrainingLastShotDamage > 0f)
                {
                    popupStartedAt = Time.unscaledTime;
                    damageText.text = $"{health.TrainingLastShotDamage:0.##} damage";
                    if (logToConsole)
                        Debug.Log($"{enemyLabel}: Damage {health.TrainingLastShotDamage:0.##}" +
                            $" | HP lost {health.TrainingLastHealthLost:0.##}" +
                            $" | HP {current:0.##}/{health.maxHealth:0.##}" +
                            $" | Shots hit {health.TrainingShotsHit}" +
                            (health.TrainingDefeated ? " | KILL (simulated)" : ""), this);
                }
                else
                {
                    popupStartedAt = float.NegativeInfinity;
                }
            }
        }
        else if (lost > 0f)
        {
            popupStartedAt = Time.unscaledTime;
            damageText.text = "-" + lost.ToString("0.##") + " HP";
            if (logToConsole)
            {
                Debug.Log(
                    $"{enemyLabel}: HP lost {lost:0.##} | Health: " +
                    $"{current:0.##} / {health.maxHealth:0.##}" +
                    (health.IsDead ? " | DEAD" : ""), this);
            }
        }

        if (current != previousHealth || health.maxHealth != previousMaxHealth ||
            health.IsDead != previousDead)
        {
            RefreshHealthText();
        }

        previousHealth = current;
        previousMaxHealth = health.maxHealth;
        previousDead = health.IsDead;

        PositionDisplay();
        float age = Time.unscaledTime - popupStartedAt;
        float duration = Mathf.Max(0.1f, popupDuration);
        bool popupVisible = age >= 0f && age < duration;
        damageText.gameObject.SetActive(popupVisible);
        if (popupVisible)
        {
            float progress = Mathf.Clamp01(age / duration);
            damageText.transform.localPosition = Vector3.up *
                ((health.IsTrainingDummy ? 1.2f : 0.65f) + popupRise * progress);
            Color tint = damageColor;
            // Hold full opacity for the first half, then fade out.
            tint.a *= 1f - Mathf.Clamp01((progress - 0.5f) * 2f);
            damageText.color = tint;
        }
    }

    private void RefreshHealthText()
    {
        if (health.IsTrainingDummy)
        {
            string result = health.TrainingDefeated ? "KILL" : "READY";
            if (health.TrainingShotsHit > 0 || health.TrainingHasOtherDamage)
                result = health.TrainingDefeated ? "KILL" : "ALIVE";
            healthText.text = enemyLabel + "\n" +
                $"HP {health.CurrentHealth:0.##} / {health.maxHealth:0.##}\n" +
                $"{result} | Shots hit: {health.TrainingShotsHit}";
            if (health.TrainingLastShotDamage > 0f)
            {
                string label = health.TrainingHasOtherDamage ? "Last damage" : "Last shot";
                healthText.text += $"\n{label}: {health.TrainingLastShotDamage:0.##}" +
                    $" | HP lost: {health.TrainingLastHealthLost:0.##}";
            }
            if (health.TrainingHasOtherDamage)
                healthText.text += "\nOther damage included";
            healthText.color = health.TrainingDefeated ? deadColor : healthColor;
            return;
        }

        healthText.text = enemyLabel + "\n" +
            $"HP {health.CurrentHealth:0.##} / {health.maxHealth:0.##}" +
            (health.IsDead ? "  DEAD" : "");
        healthText.color = health.IsDead ? deadColor : healthColor;
    }

    private void PositionDisplay()
    {
        if (viewingCamera == null)
            viewingCamera = Camera.main;

        // Match camera orientation to keep the text upright and readable.
        displayRoot.transform.position = transform.position + worldOffset;
        if (viewingCamera != null)
            displayRoot.transform.rotation = viewingCamera.transform.rotation;
        displayRoot.SetActive(viewingCamera != null);
    }

    private void OnEnable()
    {
        if (!initialized || health == null)
            return;

        previousHealth = health.CurrentHealth;
        previousMaxHealth = health.maxHealth;
        previousDead = health.IsDead;
        popupStartedAt = float.NegativeInfinity;
        damageText.gameObject.SetActive(false);
        RefreshHealthText();
        PositionDisplay();
    }

    private void OnDisable()
    {
        if (displayRoot != null)
            displayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (displayRoot != null)
            Destroy(displayRoot);
    }
}
