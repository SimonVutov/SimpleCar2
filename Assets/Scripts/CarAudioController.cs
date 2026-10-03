using UnityEngine;

public class CarAudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource engineSource;
    public AudioSource engineRevSource;
    public AudioSource throttleSource;
    public AudioSource tireSquealSource;
    public AudioSource windSource;
    public AudioSource gearShiftSource;
    
    [Header("Audio Clips")]
    public AudioClip engineIdleClip;
    public AudioClip engineRevClip;
    public AudioClip throttleClip;
    public AudioClip tireSquealClip;
    public AudioClip windClip;
    public AudioClip gearShiftClip;
    
    [Header("Engine Settings")]
    public float minEngineRPM = 800f;
    public float maxEngineRPM = 8500f;  // Match car engine maxRPM
    public float minEnginePitch = 0.5f;
    public float maxEnginePitch = 2.0f;
    public float engineVolumeMultiplier = 1.0f;
    public float baseEngineVolume = 0.4f;  // Base volume at idle
    public float maxEngineVolume = 0.8f;   // Max volume at redline
    
    [Header("Engine Response Curves")]
    [Tooltip("Controls how pitch changes with RPM. X = RPM (0-1), Y = Pitch response (0-1)")]
    public AnimationCurve enginePitchCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); // Custom pitch response curve
    [Tooltip("Controls how volume changes with RPM. X = RPM (0-1), Y = Volume response (0-1)")]
    public AnimationCurve engineVolumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Custom volume response curve
    
    [Header("Engine Crossfade Settings")]
    public float crossfadeStartRPM = 0.3f; // Start blending at 30% of max RPM (normalized)
    public AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Smooth transition curve
    
    [Header("Throttle Settings")]
    public float throttleVolumeMultiplier = 0.7f;
    public float throttlePitchMultiplier = 1.2f;
    
    [Header("Tire Squeal Settings")]
    public float squealThreshold = 0.5f; // How much slip needed to trigger squeal
    public float maxSquealVolume = 0.8f;
    
    [Header("Wind Settings")]
    public float windStartSpeed = 10f; // Speed when wind noise starts
    public float maxWindSpeed = 100f;
    public float maxWindVolume = 0.6f;
    
    [Header("Input Variables - Set these from your car controller")]
    public float currentRPM;
    public float throttleInput; // 0 to 1
    public float currentSpeed; // In units per second
    public float lateralSlip; // For tire squeal (0 to 1)
    public bool isShifting; // Trigger for gear shift sound
    
    private float targetEngineVolume;
    private float targetEnginePitch;
    private bool wasShifting;
    
    void Start()
    {
        SetupAudioSources();
    }
    
    void SetupAudioSources()
    {
        SetupSource(engineSource, engineIdleClip, true, baseEngineVolume, 1f, true);
        SetupSource(engineRevSource, engineRevClip, true, 0f, 1f, true);
        SetupSource(throttleSource, throttleClip, true, 0f, 1f, false);
        SetupSource(tireSquealSource, tireSquealClip, true, 0f, 0.8f, false);
        SetupSource(windSource, windClip, true, 0f, 0.7f, false);
        SetupSource(gearShiftSource, gearShiftClip, false, 0.8f, 1f, false);
    }

    static void SetupSource(AudioSource source, AudioClip clip, bool loop, float volume, float pitch, bool play)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.loop = loop;
        source.volume = volume;
        source.pitch = pitch;
        if (play) source.Play();
    }

    void Update()
    {
        UpdateEngineSound();
        UpdateThrottleSound();
        UpdateTireSquealSound();
        UpdateWindSound();
        UpdateGearShiftSound();
    }
    
    void UpdateEngineSound()
    {
        if (engineSource == null || engineSource.clip == null) return;
        
        // Ensure currentRPM is within valid range
        currentRPM = Mathf.Clamp(currentRPM, minEngineRPM, maxEngineRPM);
        
        // Calculate engine pitch based on RPM
        float rpmNormalized = Mathf.Clamp01((currentRPM - minEngineRPM) / Mathf.Max(1f, maxEngineRPM - minEngineRPM));
        float pitchCurveValue = enginePitchCurve.Evaluate(rpmNormalized);
        targetEnginePitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, pitchCurveValue);
        
        // Calculate base engine volume
        float volumeCurveValue = engineVolumeCurve.Evaluate(rpmNormalized);
        float baseVolume = Mathf.Lerp(baseEngineVolume, maxEngineVolume, volumeCurveValue) * engineVolumeMultiplier;
        
        // Add throttle influence to volume
        float throttleVolumeBoost = throttleInput * 0.2f;
        baseVolume += throttleVolumeBoost;
        baseVolume = Mathf.Clamp(baseVolume, 0f, 1f);
        
        // Calculate crossfade blend factor (0 = idle only, 1 = rev only)
        float blendFactor = engineRevSource == null || engineRevSource.clip == null ? 0f : Mathf.Clamp01((rpmNormalized - crossfadeStartRPM) / Mathf.Max(0.001f, 1f - crossfadeStartRPM));
        
        // Apply smooth crossfade curve for more natural transition
        blendFactor = crossfadeCurve.Evaluate(blendFactor);
        
        // Calculate volumes for each source
        float idleVolume = baseVolume * (1f - blendFactor);
        float revVolume = baseVolume * blendFactor;
        
        // Update idle source
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetEnginePitch, Time.deltaTime * 8f);
        engineSource.volume = Mathf.Lerp(engineSource.volume, idleVolume, Time.deltaTime * 4f);
        
        // Update rev source (if available)
        if (engineRevSource != null)
        {
            engineRevSource.pitch = Mathf.Lerp(engineRevSource.pitch, targetEnginePitch, Time.deltaTime * 8f);
            engineRevSource.volume = Mathf.Lerp(engineRevSource.volume, revVolume, Time.deltaTime * 4f);
            
            // Ensure rev source is playing
            if (!engineRevSource.isPlaying && engineRevClip != null)
            {
                engineRevSource.Play();
            }
        }
        
        // Ensure idle source is always playing
        if (!engineSource.isPlaying)
        {
            engineSource.Play();
        }
    }
    
    void UpdateThrottleSound()
    {
        if (throttleSource == null || throttleSource.clip == null) return;
        
        // Play throttle sound when accelerating
        if (throttleInput > 0.1f)
        {
            if (!throttleSource.isPlaying)
                throttleSource.Play();
                
            throttleSource.volume = throttleInput * throttleVolumeMultiplier;
            throttleSource.pitch = 1.0f + (throttleInput * throttlePitchMultiplier);
        }
        else
        {
            if (throttleSource.isPlaying)
                throttleSource.Stop();
        }
    }
    
    void UpdateTireSquealSound()
    {
        if (tireSquealSource == null || tireSquealSource.clip == null) return;
        
        // Play tire squeal based on lateral slip
        if (lateralSlip > squealThreshold)
        {
            if (!tireSquealSource.isPlaying)
                tireSquealSource.Play();
                
            float squealIntensity = Mathf.Clamp01((lateralSlip - squealThreshold) / Mathf.Max(0.001f, 1f - squealThreshold));
            tireSquealSource.volume = squealIntensity * maxSquealVolume;
            tireSquealSource.pitch = 0.8f + (squealIntensity * 0.4f);
        }
        else
        {
            if (tireSquealSource.isPlaying)
                tireSquealSource.Stop();
        }
    }
    
    void UpdateWindSound()
    {
        if (windSource == null || windSource.clip == null) return;
        
        // Play wind sound based on speed
        if (currentSpeed > windStartSpeed)
        {
            if (!windSource.isPlaying)
                windSource.Play();
                
            float windIntensity = Mathf.Clamp01((currentSpeed - windStartSpeed) / Mathf.Max(0.001f, maxWindSpeed - windStartSpeed));
            windSource.volume = windIntensity * maxWindVolume;
            windSource.pitch = 0.7f + (windIntensity * 0.6f);
        }
        else
        {
            if (windSource.isPlaying)
                windSource.Stop();
        }
    }
    
    void UpdateGearShiftSound()
    {
        if (gearShiftSource == null || gearShiftSource.clip == null) return;
        
        // Play gear shift sound when shifting
        if (isShifting && !wasShifting)
        {
            gearShiftSource.Play();
        }
        
        wasShifting = isShifting;
    }
    
    // Call this method from your car controller to update audio values
    public void UpdateAudioValues(float rpm, float throttle, float speed, float slip, bool shifting)
    {
        // Add NaN checks for audio values
        if (float.IsNaN(rpm) || float.IsInfinity(rpm)) rpm = minEngineRPM;
        if (float.IsNaN(throttle) || float.IsInfinity(throttle)) throttle = 0f;
        if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0f;
        if (float.IsNaN(slip) || float.IsInfinity(slip)) slip = 0f;
        
        currentRPM = rpm;
        throttleInput = Mathf.Clamp01(throttle);
        currentSpeed = Mathf.Max(0f, speed);
        lateralSlip = Mathf.Max(0f, slip);
        isShifting = shifting;
        
    }
}
