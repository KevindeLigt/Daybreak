using UnityEngine;
using System.Collections;

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

    private AudioSource[] layers;
    private float[] maxVolumes;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        layers = new AudioSource[]
        {
            drumsLayer, rhythmGuitarLayer, leadGuitarLayer, padsLayer
        };

        maxVolumes = new float[]
        {
            drumsMaxVolume, rhythmMaxVolume, leadMaxVolume, padsMaxVolume
        };

        StartAllLayersSynced();
    }

    void StartAllLayersSynced()
    {
        // Base layer first
        baseLayer.volume = baseLayerVolume;
        baseLayer.Play();

        // Start all other layers muted but synced
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;

            layers[i].Stop();
            layers[i].volume = 0f;
            layers[i].Play();
        }
    }

    public void SetActiveLayers(int layerCount)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;

            float targetVolume = (i < layerCount) ? maxVolumes[i] : 0f;
            StartCoroutine(FadeLayer(layers[i], targetVolume));
        }
    }

    public void ResetToBase()
    {
        foreach (var layer in layers)
        {
            if (layer == null) continue;
            StartCoroutine(FadeLayer(layer, 0f));
        }
    }

    IEnumerator FadeLayer(AudioSource src, float target)
    {
        while (!Mathf.Approximately(src.volume, target))
        {
            src.volume = Mathf.MoveTowards(src.volume, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
