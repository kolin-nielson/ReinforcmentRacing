using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class VehicleController : MonoBehaviour
{
    public float Acceleration;
    public AnimationCurve AccelerationCurve;

    [Header("Player Stats Overrides")]
    [Tooltip("Enable to override MaxSpeed and Acceleration for the player vehicle")]
    [SerializeField] private bool usePlayerOverrides = false;
    [Tooltip("Custom top speed when overridden")]
    [SerializeField] private float playerMaxSpeed = 200f;
    [Tooltip("Custom acceleration force when overridden")]
    [SerializeField] private float playerAcceleration = 10f;

    public float steerInput, accelerationInput, handbrakeInput, rearTrack, wheelBase, ackermennLeftAngle, ackermennRightAngle;
    public float airAngularDrag = 0.2f;
    public bool AutoCounterSteer = false;
    public float brakeAcceleration = 50f;
    public bool CanAccelerate;

    [HideInInspector]
    public bool CanDrive;

    [HideInInspector]
    public Vector3 carVelocity;

    public Transform CenterOfMass_air;
    public float DownForce = 5;
    public float driftFactor = 0.2f;

    [Range(0, 10)]
    public float forwardBodyTilt = 3f;

    public AnimationCurve forwardFrictionCurve;
    public float FrictionCoefficient = 1f;
    public Transform[] HardPoints = new Transform[4];

    [Tooltip("Curve for acceleration adjustment based on incline angle")]
    public AnimationCurve InclineAccelerationCurve = AnimationCurve.Constant(0, 1, 1);

    public InputManager inputManager;

    [HideInInspector]
    public Vector3 localVehicleVelocity;

    [Range(0, 90)]
    public float maxDriftAngle = 60f;

    [Header("Car Stats")]
    [Space(10)]
    public float MaxSpeed = 200f;

    //Animation Curve for acceleration adjustment based on incline
    public float MaxTurnAngle = 30f;

    public float maxWheelTravel = 0.2f;
    public float RollingResistance = 2f;
    public AnimationCurve sideFrictionCurve;

    [Range(0, 10)]
    public float sidewaysBodyTilt = 3f;

    public float[] forwardSlip = new float[4], slipCoeff = new float[4], skidTotal = new float[4];
    public float slopeSlideAngle = 30f;
    public float springDamper = 200f;

    [Header("Suspension")]
    [Space(10)]
    public float springForce = 30000f;

    public AnimationCurve turnCurve;

    [Header("Visuals")]
    [Space(10)]
    public Transform VehicleBody;

    [Header("Events")]
    [Space(10)]
    public Vehicle_Events VehicleEvents;

    [HideInInspector]
    public bool vehicleIsGrounded;

    public float wheelRadius;
    public Transform[] Wheels;

    [SerializeField]
    private float additionalDownForce = 1.5f;

    private Vector3 CentreOfMass_ground;
    private float driftAngle;

    [Header("Debug Settings")]
    [SerializeField]
    private bool enableLogging = true;

    //Skidmarks
    [HideInInspector]
    public float[] skidTotalVisual = new float[4];

    // Increased from 2.5f for more responsive steering
    [SerializeField]
    private float frontWheelFrictionMultiplier = 3.0f;

    // Only log ML-Agent related inputs
    private float lastLogTime = 0f;

    private Vector3 lastVelocity;

    //[Header("Engine")]
    //public float engineRPM;
    //public float maxEngineRPM = 6000f;
    //public float minEngineRPM = 1000f;
    //public float engineInertia = 0.3f;
    //public float engineFrictionFactor = 0.25f;
    //
    //public AnimationCurve engineTorqueCurve =
    //   new AnimationCurve(
    //                         new Keyframe(0f, 50f), // Idle torque at 0 RPM
    //                         new Keyframe(1500f, 200f), // Torque starts to increase
    //                         new Keyframe(3000f, 300f), // Peak torque
    //                         new Keyframe(4500f, 250f), // Torque starts to decrease
    //                         new Keyframe(6000f, 150f)  // Torque at redline
    //                     );

    // Add serialized field for lateral friction multiplier
    [Header("Improved Steering")]
    [SerializeField]
    private float lateralFrictionMultiplier = 4.0f;

    [SerializeField]
    private float logFrequency = 1.0f;

    // Reduced frequency
    [SerializeField]
    private bool logMLAgentInputsOnly = true;

    private float MaxSpringDistance;
    private int NumberOfGroundedWheels;
    private float[] offset_Prev = new float[4];
    private Rigidbody rb;

    // Increased from 2.0f
    [SerializeField]
    private float rearWheelDriftFactor = 0.5f;

    // Increased from 2.5f for more grip
    [SerializeField]
    private float steeringForceMultiplier = 3.5f;

    private float[] suspensionForce = new float[4];
    private bool tempGroundedProperty;

    [Header("Other Things")]

    private RaycastHit[] wheelHits = new RaycastHit[4];

    [Serializable]
    public class Vehicle_Events
    {
        public UnityEvent OnGearChange;
        public UnityEvent OnGrounded;
        public UnityEvent OnTakeOff;
    }

    public void AddLateralFriction(Vector3 hardPoint, Transform wheel, RaycastHit wheelHit, bool wheelIsGrounded, float factor)
    {
        if (wheelIsGrounded)
        {
            Vector3 SurfaceNormal = wheelHit.normal;

            Vector3 contactVel = (wheel.InverseTransformDirection(rb.GetPointVelocity(hardPoint)).x) * wheel.right;
            //contactVel = localVehicleVelocity.x * wheel.right;
            //Debug.DrawRay(hardPoint, contactVel.normalized, Color.gray);
            Vector3 contactDesiredAccel = -Vector3.ProjectOnPlane(contactVel, SurfaceNormal) / Time.fixedDeltaTime;

            //Vector3 frictionForce = Vector3.ClampMagnitude(rb.mass/4 * contactDesiredAccel, springForce * FrictionCoefficient);
            Vector3 frictionForce = rb.mass / 4 * contactDesiredAccel * FrictionCoefficient;

            //Debug.DrawRay(hardPoint, frictionForce.normalized, Color.red);

            rb.AddForceAtPosition(frictionForce * factor, hardPoint);
        }

    }

    public void AddLateralFriction_2(Vector3 hardPoint, Transform wheel, RaycastHit wheelHit, bool wheelIsGrounded, float factor, float suspensionForce, int wheelNum)
    {
        if (wheelIsGrounded)
        {
            Vector3 SurfaceNormal = wheelHit.normal;

            Vector3 sideVelocity = (wheel.InverseTransformDirection(rb.GetPointVelocity(hardPoint)).x) * wheel.right;
            Vector3 forwardVelocity = (wheel.InverseTransformDirection(rb.GetPointVelocity(hardPoint)).z) * wheel.forward;

            slipCoeff[wheelNum] = sideVelocity.magnitude / (sideVelocity.magnitude + Mathf.Clamp(forwardVelocity.magnitude, 0.1f, forwardVelocity.magnitude));

            Vector3 contactDesiredAccel = -Vector3.ProjectOnPlane(sideVelocity, SurfaceNormal) / Time.fixedDeltaTime;

            // Apply different friction multipliers based on wheel position
            float frictionMultiplier = FrictionCoefficient;

            // Increase base friction coefficient for all wheels to counter "ice" feeling
            frictionMultiplier *= 1.5f;

            // Front wheels (0 and 1) get more lateral friction for better steering
            if (wheelNum < 2)
            {
                // Apply more friction when turning
                if (Mathf.Abs(steerInput) > 0.1f)
                {
                    frictionMultiplier *= lateralFrictionMultiplier * frontWheelFrictionMultiplier;
                }
                else
                {
                    // Still need some friction when not turning
                    frictionMultiplier *= lateralFrictionMultiplier;
                }
            }
            // Rear wheels (2 and 3) can have reduced friction during handbrake for drifting
            else if (handbrakeInput > 0.1f)
            {
                float driftFactor = Mathf.Lerp(rearWheelDriftFactor, 1.0f,
                    Mathf.Clamp01(1.0f - (driftAngle / maxDriftAngle)));

                frictionMultiplier *= driftFactor;
            }
            else
            {
                // Add more friction to rear wheels too
                frictionMultiplier *= 1.25f;
            }

            // Increase friction as speed increases to counter high-speed sliding
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 10f); // Normalizes around 10 m/s
            frictionMultiplier *= (1f + speedFactor * 0.5f);

            Vector3 frictionForce = Vector3.ClampMagnitude(rb.mass * contactDesiredAccel * sideFrictionCurve.Evaluate(slipCoeff[wheelNum]),
                                                        suspensionForce * frictionMultiplier);
            frictionForce = suspensionForce * frictionMultiplier * -sideVelocity.normalized * sideFrictionCurve.Evaluate(slipCoeff[wheelNum]);

            float clampedFrictionForce = Mathf.Min(rb.mass / 4 * contactDesiredAccel.magnitude, -Physics.gravity.y * rb.mass);

            frictionForce = Vector3.ClampMagnitude(frictionForce * factor, clampedFrictionForce);

            // gravity friction
            float slopeAngle = Vector3.Angle(transform.up, Vector3.up);
            Vector3 gravityForce = Physics.gravity.y * (rb.mass / 4) * Vector3.up;
            Vector3 gravitySideFriction = -Vector3.Project(gravityForce, transform.right);

            Vector3 gravityForwardFriction = -Vector3.Project(gravityForce, transform.forward);

            if (slopeAngle > slopeSlideAngle)
            {
                gravitySideFriction = Vector3.zero;
                gravityForwardFriction = Vector3.zero;
            }

            // Apply more direct friction force to reduce sliding
            Vector3 totalFrictionForce = (frictionForce * factor) + gravitySideFriction;

            // Special case for high-speed turning to reduce the "ice" feeling
            if (Mathf.Abs(steerInput) > 0.3f && rb.linearVelocity.magnitude > 10f && wheelNum < 2)
            {
                // Apply extra anti-slide force
                float antiSlideMultiplier = 1.5f;
                totalFrictionForce *= antiSlideMultiplier;
            }

            rb.AddForceAtPosition(totalFrictionForce, hardPoint);

            if (handbrakeInput > 0 || localVehicleVelocity.magnitude < 0.1f)
            {
                rb.AddForce(gravityForwardFriction);
            }

            // Only log when ML-Agent controlled and having issues
            if (logMLAgentInputsOnly && inputManager && inputManager.isMLAgentControlled &&
                Mathf.Abs(steerInput) > 0.3f && rb.linearVelocity.magnitude < 1.0f &&
                Time.frameCount % 180 == 0 && wheelNum < 2) // Front wheels, when not moving
            {
                // No-op (removing debug)
            }
        }
    }

    void AckermannSteering(float steerInput)
    {
        // Base steering angle in degrees
        float steeringAngle = steerInput * MaxTurnAngle;

        // Calculate turn radius and Ackermann steering angles for both wheels
        float turnRadius = wheelBase / Mathf.Tan(Mathf.Abs(steeringAngle) * Mathf.Deg2Rad);

        // Calculate left and right wheel angles
        if (steerInput > 0) // turning right
        {
            ackermennLeftAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius + (rearTrack / 2))) * steerInput;
            ackermennRightAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius - (rearTrack / 2))) * steerInput;
        }
        else if (steerInput < 0) // turning left
        {
            ackermennLeftAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius - (rearTrack / 2))) * steerInput;
            ackermennRightAngle = Mathf.Rad2Deg * Mathf.Atan(wheelBase / (turnRadius + (rearTrack / 2))) * steerInput;
        }
        else
        {
            ackermennLeftAngle = 0;
            ackermennRightAngle = 0;
        }

        // Apply counter-steering if enabled
        if (localVehicleVelocity.z > 0 && AutoCounterSteer && Mathf.Abs(localVehicleVelocity.x) > 1f)
        {
            ackermennLeftAngle += Vector3.SignedAngle(transform.forward, rb.linearVelocity + transform.forward, transform.up);
            ackermennLeftAngle = Mathf.Clamp(ackermennLeftAngle, -70, 70);
            ackermennRightAngle += Vector3.SignedAngle(transform.forward, rb.linearVelocity + transform.forward, transform.up);
            ackermennRightAngle = Mathf.Clamp(ackermennRightAngle, -70, 70);
        }

        // Apply steering angles to wheels with speed-based curve adjustment
        Wheels[0].localRotation = Quaternion.Euler(0, ackermennLeftAngle * turnCurve.Evaluate(localVehicleVelocity.z / MaxSpeed), 0);
        Wheels[1].localRotation = Quaternion.Euler(0, ackermennRightAngle * turnCurve.Evaluate(localVehicleVelocity.z / MaxSpeed), 0);

        // Apply extra steering torque to help with turning
        // Stronger at lower speeds, less at higher speeds to prevent oversteer
        float speedFactor = Mathf.Lerp(1.0f, 0.5f, Mathf.Clamp01(rb.linearVelocity.magnitude / MaxSpeed));

        // Add more direct steering force at low speeds and gradually reduce as speed increases
        if (rb.linearVelocity.magnitude < 10f)
        {
            speedFactor = Mathf.Lerp(1.5f, 1.0f, rb.linearVelocity.magnitude / 10f);
        }

        float steerForce = steerInput * 3000f * steeringForceMultiplier * speedFactor;

        // Apply torque when steering and the car is moving
        if (Mathf.Abs(steerInput) > 0.1f && rb.linearVelocity.magnitude > 0.5f)
        {
            // Apply a torque to help the car turn
            rb.AddTorque(transform.up * steerForce * Time.fixedDeltaTime, ForceMode.Force);

            // Add an immediate velocity change for more responsive steering at lower speeds
            if (rb.linearVelocity.magnitude < 8f && Mathf.Abs(steerInput) > 0.3f)
            {
                // Calculate rotation amount based on steering input
                float rotationFactor = steerInput * 0.06f * (8f - rb.linearVelocity.magnitude) / 8f;

                // Add immediate rotation to the velocity vector
                Vector3 currentVelocity = rb.linearVelocity;
                Vector3 rotatedVelocity = Quaternion.AngleAxis(rotationFactor * 90f, transform.up) * currentVelocity;

                // Apply a percentage of the rotated velocity
                rb.linearVelocity = Vector3.Lerp(currentVelocity, rotatedVelocity, 0.15f);
            }
        }
        else if (enableLogging && inputManager.isMLAgentControlled &&
                 Mathf.Abs(steerInput) > 0.1f && rb.linearVelocity.magnitude <= 0.5f &&
                 Time.frameCount % 180 == 0)
        {
            // Log when agent is trying to steer but car isn't moving
            // Debug.Log($"[MLDiag] ISSUE: ML-Agent steering input ({steerInput:F2}) but vehicle speed too low ({rb.linearVelocity.magnitude:F2}m/s)");
        }
    }

    void AddAcceleration(float accelerationInput)
    {
        if (accelerationInput == 0f)
        {
            if (enableLogging && inputManager.isMLAgentControlled && Time.frameCount % 300 == 0)
            {
                // Debug.Log($"[MLDiag] ML-Agent zero acceleration input");
            }
            return;
        }

        float accelDirection = Mathf.Sign(accelerationInput);

        float speed = Vector3.Dot(transform.forward, rb.linearVelocity);

        // Don't allow adding more acceleration if the speed is already maxed out in that direction
        if ((accelDirection > 0 && speed >= MaxSpeed) || (accelDirection < 0 && speed <= -MaxSpeed * 0.5f))
        {
            if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
            {
                // Debug.Log($"[VehicleController] AddAcceleration skipped - At max speed: {speed:F1}/{MaxSpeed}");
            }
            return;
        }

        // Calculate incline and adjust acceleration
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        float inclineMultiplier = 1.0f;

        if (Physics.Raycast(rayStart, -transform.up, out hit, 2f))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            float direction = Mathf.Sign(Vector3.Dot(transform.forward, Vector3.ProjectOnPlane(Vector3.up, hit.normal)));

            // Adjust acceleration based on incline
            inclineMultiplier = InclineAccelerationCurve.Evaluate(angle / 90f);

            // If going uphill, reduce acceleration (positive direction value when facing uphill)
            if (direction * accelDirection < 0)
            {
                accelerationInput *= inclineMultiplier;
            }
        }

        // Log acceleration calculation
        if (Time.frameCount % 120 == 0) // Every ~2 seconds at 60fps
        {
            // Debug.Log($"[VehicleController] AddAcceleration - Input: {accelerationInput:F2}, Speed: {speed:F1}/{MaxSpeed}, Incline multiplier: {inclineMultiplier:F2}");
        }

        if (enableLogging && Time.time - lastLogTime > logFrequency)
        {
            // Debug.Log($"AddAcceleration - Input: {accelerationInput:F2}, Speed: {speed:F1}/{MaxSpeed}");
        }

        float accelerationForce = 0f;

        float speedRatio = Mathf.Abs(speed) / MaxSpeed;
        accelerationForce = AccelerationCurve.Evaluate(speedRatio) * accelDirection * Acceleration * rb.mass;

        // Log applied force
        if (Time.frameCount % 120 == 0) // Every ~2 seconds at 60fps
        {
            // Debug.Log($"[VehicleController] Applying acceleration force: {accelerationForce:F1}N in direction {transform.forward}");
        }

        rb.AddForce(transform.forward * accelerationForce, ForceMode.Force);

        // Log acceleration from ML-Agent
        if (enableLogging && inputManager.isMLAgentControlled && Time.frameCount % 300 == 0)
        {
            // Debug.Log($"[MLDiag] ML-Agent acceleration: Input={accelerationInput:F2}, Speed={speed:F1}/{MaxSpeed}, Force={accelerationForce:F1}N");
        }
    }

    void AddRollingResistance()
    {
        float localSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        float deltaSpeed = RollingResistance * Time.fixedDeltaTime * Mathf.Clamp01(Mathf.Abs(localSpeed));
        deltaSpeed = Mathf.Clamp(deltaSpeed, -MaxSpeed, MaxSpeed);
        if (accelerationInput == 0)
        {
            if (localSpeed > 0)
            {
                rb.linearVelocity -= transform.forward * deltaSpeed;
            }
            else
            {
                rb.linearVelocity += transform.forward * deltaSpeed;
            }
        }

    }

    //Vector3[] wheelRelativeVel = new Vector3[4];

    void AddSuspensionForce(Vector3 hardPoint, Transform wheel, float MaxSpringDistance, out RaycastHit wheelHit, out bool WheelIsGrounded, out float SuspensionForce, int WheelNum)
    {
        var direction = -transform.up;

        // SphereCast to detect if the wheel is grounded
        WheelIsGrounded = Physics.SphereCast(hardPoint + (transform.up * wheelRadius), wheelRadius, direction, out wheelHit, MaxSpringDistance);

        if (WheelIsGrounded)
        {
            float springCompression = MaxSpringDistance - wheelHit.distance;
            Vector3 springDirection = transform.up;
            Vector3 wheelWorldVelocity = rb.GetPointVelocity(hardPoint);
            float relativeVelocity = Vector3.Dot(springDirection, wheelWorldVelocity);

            // Standard spring force calculation
            float springForceMagnitude = springForce * springCompression;
            float damperForceMagnitude = springDamper * relativeVelocity;
            SuspensionForce = springForceMagnitude - damperForceMagnitude;

            // Apply hard stop force if compression exceeds threshold
            if (springCompression >= 0.3f && relativeVelocity > 0)
            {
                //rb.velocity -= relativeVelocity * springDirection;
            }

            // Apply force only if the spring is compressed
            if (springCompression > 0)
            {
                rb.AddForceAtPosition(springDirection * SuspensionForce, hardPoint);
            }
        }
        else
        {
            SuspensionForce = 0;
        }
    }

    void AddSuspensionForce_2(Vector3 hardPoint, Transform wheel, float MaxSpringDistance, out RaycastHit wheelHit, out bool WheelIsGrounded, out float SuspensionForce, int WheelNum)
    {
        var direction = -transform.up;

        if (Physics.SphereCast(hardPoint + (transform.up * wheelRadius), wheelRadius, direction, out wheelHit, MaxSpringDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            WheelIsGrounded = true;
        }
        else
        {
            WheelIsGrounded = false;
        }

        // suspension spring force
        if (WheelIsGrounded)
        {
            Vector3 springDir = wheelHit.normal;
            //springDir = transform.up;
            float offset = (MaxSpringDistance + 0.1f - wheelHit.distance) / (MaxSpringDistance - wheelRadius - 0.1f);
            offset = Mathf.Clamp01(offset);

            float vel = -((offset - offset_Prev[WheelNum]) / Time.fixedDeltaTime);

            Vector3 wheelWorldVel = rb.GetPointVelocity(wheelHit.point);
            float WheelVel = Vector3.Dot(transform.up, wheelWorldVel);



            offset_Prev[WheelNum] = offset;
            if (offset < 0.3f)
            {
                vel = 0;
            }
            else if (vel < 0 && offset > 0.6f && WheelVel < 10)
            {
                vel *= 10;
            }

            float TotalSpringForce = offset * offset * springForce;
            float totalDampingForce = Mathf.Clamp(-(vel * springDamper), -0.25f * rb.mass * Mathf.Abs(WheelVel) / Time.fixedDeltaTime, 0.25f * rb.mass * Mathf.Abs(WheelVel) / Time.fixedDeltaTime);
            if ((MaxSpringDistance + 0.1f - wheelHit.distance) < 0.1f)
            {
                totalDampingForce = 0;
            }
            float force = TotalSpringForce + totalDampingForce;


            SuspensionForce = force;

            Vector3 suspensionForce_vector = Vector3.Project(springDir, transform.up) * force;

            rb.AddForceAtPosition(suspensionForce_vector, hardPoint);

            //if (offset > 0.5f && WheelVel > 5)
            //{
            //    rb.velocity -= WheelVel * springDir / 4;
            //}

        }
        else
        {
            SuspensionForce = 0;
        }

    }

    // Additional downforce multiplier

    void Awake()
    {
        inputManager = inputManager ?? GetComponent<InputManager>() ?? throw new MissingComponentException("Input Manager is missing.");

        // Apply player-specific overrides if enabled and not ML-Agent controlled
        if (usePlayerOverrides && inputManager != null && !inputManager.isMLAgentControlled)
        {
            MaxSpeed = playerMaxSpeed;
            Acceleration = playerAcceleration;
            Debug.Log($"[VehicleController] Applied player overrides: MaxSpeed={MaxSpeed}, Acceleration={Acceleration}");
        }

        CanDrive = true;
        CanAccelerate = true;

        rb = GetComponent<Rigidbody>();
        lastVelocity = Vector3.zero;


        for (int i = 0; i < Wheels.Length; i++)
        {
            HardPoints[i].localPosition = new Vector3(Wheels[i].localPosition.x, 0, Wheels[i].localPosition.z);

        }
        MaxSpringDistance = Mathf.Abs(Wheels[0].localPosition.y - HardPoints[0].localPosition.y) + 0.1f + wheelRadius;

        wheelBase = Vector3.Distance(Wheels[0].position, Wheels[2].position);
        rearTrack = Vector3.Distance(Wheels[0].position, Wheels[1].position);


        if (enableLogging)
        {
            // Debug.Log($"VehicleController initialized on {gameObject.name}");
            // Debug.Log($"Input Manager reference: {(inputManager != null ? inputManager.name : "NULL")}");
        }
    }

    void bodyAnimation()
    {
        Vector3 accel = Vector3.ProjectOnPlane((rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime, transform.up);
        accel = transform.InverseTransformDirection(accel);
        lastVelocity = rb.linearVelocity;

        VehicleBody.localRotation = Quaternion.Lerp(VehicleBody.localRotation, Quaternion.Euler(Mathf.Clamp(-accel.z / 10, -forwardBodyTilt, forwardBodyTilt), 0, Mathf.Clamp(accel.x / 5, -sidewaysBodyTilt, sidewaysBodyTilt)), 0.1f);
    }

    void brakeLogic(float brakeInput)
    {
        float localSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        float deltaSpeed = brakeAcceleration * brakeInput * Time.fixedDeltaTime * Mathf.Clamp01(Mathf.Abs(localSpeed));
        deltaSpeed = Mathf.Clamp(deltaSpeed, -MaxSpeed, MaxSpeed);
        if (localSpeed > 0)
        {
            rb.linearVelocity -= transform.forward * deltaSpeed;
        }
        else
        {
            rb.linearVelocity += transform.forward * deltaSpeed;
        }

    }

    void FixedUpdate()
    {
        localVehicleVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        driftAngle = Mathf.Abs(Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(rb.linearVelocity, transform.up)));

        // Only log detailed physics info when ML-Agent controlled
        if (enableLogging && inputManager.isMLAgentControlled && Time.frameCount % 300 == 0) // Every ~5 seconds
        {
            // Debug.Log($"[MLDiag] Vehicle Physics - World Vel: {rb.linearVelocity.magnitude:F2}m/s, Local Vel: {localVehicleVelocity.z:F2}m/s forward, {localVehicleVelocity.x:F2}m/s lateral");
        }

        AckermannSteering(steerInput);

        // Log actual inputs being used for physics
        if (enableLogging && Time.time - lastLogTime > logFrequency)
        {
            // Debug.Log($"VehicleController physics using - Steer={steerInput:F2}, Accel={accelerationInput:F2}, Speed={localVehicleVelocity.z:F1}m/s");
        }

        //float suspensionForce = 0;
        suspensionForce[0] = 0;
        suspensionForce[1] = 0;
        suspensionForce[2] = 0;
        suspensionForce[3] = 0;

        for (int i = 0; i < Wheels.Length; i++)
        {
            bool wheelIsGrounded = false;

            AddSuspensionForce_2(HardPoints[i].position, Wheels[i], MaxSpringDistance, out wheelHits[i], out wheelIsGrounded, out suspensionForce[i], i);
            //AddSuspensionForce(HardPoints[i].position, Wheels[i], MaxSpringDistance, out wheelHits[i], out wheelIsGrounded, out suspensionForce, i);

            // Log wheel contact info periodically
            if (Time.frameCount % 180 == 0 && i < 2) // Log front wheels (the steering wheels) less frequently
            {
                string groundedStatus = wheelIsGrounded ? "GROUNDED" : "NOT GROUNDED";
                string surfaceTag = wheelIsGrounded ? (wheelHits[i].collider != null ? wheelHits[i].collider.tag : "Unknown") : "None";
                // Debug.Log($"[VehicleController] Wheel {i} status: {groundedStatus}, Surface: {surfaceTag}, Suspension force: {suspensionForce[i]:F1}");
            }

            GroundedCheckPerWheel(wheelIsGrounded);

            tireVisual(wheelIsGrounded, Wheels[i], HardPoints[i], wheelHits[i].distance, i);
        }

        // Calculate improved suspension forces - reference implementation had a more stable suspension
        float suspensionForce_hackSum = (suspensionForce[0] + suspensionForce[1] + suspensionForce[2] + suspensionForce[3]) / 4;
        suspensionForce[0] = suspensionForce_hackSum;
        suspensionForce[1] = suspensionForce_hackSum;
        suspensionForce[2] = suspensionForce_hackSum;
        suspensionForce[3] = suspensionForce_hackSum;

        vehicleIsGrounded = (NumberOfGroundedWheels > 1);

        if (vehicleIsGrounded)
        {
            AddAcceleration(accelerationInput);
            AddRollingResistance();
            brakeLogic(handbrakeInput);
            bodyAnimation();

            //AutoBalence
            if (rb.centerOfMass != CentreOfMass_ground)
            {
                rb.centerOfMass = CentreOfMass_ground;
            }

            //ground angular drag - increased for better stability
            rb.angularDamping = 2.0f; // Increased from 1.0f

            //downforce - increased for better grip
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / MaxSpeed);
            float dynamicDownforce = DownForce * (1f + speedFactor * additionalDownForce);
            rb.AddForce(-transform.up * dynamicDownforce * rb.mass);

            // Add extra grip when turning at speed - like in the reference implementation
            if (Mathf.Abs(steerInput) > 0.1f && rb.linearVelocity.magnitude > 5f)
            {
                float extraGripForce = rb.mass * 0.5f * speedFactor;
                rb.AddForce(-transform.up * extraGripForce);
            }
        }
        else
        {
            if (rb.centerOfMass != CenterOfMass_air.localPosition)
            {
                rb.centerOfMass = CenterOfMass_air.localPosition;
            }

            // air angular drag - increased for more stable airtime
            rb.angularDamping = airAngularDrag * 1.5f; // Increased drag in air
        }

        // Apply lateral friction for wheels
        for (int i = 0; i < Wheels.Length; i++)
        {
            if (i < 2) // Front wheels
            {
                AddLateralFriction_2(HardPoints[i].position, Wheels[i], wheelHits[i], vehicleIsGrounded, 1, suspensionForce[i], i);
            }
            else // Rear wheels
            {
                if (handbrakeInput > 0.1f && driftAngle < maxDriftAngle)
                {
                    AddLateralFriction_2(HardPoints[i].position, Wheels[i], wheelHits[i], vehicleIsGrounded, rearWheelDriftFactor, suspensionForce[i], i);
                }
                else
                {
                    AddLateralFriction_2(HardPoints[i].position, Wheels[i], wheelHits[i], vehicleIsGrounded, 1, suspensionForce[i], i);
                }
            }
        }

        // Additional stabilization to prevent ice-like sliding
        if (vehicleIsGrounded && rb.linearVelocity.magnitude > 1.0f)
        {
            // Calculate lateral velocity in world space
            Vector3 forwardDir = transform.forward;
            Vector3 rightDir = transform.right;
            Vector3 forwardVel = Vector3.Dot(rb.linearVelocity, forwardDir) * forwardDir;
            Vector3 rightVel = Vector3.Dot(rb.linearVelocity, rightDir) * rightDir;

            // Apply counter force to lateral movement when not steering
            if (Mathf.Abs(steerInput) < 0.1f && rightVel.magnitude > 1.0f)
            {
                float stabilizingForceMagnitude = rightVel.magnitude * 0.8f * rb.mass;
                Vector3 stabilizingForce = -rightVel.normalized * stabilizingForceMagnitude;
                rb.AddForce(stabilizingForce, ForceMode.Force);
            }
        }

        NumberOfGroundedWheels = 0; //reset grounded int

        // Only log additional steering and friction info when ML-Agent controlled with issues
        if (enableLogging && inputManager.isMLAgentControlled &&
            Mathf.Abs(steerInput) > 0.1f && rb.linearVelocity.magnitude < 1.0f &&
            Time.frameCount % 180 == 0) // Every ~3 seconds when not moving despite steering input
        {
            // Debug.Log($"[MLDiag] ISSUE: ML-Agent steering ({steerInput:F2}) but vehicle not moving. Force: {steeringForceMultiplier}");
        }
    }

    void GroundedCheckPerWheel(bool wheelIsGrounded)
    {
        if (wheelIsGrounded)
        {
            NumberOfGroundedWheels += 1;
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 wantedImpulseY = Vector3.Dot(collision.impulse, transform.up) * transform.up;
        Vector3 wantedImpulseZ = Vector3.Dot(collision.impulse, transform.forward) * transform.forward;

        //rb.AddForce(-(wantedImpulseY + wantedImpulseZ), ForceMode.Impulse);
    }

    private void Start()
    {
        CentreOfMass_ground = (HardPoints[0].localPosition + HardPoints[1].localPosition + HardPoints[2].localPosition + HardPoints[3].localPosition) / 4;

        rb.centerOfMass = CentreOfMass_ground;

        // Check Rigidbody configuration and log it
        // Debug.Log($"[VehicleController] Rigidbody settings on {gameObject.name}:");
        // Debug.Log($"[VehicleController] - Mass: {rb.mass} kg");
        // Debug.Log($"[VehicleController] - Drag: {rb.linearDamping}, Angular Drag: {rb.angularDamping}");
        // Debug.Log($"[VehicleController] - Use Gravity: {rb.useGravity}, Is Kinematic: {rb.isKinematic}");
        // Debug.Log($"[VehicleController] - Interpolation: {rb.interpolation}, Collision Detection: {rb.collisionDetectionMode}");
        // Debug.Log($"[VehicleController] - Constraints: {rb.constraints}");

        // Check for potential issues
        if (rb.isKinematic)
        {
            Debug.LogError("[VehicleController] Rigidbody is set to Kinematic - car will not move properly with physics!");
        }

        if (!rb.useGravity)
        {
            Debug.LogError("[VehicleController] Rigidbody has gravity disabled - car may not behave correctly!");
        }

        if (rb.mass < 100)
        {
            Debug.LogWarning("[VehicleController] Rigidbody mass seems low for a car. Consider increasing it.");
        }

        if (rb.constraints != RigidbodyConstraints.None)
        {
            Debug.LogWarning("[VehicleController] Rigidbody has constraints set, which may restrict movement.");
        }
    }

    void tireVisual(bool WheelIsGrounded, Transform wheel, Transform hardPoint, float hitDistance, int tireNum)
    {
        if (WheelIsGrounded)
        {
            Vector3 wheelPos = wheel.localPosition;
            if (offset_Prev[tireNum] > 0.3f)
            {
                wheelPos = hardPoint.localPosition + (Vector3.up * wheelRadius) - Vector3.up * (hitDistance);
            }
            else
            {
                wheelPos = Vector3.Lerp(new Vector3(hardPoint.localPosition.x, wheel.localPosition.y, hardPoint.localPosition.z), hardPoint.localPosition + (Vector3.up * wheelRadius) - Vector3.up * (hitDistance), 0.1f);
            }

            if (wheelPos.y > hardPoint.localPosition.y + wheelRadius + maxWheelTravel - MaxSpringDistance)
            {
                wheelPos.y = hardPoint.localPosition.y + wheelRadius + maxWheelTravel - MaxSpringDistance;
            }


            wheel.localPosition = wheelPos;

        }
        else
        {
            wheel.localPosition = Vector3.Lerp(new Vector3(hardPoint.localPosition.x, wheel.localPosition.y, hardPoint.localPosition.z), hardPoint.localPosition + (Vector3.up * wheelRadius) - Vector3.up * MaxSpringDistance, 0.05f);
        }

        Vector3 wheelVelocity = rb.GetPointVelocity(hardPoint.position);
        float minRotation = (Vector3.Dot(wheelVelocity, wheel.forward) / wheelRadius) * Time.fixedDeltaTime * Mathf.Rad2Deg;
        float maxRotation = (Mathf.Sign(Vector3.Dot(wheelVelocity, wheel.forward)) * MaxSpeed / wheelRadius) * Time.fixedDeltaTime * Mathf.Rad2Deg;
        float wheelRotation = 0;

        if (handbrakeInput > 0.1f)
        {
            wheelRotation = 0;
        }
        else if (Mathf.Abs(accelerationInput) > 0.1f)
        {
            wheel.GetChild(0).RotateAround(wheel.position, wheel.right, maxRotation / 2);
            wheelRotation = maxRotation;
        }
        else
        {
            wheel.GetChild(0).RotateAround(wheel.position, wheel.right, minRotation);
            wheelRotation = minRotation;
        }
        wheel.GetChild(0).localPosition = Vector3.zero;
        var rot = wheel.GetChild(0).localRotation;
        rot.y = 0;
        rot.z = 0;
        wheel.GetChild(0).localRotation = rot;

        //wheel slip calculation
        forwardSlip[tireNum] = Mathf.Abs(Mathf.Clamp((wheelRotation - minRotation) / (maxRotation), -1, 1));
        if (WheelIsGrounded)
        {
            skidTotalVisual[tireNum] = Mathf.MoveTowards(skidTotalVisual[tireNum], (forwardSlip[tireNum] + slipCoeff[tireNum]) / 2, 0.05f);
        }
        else
        {
            skidTotalVisual[tireNum] = 0;
        }


    }

    private void Update()
    {
        if (CanDrive && CanAccelerate)
        {
            accelerationInput = inputManager.AccelerationInput;
            steerInput = inputManager.SteerInput;
            handbrakeInput = inputManager.HandbrakeInput;

            // Log only ML-Agent related diagnostics
            if (enableLogging && (Time.frameCount % 300 == 0 || inputManager.isMLAgentControlled)) // Reduced to once per 5 seconds or when ML-Agent controlled
            {
                // Debug.Log($"[MLDiag] Input Source: {(inputManager.isMLAgentControlled ? "ML-Agent" : "Human")}, Override: {inputManager.overrideInputs}");
                // Debug.Log($"[MLDiag] Inputs - Steer: {steerInput:F2}, Accel: {accelerationInput:F2}, Brake: {handbrakeInput:F2}");

                if (inputManager.isMLAgentControlled)
                {
                    // Debug.Log($"[MLDiag] Vehicle State - Velocity: {rb.linearVelocity.magnitude:F2}m/s, Grounded: {vehicleIsGrounded}, Wheels: {NumberOfGroundedWheels}/4");
                }
            }
        }
        else if (CanDrive && !CanAccelerate)
        {
            accelerationInput = 0;
            steerInput = inputManager.SteerInput;
            handbrakeInput = inputManager.HandbrakeInput;

            if (enableLogging && inputManager.isMLAgentControlled && Time.time - lastLogTime > logFrequency)
            {
                // Debug.Log($"[MLDiag] ML-Agent CanAccelerate=false - Forcing Accel=0");
                lastLogTime = Time.time;
            }
        }
        else
        {
            accelerationInput = 0;
            steerInput = 0;
            handbrakeInput = 1;

            if (enableLogging && inputManager.isMLAgentControlled && Time.time - lastLogTime > logFrequency)
            {
                // Debug.Log($"[MLDiag] ML-Agent CanDrive=false - Forcing all inputs to zero/brake");
                lastLogTime = Time.time;
            }
        }
    }

    //private void CalculateEngineRPM()
    //{
    //    // Calculate engine RPM based on vehicle speed (simplified example)
    //    float speedRatio = localVehicleVelocity.z / MaxSpeed;
    //    engineRPM = Mathf.Lerp(minEngineRPM, maxEngineRPM, speedRatio);
    //
    //    // Apply engine inertia and friction
    //    float engineTorque = engineTorqueCurve.Evaluate(engineRPM) * accelerationInput; // Assuming you have a torque curve
    //    float frictionTorque = engineRPM / maxEngineRPM * engineFrictionFactor;
    //    float netEngineTorque = engineTorque - frictionTorque;
    //    float engineAngularAcceleration = netEngineTorque / engineInertia;
    //    engineRPM += engineAngularAcceleration * Time.fixedDeltaTime * 60f; // Convert to RPM
    //
    //    // Clamp engine RPM to min and max values
    //    engineRPM = Mathf.Clamp(engineRPM, minEngineRPM, maxEngineRPM);
    //}

    public bool GroundedProperty
    {
        get
        {
            return tempGroundedProperty;
        }

        set
        {
            if (value != tempGroundedProperty)
            {
                tempGroundedProperty = value;
                if (tempGroundedProperty)
                {
                    // Debug.Log("Grounded");
                    VehicleEvents.OnGrounded.Invoke();
                }
                else
                {
                    // Debug.Log("Take off");
                    VehicleEvents.OnTakeOff.Invoke();
                }
            }


        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (rb != null)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(rb.centerOfMass), 0.1f);
        }
    }

    public void OnGroundedVehicle() 
    {
        VehicleEvents.OnGrounded?.Invoke();
    }

    public void OnVehicleTakeOff()
    {
        VehicleEvents.OnTakeOff?.Invoke();
    }

    /// <summary>
    /// Applies new max speed and acceleration for the player vehicle at runtime.
    /// </summary>
    public void OverridePlayerStats(float maxSpeed, float acceleration)
    {
        usePlayerOverrides = true;
        playerMaxSpeed = maxSpeed;
        playerAcceleration = acceleration;
        MaxSpeed = maxSpeed;
        Acceleration = acceleration;
        Debug.Log($"[VehicleController] Player overrides applied at runtime: MaxSpeed={MaxSpeed}, Acceleration={Acceleration}");
    }
}