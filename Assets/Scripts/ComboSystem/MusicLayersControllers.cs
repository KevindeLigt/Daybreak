using UnityEngine;
using System.Collections.Generic;

public class MusicLayersController : MonoBehaviour
{
    public static MusicLayersController Instance;
    [Header("Base Layer (Always On)")]
    public AudioSource baseLayer;
    [Range(0f, 1f)] public float baseLayerVolume = 1f;
    [Header("Instrument Layers (Fade In With Combo)")]
    public AudioSource drumsLayer;
    [Range(0f, 1f)] public float drumsMaxVolume = 1f;
    public AudioSource rhythmGuitarLayer;
    [Range(0f, 1f)] public float rhythmMaxVolume = 1f;
    public AudioSource leadGuitarLayer;
    [Range(0f, 1f)] public float leadMaxVolume = 1f;
    public AudioSource padsLayer;
    [Range(0f, 1f)] public float padsMaxVolume = 1f;
    [Header("Layer Settings")]
    public float fadeSpeed = 1.5f;
    [Min(.05f)] public float scheduledStartLead = .15f;

    private AudioSource[] layers;
    private int requestedLayers;
    public int RequestedLayers => requestedLayers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        layers = new[] { drumsLayer, rhythmGuitarLayer, leadGuitarLayer, padsLayer };
        var seen = new HashSet<AudioSource>();
        if (baseLayer != null) seen.Add(baseLayer);
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            if (!seen.Add(layers[i]))
            {
                Debug.LogWarning("Use a separate AudioSource for each music stem.", this);
                layers[i] = null;
                continue;
            }
            layers[i].volume = 0f;
        }
    }
    private void Start()
    {
        double start = AudioSettings.dspTime + Mathf.Max(.05f, scheduledStartLead);
        Schedule(baseLayer, Mathf.Clamp01(baseLayerVolume), start);
        foreach (AudioSource layer in layers) Schedule(layer, 0f, start);
        requestedLayers = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.MusicLayerCount : 0;
    }
    private static void Schedule(AudioSource source, float volume, double start)
    {
        if (source == null || source.clip == null) return;
        source.Stop();
        source.volume = volume;
        source.loop = true;
        source.PlayScheduled(start);
    }
    public void SetActiveLayers(int count) => requestedLayers = Mathf.Clamp(count, 0, 4);
    public void ResetToBase() => SetActiveLayers(0);
    private void Update()
    {
        if (baseLayer != null) baseLayer.volume = Mathf.Clamp01(baseLayerVolume);
        if (layers == null) return;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            float maximum = i == 0 ? drumsMaxVolume : i == 1 ? rhythmMaxVolume : i == 2 ? leadMaxVolume : padsMaxVolume;
            float target = i < requestedLayers ? Mathf.Clamp01(maximum) : 0f;
            layers[i].volume = Mathf.MoveTowards(layers[i].volume, target, Mathf.Max(.01f, fadeSpeed) * Time.unscaledDeltaTime);
        }
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
