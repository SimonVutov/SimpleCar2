using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class Engine
{
    public bool targetRPM = false;
    public float currentTarget = 0f;
    public bool forceAdjacentGears = false; // Force transmission to only shift to adjacent gears
    public bool constantTorque = false; // Constant torque, e.g. for electric motors or vehicles
    public float idleRPM = 800f; // Koenigsegg idle RPM
    public float maxRPM = 8500f; // Koenigsegg redline (Jesko/modern engines)
    public float peakPowerRPM = 7800f; // Where peak power occurs
    public float[] gearRatios = { 3.31f, 2.27f, 1.69f, 1.32f, 1.02f, 0.82f, 0.67f, 0.56f, 0.46f }; // 9-speed LST ratios (estimated)
    public float finalDriveRatio = 3.73f; // Estimated for Koenigsegg
    private int currentGear = 0;
    private int previousGear = -1; // Track previous gear to prevent rapid switching back
    private float lastGearChangeTime = 0f; // Time when last gear change occurred
    private float gearCooldown = 1f; // 1 second cooldown before switching back to previous gear
    public bool automaticTransmission = true;
    private bool switchingGears = false;
    private float gearChangeTime = 0.09f; // Light Speed Transmission is incredibly fast, 0.02 default
    private float rpm = 0f;
    private bool engineInitialized = false;
    public void SetRPM(float averageWheelAngularVelocity)
    {
        // Initialize engine at idle RPM on first call
        if (!engineInitialized)
        {
            this.rpm = idleRPM;
            engineInitialized = true;
            return;
        }
        
        // Add NaN checks to prevent engine RPM issues
        if (float.IsNaN(averageWheelAngularVelocity) || float.IsInfinity(averageWheelAngularVelocity))
            averageWheelAngularVelocity = 0f;
            
        float averageWheelRPM = (Mathf.Abs(averageWheelAngularVelocity) * 60f) / (2f * Mathf.PI);
        float totalRatio = Math.Abs(gearRatios[currentGear] * finalDriveRatio);
        float transmissionRPM = averageWheelRPM * totalRatio;
        float targetRPM = Mathf.Max(idleRPM, transmissionRPM);
        this.rpm = Mathf.Clamp(targetRPM, idleRPM, maxRPM);
        
        // Additional NaN check for final RPM value
        if (float.IsNaN(this.rpm) || float.IsInfinity(this.rpm))
            this.rpm = idleRPM;
    }
    // Enhanced power curve for Koenigsegg engine
    public float GetCurrentPower(MonoBehaviour context) // 0-1 based on RPM
    {
        if (constantTorque) return 1;
        if (rpm >= maxRPM) return 0f; // No power if RPM exceeds max
        if (switchingGears) return 0.7f; // Less power reduction due to fast shifts
        
        // Realistic power curve - peak power at 7800 RPM
        float normalizedRPM = rpm / maxRPM;
        
        if (rpm < idleRPM) return 0f;
        if (rpm < peakPowerRPM)
        {
            // Power builds up to peak
            float t = (rpm - idleRPM) / (peakPowerRPM - idleRPM);
            return Mathf.Lerp(0.3f, 1.0f, t * t); // Quadratic curve for realistic torque
        }
        else
        {
            // Power drops off after peak
            float t = (rpm - peakPowerRPM) / (maxRPM - peakPowerRPM);
            return Mathf.Lerp(1.0f, 0.1f, t);
        }
    }
    public float AngularVelocityToRPM(float angularVelocity)
    {
        return angularVelocity * 60f / (2f * Mathf.PI);
    }

    public void UpGear(MonoBehaviour context)
    {
        if (currentGear < gearRatios.Length - 1 && !switchingGears)
        {
            previousGear = currentGear;
            currentGear++;
            lastGearChangeTime = Time.time;
            switchingGears = true;
            // Start coroutine to reset switchingGears after 0.4 seconds
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    public void DownGear(MonoBehaviour context)
    {
        if (currentGear > 0 && !switchingGears)
        {
            previousGear = currentGear;
            currentGear--;
            lastGearChangeTime = Time.time;
            switchingGears = true;
            // Start coroutine to reset switchingGears after 0.4 seconds
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    private System.Collections.IEnumerator ResetSwitchingGearsCoroutine()
    {
        yield return new WaitForSeconds(gearChangeTime);
        switchingGears = false;
    }

    public int getCurrentGear()
    {
        return currentGear + 1; // Return 1-based gear number
    }

    // Enhanced gear shifting logic for Koenigsegg
    public void checkGearSwitching(MonoBehaviour context, float throttle)
    {
        if (!automaticTransmission || switchingGears) return;

        // Safety override: Always try to shift up if RPM is too high (prevent rev limiter)
        if (rpm > maxRPM * 0.91f && currentGear < gearRatios.Length - 1)
        {
            UpGear(context);
            return;
        }

        if (!targetRPM) {
            currentTarget = 0.7f * maxRPM;
        }
        float tolerance = 0.2f * maxRPM;

        if (targetRPM) {
            float newTarget = Mathf.Clamp(maxRPM * throttle, idleRPM, maxRPM*0.8f);
            if (newTarget >= currentTarget) currentTarget = newTarget;
            else currentTarget = Mathf.Lerp(currentTarget, newTarget, 0.002f);
        }

        if (forceAdjacentGears)
        {
            // Find optimal gear but only shift one gear at a time towards it
            int optimalGear = FindOptimalGear(currentTarget);
            
            // Always shift towards optimal gear if it's different
            if (optimalGear > currentGear && currentGear < gearRatios.Length - 1)
            {
                // Check if we're trying to switch back to previous gear within cooldown
                int targetGear = currentGear + 1;
                if (targetGear != previousGear || Time.time - lastGearChangeTime > gearCooldown)
                {
                    // Optimal gear is higher, shift up one gear
                    UpGear(context);
                }
            }
            else if (optimalGear < currentGear && currentGear > 0)
            {
                // Check if we're trying to switch back to previous gear within cooldown
                int targetGear = currentGear - 1;
                if (targetGear != previousGear || Time.time - lastGearChangeTime > gearCooldown)
                {
                    // Optimal gear is lower, shift down one gear
                    DownGear(context);
                }
            }
        }
        else
        {
            // Find the optimal gear that gets closest to target RPM
            int optimalGear = FindOptimalGear(currentTarget);
            
            // Only shift if we're not already in the optimal gear and outside tolerance
            if (optimalGear != currentGear && (rpm > currentTarget + tolerance || rpm < currentTarget - tolerance))
            {
                // Check if we're trying to switch back to previous gear within cooldown
                if (optimalGear != previousGear || Time.time - lastGearChangeTime > gearCooldown)
                {
                    previousGear = currentGear;
                    currentGear = optimalGear;
                    lastGearChangeTime = Time.time;
                    switchingGears = true;
                    context.StartCoroutine(ResetSwitchingGearsCoroutine());
                }
            }
        }
    }

    private int FindOptimalGear(float targetRPM)
    {
        // Get current wheel angular velocity to calculate RPM in different gears
        float currentWheelRPM = rpm / (Math.Abs(gearRatios[currentGear] * finalDriveRatio));
        
        int bestGear = currentGear;
        float bestDifference = float.MaxValue;
        
        // Check each gear to find which one gets closest to target RPM
        for (int gear = 0; gear < gearRatios.Length; gear++)
        {
            float totalRatio = Math.Abs(gearRatios[gear] * finalDriveRatio);
            float projectedRPM = Mathf.Max(idleRPM, currentWheelRPM * totalRatio);
            
            // Clamp to engine limits
            projectedRPM = Mathf.Clamp(projectedRPM, idleRPM, maxRPM);
            
            float difference = Math.Abs(projectedRPM - targetRPM);
            
            // Prefer this gear if it's closer to target RPM
            if (difference < bestDifference)
            {
                bestDifference = difference;
                bestGear = gear;
            }
            // If differences are very close, prefer higher gear for efficiency
            else if (Math.Abs(difference - bestDifference) < 50f && gear > bestGear)
            {
                bestGear = gear;
            }
        }
        
        return bestGear;
    }

    public float getRPM()
    {
        return rpm;
    }
    public bool isSwitchingGears()
    {
        return switchingGears;
    }

    public float GetCurrentTotalGearRatio()
    {
        return gearRatios[currentGear] * finalDriveRatio;
    }
}

[Serializable]
public class WheelProperties
{
    [HideInInspector] public TrailRenderer skidTrail;
    [HideInInspector] public GameObject skidTrailGameObject;

    public Vector3 localPosition;
    public float turnAngle = 30f;
    public float suspensionLength = 0.5f;

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    public float engineTorque = 40f;
    public float brakeStrength = 0.5f;
    public bool slidding = false;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float angularVelocity;
    [HideInInspector] public float slip;
    [HideInInspector] public Vector2 input = Vector2.zero;
    [HideInInspector] public float braking = 0;
    [HideInInspector] public float slipHistory = 0f;
    [HideInInspector] public float tcsReduction = 0f; // Traction control reduction factor
    [HideInInspector] public float steeringReduction = 0f; // Steering control reduction factor
    [HideInInspector] public float xSlipAngle = 0f; // Slip in X direction in degrees (5 degrees for example when slightly slipping)
}

[RequireComponent(typeof(Rigidbody))]
public class Car : MonoBehaviour
{
    public bool motorCycleControl = false;
    public float motorcycleTiltDamping = 2f; // Damping factor for motorcycle tilt oscillation
    public float motorcycleYawDamping = 1f; // Damping factor for motorcycle yaw oscillation
    public float restoreStrength = 1f; // Strength of restoring force when sliding
    public float restoreStrengthY = 1f; // Strength of restoring force when sliding
    public float steerAssistTarget = 0.75f; // Target slip ratio for steering assist
    public float coefFrictionMultiplier = 1.0f; // Multiplier for friction coefficient
    public Vector3 centerOfDownforce = new Vector3(0, 0, 0);
    
    [Header("Audio")]
    public CarAudioController audioController;
    
    [Header("Aerodynamics")]
    public float dragCoefficient = 0.278f; // Jesko Absolut value
    public float frontalArea = 1.88f; // m² - Jesko Absolut frontal area
    public float airDensity = 1.225f; // kg/m³ at sea level, 15°C
    public float lowSpeedDragCoefficient = 0.37f; // Higher drag at low speeds
    public float rollingResistanceCoeff = 0.015f; // Typical for performance tires
    public GameObject adaptiveBrakingWing;
    public float brakingWingAngle = 60f; // Degrees to tip forward when braking
    public float brakingWingSpeed = 8f; // Speed of wing animation
    [HideInInspector] public float currentWingAngle = 0f;
    public InputActions input;
    public Engine e = new Engine();
    public GameObject skidMarkPrefab;
    public float smoothTurn = 0.03f;
    float coefStaticFriction = 0.95f;
    float coefKineticFriction = 0.35f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float wheelGripX = 8f;
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;
    public float dampAmount = 2.5f;
    public float suspensionForceClamp = 200f;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public bool forwards = true;

    // Assists
    public bool steeringAssist = true;
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    [HideInInspector] public Vector2 userInput = Vector2.zero;
    public float downforce = 0.16f;
    [HideInInspector] public float isBraking = 0f;

    public Vector3 COMOffset = new Vector3(0, -0.2f, 0);
    public float Inertia = 1.2f; // Multiplier for inertia tensor
    public Vector2 RawInput = Vector2.zero;
    private InputAction move;
    private InputAction Throttle;
    private InputAction Hand; // handbrake
    private InputAction Steer;
    public float carSpeedFactor = 0.03f;
    float handbrakeInput = 0f;

    void Start()
    {
        if (e == null || wheels == null || wheels.Length == 0 || wheelPrefab == null || wheelPrefab.transform.childCount == 0)
        {
            Debug.LogError("Car requires an engine, wheels, and a wheel prefab with a visual child.", this);
            enabled = false;
            return;
        }
        if (Array.Exists(wheels, w => w == null || w.mass <= 0f || w.size <= 0f) ||
            e.gearRatios == null || e.gearRatios.Length == 0 || Array.Exists(e.gearRatios, ratio => ratio <= 0f) ||
            e.finalDriveRatio <= 0f || e.idleRPM < 0f || e.maxRPM <= e.idleRPM)
        {
            Debug.LogError("Car has invalid wheel dimensions or engine ratios/RPM limits.", this);
            enabled = false;
            return;
        }
        e.peakPowerRPM = Mathf.Clamp(e.peakPowerRPM, e.idleRPM + (e.maxRPM - e.idleRPM) * 0.01f, e.maxRPM - (e.maxRPM - e.idleRPM) * 0.01f);
        if (skidMarkPrefab != null && skidMarkPrefab.GetComponent<TrailRenderer>() == null)
            skidMarkPrefab = null;
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        foreach (var w in wheels)
        {
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
            w.wheelCircumference = 2f * Mathf.PI * w.size;

            if (skidMarkPrefab != null)
            {
                w.skidTrailGameObject = Instantiate(skidMarkPrefab, w.wheelObject.transform);
                w.skidTrailGameObject.transform.localPosition = Vector3.zero;
                w.skidTrailGameObject.transform.localRotation = Quaternion.identity;
                w.skidTrailGameObject.transform.parent = null;
                
                w.skidTrail = w.skidTrailGameObject.GetComponent<TrailRenderer>();
                if (w.skidTrail != null)
                    w.skidTrail.emitting = false;
            }
        }

        foreach (var w in wheels)
        {
            w.tcsReduction = 0f;
            w.slipHistory = 0f;
            w.steeringReduction = 0f;
        }

        rb.centerOfMass += COMOffset;
        rb.inertiaTensor *= Inertia;
        
        // Initialize engine at idle RPM
        e.SetRPM(0f);
    }

    void Awake()
    {
        input = new InputActions();
  }

    private void OnEnable()
    {
        move = input.Move.Main;
        move.Enable();
        Throttle = input.Move.Throttle;
        Throttle.Enable();
        Hand = input.Move.Hand;
        Hand.Enable();
        Steer = input.Move.Steer;
        Steer.Enable();
    }
    private void OnDisable()
    {
        input?.Disable();
        userInput = Vector2.zero;
    }

    private void OnDestroy()
    {
        input?.Dispose();
        if (wheels == null) return;
        foreach (var wheel in wheels)
            if (wheel?.skidTrail != null) Destroy(wheel.skidTrail.gameObject);
    }

    void Update()
    {
        // detect press of r key, reset rotation and move 2 units up
        if (Input.GetKeyDown(KeyCode.R))
        {
            float yrotation = transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, yrotation, 0);
            transform.position += Vector3.up * 2f;
            rb.velocity = transform.forward * 5f;
            rb.angularVelocity = Vector3.zero;
        }

        // Get player input for reference
        Vector2 moveInput = move.ReadValue<Vector2>();
        float steerInput = Steer.ReadValue<float>();
        float throttleInput = Throttle.ReadValue<float>();
        handbrakeInput = Hand.ReadValue<float>();

        // Add NaN checks for input values
        if (float.IsNaN(moveInput.x) || float.IsInfinity(moveInput.x)) moveInput.x = 0f;
        if (float.IsNaN(moveInput.y) || float.IsInfinity(moveInput.y)) moveInput.y = 0f;
        if (float.IsNaN(steerInput) || float.IsInfinity(steerInput)) steerInput = 0f;
        if (float.IsNaN(throttleInput) || float.IsInfinity(throttleInput)) throttleInput = 0f;
        if (float.IsNaN(handbrakeInput) || float.IsInfinity(handbrakeInput)) handbrakeInput = 0f;
        
        userInput.x = Mathf.Lerp(userInput.x, (moveInput.x + steerInput) / (1 + rb.velocity.magnitude * carSpeedFactor), 50f * Time.deltaTime);
        userInput.y = Mathf.Lerp(userInput.y, moveInput.y + throttleInput, 50f * Time.deltaTime);
        isBraking = userInput.y < 0 && forwards ? Mathf.Abs(userInput.y) : 0f;
        
        // Add NaN checks for final userInput values
        if (float.IsNaN(userInput.x) || float.IsInfinity(userInput.x)) userInput.x = 0f;
        if (float.IsNaN(userInput.y) || float.IsInfinity(userInput.y)) userInput.y = 0f;

        // Adaptive braking wing animation
        if (adaptiveBrakingWing != null) {
            float targetAngle = isBraking > 0.15f ? brakingWingAngle : 0f;
            currentWingAngle = Mathf.Lerp(currentWingAngle, targetAngle, brakingWingSpeed * Time.deltaTime);
            
            // Rotate wing forward (negative X rotation tips forward)
            adaptiveBrakingWing.transform.localRotation = Quaternion.Euler(currentWingAngle, 0, 0);
        }

        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            
            // Ensure no NaN values from previous frames
            if (float.IsNaN(w.slip) || float.IsInfinity(w.slip))
                w.slip = 0f;
            
            // High-performance F1 traction control
            if (throttleAssist)
            {
                float targetSlip = 0.91f; // Desired slip ratio for max traction
                float slipTolerance = 0.02f; // Allowable deviation from target slip
                if (w.slip > targetSlip + slipTolerance)
                {
                    // If slip exceeds the upper bound, calculate how much it overshoots
                    float overshoot = w.slip - targetSlip;
                    // Convert overshoot to a reduction factor (aggressive multiplier)
                    float reduction = Mathf.Clamp01(overshoot * 2.0f);
                    // Aggressively increase TCS reduction to cut power fast
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 1, reduction/5f);
                }
                else if (w.slip < targetSlip - slipTolerance)
                {
                    // If slip is below the lower bound, quickly restore power
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 0f, 0.6f * Time.deltaTime);
                }
                // Clamp TCS reduction to [0, 1] range
                w.tcsReduction = Mathf.Clamp01(w.tcsReduction);
            }
            if (steeringAssist)
            {
                float targetSlip = steerAssistTarget;
                float slipTolerance = 0.02f; // Allowable deviation from target slip
                if (w.slip > targetSlip + slipTolerance)
                {
                    // If slip exceeds the upper bound, calculate how much it overshoots
                    float overshoot = w.slip - targetSlip;
                    // Convert overshoot to a reduction factor (aggressive multiplier)
                    float reduction = Mathf.Clamp01(overshoot * 2.0f);
                    // Aggressively increase steering reduction to cut steering input fast
                    w.steeringReduction = Mathf.Lerp(w.steeringReduction, 1, reduction / 5f);
                }
                else if (w.slip < targetSlip - slipTolerance)
                {
                    // If slip is below the lower bound, quickly restore steering input
                    w.steeringReduction = Mathf.Lerp(w.steeringReduction, 0f, 6f * Time.deltaTime);
                }
                // Clamp steering reduction to [0, 1] range
                w.steeringReduction = Mathf.Clamp01(w.steeringReduction);
            }
            w.braking = isBraking * (1 - w.tcsReduction);

            float maxTilt = 8f + rb.velocity.magnitude*2;
            maxTilt = Mathf.Min(maxTilt, 65f);

            if (!motorCycleControl)w.input.x = Mathf.Lerp(w.input.x, userInput.x * (1f - w.steeringReduction), Time.deltaTime * 60f);
            else {
                // Motorcycle physics: steering based on lean angle
                float currentLeanAngle = transform.localRotation.eulerAngles.z;
                
                // Convert from 0-360 to -180 to +180 range
                if (currentLeanAngle > 180f)
                    currentLeanAngle -= 360f;
                
                // Get angular velocities for damping
                Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
                float tiltAngularVelocity = localAngularVelocity.z; // Z is forward axis (tilt direction)
                float yawAngularVelocity = localAngularVelocity.y; // Y is up axis (yaw direction)
                
                // Calculate steering based on lean angle with damping
                // Tilt damping opposes tilt angular velocity to prevent roll oscillation
                float tiltDampingComponent = tiltAngularVelocity * motorcycleTiltDamping;
                tiltDampingComponent = Mathf.Clamp(tiltDampingComponent, -40f, 40f);
                
                // If tiltDampingComponent is pushing the bike to a more extreme angle, set tiltDampingComponent to zero
                if ((currentLeanAngle > 0f && tiltDampingComponent < 0f) || (currentLeanAngle < 0f && tiltDampingComponent > 0f))
                {
                    tiltDampingComponent *= 0.7f;
                }

                // blend user input to limit to maxTilt
                float blend = Mathf.Clamp01(Mathf.Abs(currentLeanAngle) / maxTilt);

                // Yaw damping opposes yaw angular velocity to prevent turning oscillation
                float yawDampingComponent = yawAngularVelocity * motorcycleYawDamping;
                yawDampingComponent = Mathf.Clamp(yawDampingComponent, -30f, 30f);
                
                float leanSteering = (currentLeanAngle * (1 + Mathf.Max(blend- (Mathf.Min(rb.velocity.magnitude/20f, 1)), 0) * 3f) + userInput.x * (1-blend) * 30f * (1 + rb.velocity.magnitude / 60f) * (1 + Mathf.Max(0, rb.velocity.magnitude - 2f) / 3f) + tiltDampingComponent * (1.8f - blend * 0.5f) + yawDampingComponent) / 45f;
                leanSteering = Mathf.Clamp(leanSteering, -1f, 1f);
                
                // Combine lean-based steering with slight user input for responsiveness
                w.input.x = Mathf.Lerp(w.input.x, -leanSteering / (1 + Mathf.Max(0, rb.velocity.magnitude - 2f) / 3f), Time.deltaTime * 60f);
            }
            if (w.slip > 1.0f && steeringAssist && Mathf.Abs(w.turnAngle) > 0.001f) w.input.x = Mathf.Lerp(w.input.x, w.xSlipAngle / w.turnAngle, Time.deltaTime);

            // Apply throttle with TCS - more responsive for F1
            float finalThrottle = userInput.y * (1f - w.tcsReduction);
            if (float.IsNaN(finalThrottle) || float.IsInfinity(finalThrottle))
                finalThrottle = 0f;
            if (float.IsNaN(w.steeringReduction) || float.IsInfinity(w.steeringReduction))
                w.steeringReduction = 0f;

            if (throttleAssist)
            {
                w.input.y = Mathf.Lerp(w.input.y, finalThrottle, 0.95f * Time.deltaTime * 60f);
            } else w.input.y = userInput.y;
            
            // Add comprehensive NaN checks for wheel input
            if (float.IsNaN(w.input.x) || float.IsInfinity(w.input.x)) w.input.x = 0f;
            if (float.IsNaN(w.input.y) || float.IsInfinity(w.input.y)) w.input.y = 0f;
            
            // Clamp input values to reasonable range
            w.input.x = Mathf.Clamp(w.input.x, -1f, 1f);
            w.input.y = Mathf.Clamp(w.input.y, -1f, 1f);
        }

        if (Input.GetKeyDown(KeyCode.E)) e.UpGear(this);
        else if (Input.GetKeyDown(KeyCode.Q)) e.DownGear(this);

        e.checkGearSwitching(this, Mathf.Max(0f, userInput.y));

        // Update audio values
        if (audioController != null)
        {
            // Calculate average slip from all wheels
            float averageSlip = 0f;
            for (int i = 0; i < wheels.Length; i++)
            {
                averageSlip += wheels[i].slip;
            }
            averageSlip /= wheels.Length;
            
            // Update audio controller with current car state
            audioController.UpdateAudioValues(
                e.getRPM(),                          // rpm
                Mathf.Max(0f, userInput.y),          // throttle (only positive values)
                rb.velocity.magnitude,               // speed
                averageSlip,                         // slip
                e.isSwitchingGears()                 // shifting
            );
        }
    }

    // Add this method to calculate aerodynamic drag
    private void ApplyAerodynamicDrag()
    {
        Vector3 velocity = rb.velocity;
        float speed = velocity.magnitude;
        float speedKmh = speed * 3.6f; // Convert m/s to km/h
        
        // Calculate current drag coefficient based on speed (adaptive aero)
        float currentDragCoeff = dragCoefficient * (isBraking > 0.3f ? (adaptiveBrakingWing != null ? 2f : 1f) : 1f);
        // Drag force formula: F = 0.5 * ρ * v² * Cd * A
        float dragMagnitude = 0.5f * airDensity * speed * speed * currentDragCoeff * frontalArea;
        
        // Apply drag force opposite to velocity direction
        Vector3 dragForce = -velocity.normalized * dragMagnitude;
        
        // Apply the drag force
        rb.AddForce(dragForce / 200f, ForceMode.Force);
    }

    void FixedUpdate()
    {
        // Apply aerodynamic drag calculation
        ApplyAerodynamicDrag();
        
        rb.AddForceAtPosition(-transform.up * rb.velocity.magnitude * downforce / 28f, transform.position + transform.TransformDirection(centerOfDownforce), ForceMode.Acceleration);

        rb.AddForceAtPosition(-0.9f * transform.right * transform.InverseTransformDirection(rb.velocity).x, transform.position + transform.TransformDirection(new Vector3(0, 0, -1.5f * restoreStrength)), ForceMode.Acceleration);
        rb.AddForceAtPosition(-0.9f * transform.up * transform.InverseTransformDirection(rb.velocity).y, transform.position + transform.TransformDirection(new Vector3(0, 0, -1.5f * restoreStrengthY)), ForceMode.Acceleration);
        forwards = transform.InverseTransformDirection(rb.velocity).z > 0.1f;
        float averageWheelAngularVelocity = 0f;
        foreach (var w in wheels)
        {
            RaycastHit hit;
            float rayLen = w.size * 2f + w.suspensionLength;
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual = wheelObj.GetChild(0);

            // Add NaN checks for wheel rotation
            float steerAngle = w.turnAngle * w.input.x;
            if (float.IsNaN(steerAngle) || float.IsInfinity(steerAngle))
            {
                steerAngle = 0f;
                Debug.LogWarning($"NaN detected in wheel steering angle. turnAngle: {w.turnAngle}, input.x: {w.input.x}");
            }
            wheelObj.localRotation = Quaternion.Euler(0, steerAngle, 0);
            
            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);
            
            // Debug torque calculation components
            float enginePower = e.GetCurrentPower(this);
            float gearRatio = e.GetCurrentTotalGearRatio();
            w.torque = w.engineTorque * w.input.y * enginePower * gearRatio;
            
            float inertia = w.mass * w.size * w.size / 2f;
            float lateralVel = w.localVelocity.x;

            bool grounded = Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, rayLen, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (grounded)
            {
                float compression = rayLen - hit.distance;
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                w.normalForce = Mathf.Clamp((compression + damping) * suspensionForce, 0f, suspensionForceClamp);
                w.lastSuspensionLength = hit.distance;
            }
            else
            {
                w.normalForce = 0f;
                w.lastSuspensionLength = rayLen;
                w.suspensionForceDirection = Vector3.zero;
            }
            Vector3 worldVelAtHit = grounded ? rb.GetPointVelocity(hit.point) : Vector3.zero;
            float lateralHitVel = wheelObj.InverseTransformDirection(worldVelAtHit).x;

            float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
            float longitudinalFriction = grounded ? -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size) : 0f;

            float resistance = grounded ? Mathf.Max(0f, rollingResistanceCoeff * w.normalForce * w.size) : 0f;
            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity = WheelDynamics.ApplyResistance(w.angularVelocity, resistance, inertia, Time.fixedDeltaTime);
            w.angularVelocity *= Mathf.Clamp01(1f - w.braking * w.brakeStrength * Time.fixedDeltaTime);
            if (handbrakeInput > 0.5f) w.angularVelocity = 0f;

            Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                * w.normalForce * coefStaticFriction * coefFrictionMultiplier * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction * coefFrictionMultiplier;

            w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
            w.slip = WheelDynamics.SlipRatio(totalLocalForce.magnitude, currentMaxFrictionForce);
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;

            // w.xSlipAngle = (Mathf.Atan2(w.localVelocity.x, w.localVelocity.z) * Mathf.Rad2Deg) - w.turnAngle * w.input.x;
            // Calculate the wheel's actual heading direction in local space
            // Keep your original slip angle calculation but add safety check:

            if (w.localVelocity.magnitude > 0.5f) // Only calculate when moving
            {
                // Calculate the velocity angle
                float velocityAngle = Mathf.Atan2(w.localVelocity.x, w.localVelocity.z) * Mathf.Rad2Deg;
                
                // Current wheel steering angle
                float currentWheelAngle = w.turnAngle * w.input.x;
                
                // Slip angle is the difference between where we're going vs where we're pointed
                float rawSlipAngle = velocityAngle - currentWheelAngle;
                
                // Normalize angle to [-180, 180] range
                while (rawSlipAngle > 180f) rawSlipAngle -= 360f;
                while (rawSlipAngle < -180f) rawSlipAngle += 360f;
                
                // Apply some smoothing to reduce jitter
                w.xSlipAngle = Mathf.Lerp(w.xSlipAngle, rawSlipAngle, Time.fixedDeltaTime * 10f);
            }
            else
            {
                w.xSlipAngle = Mathf.Lerp(w.xSlipAngle, 0f, Time.fixedDeltaTime * 5f);
            }

            if (grounded)
            {
                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition(springDir + totalWorldForce, hit.point);
                w.lastSuspensionLength = hit.distance;
                wheelObj.position = hit.point + transform.up * w.size;

                if (w.slidding)
                {
                    // If no skid trail exists, instantiate a new one but don't start emitting yet
                    if (w.skidTrail == null && skidMarkPrefab != null)
                    {
                        GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                        skidTrailObj.transform.SetParent(w.wheelObject.transform);
                        skidTrailObj.transform.localPosition = Vector3.zero;
                        w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                        w.skidTrail.time = 30f;
                        w.skidTrail.autodestruct = true;
                        w.skidTrail.emitting = false; // Start with emitting disabled
                        w.skidTrail.transform.position = hit.point;

                        // Set initial rotation
                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f) skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;
                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                    else if (w.skidTrail != null)
                    {
                        // Only start emitting after the trail has existed for at least one frame
                        if (!w.skidTrail.emitting)
                        {
                            w.skidTrail.emitting = true;
                        }

                        // Update position and rotation
                        w.skidTrail.transform.position = hit.point;
                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f) skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;
                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                }
                else if (w.skidTrail != null)
                {
                    // Only detach and destroy if the trail was actually emitting (had multiple points)
                    if (w.skidTrail.emitting)
                    {
                        w.skidTrail.emitting = false;
                        w.skidTrail.transform.parent = null;
                        Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    }
                    else
                    {
                        // If it never started emitting, just destroy it immediately
                        Destroy(w.skidTrail.gameObject);
                    }
                    w.skidTrail = null;
                }
            }
            else
            {
                wheelObj.position = w.wheelWorldPosition + transform.up * (w.size - rayLen);
                if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }
            
            // Always accumulate wheel angular velocity for engine RPM calculation, regardless of grounded state
            averageWheelAngularVelocity += w.angularVelocity;
            
            wheelVisual.Rotate(
                Vector3.right,
                w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime,
                Space.Self
            );
        }
        averageWheelAngularVelocity /= wheels.Length;
        e.SetRPM(averageWheelAngularVelocity);
    }
}
