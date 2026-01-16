using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Harp.Equestian;
using Player.Movement;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

//using Unity.VisualScripting.Dependencies.Sqlite;

namespace Harp.ODMLogic
{
    public class PL_ODM : MonoBehaviour
    {
        // =====================================================================================
        // SERIALIZED FIELDS
        // =====================================================================================

        public float currentSpeed; // Private variable to store speed

        [FormerlySerializedAs("_isReeling")] public bool isReeling;
        [FormerlySerializedAs("_isOrbiting")] public bool isOrbiting;

        private bool _isProperlyHooked = true;

        public PlayerMovementSlide slide;
        public PlayerMovementGround ground;
        public PlayerMovementDash dash;
        public PlayerMovementAir air;

        [FormerlySerializedAs("ReelVelocityLerp")] public float reelVelocityLerp = 5f;
        [FormerlySerializedAs("ReelOrbitLerp")] public float reelOrbitLerp = 5f;

        Vector3 _leftDirection;
        Vector3 _rightDirection;

        [Header("Main")]
        public float hookEjectForce = 2; // The speed which the hooks reel in and out to your target
        public float hookCurrentReelInForce = 2;
        public float hookNormalReelInForce = 2;
        public float hookBoostReelInForce = 2;
        public float hookMaxDistance = 100;
        public Transform playerTransform;
        public Transform playerCameraTransform;
        public PlayerMotor player;
        public PlayerLook cameralook;

        [Header("Audio Hook Fire")]
        public List<AudioSource> hookFireAudioSources;
        public List<AudioClip> hookFireAudioClips;

        [Header("Audio Latch and Reel")]
        public List<AudioSource> hookReelAudioSources;
        public List<AudioSource> hooksLatchAudioSources;
        public List<float> targetPitch = new List<float>(new[] { 1f, 1f });
        private float divider;

        [Header("Audio Gas ODM")]
        public AudioSource gasAudioSource;

        [Header("Audio Dash ODM")]
        public AudioSource gasDashAudioSource;

        [Header("Particles Gas ODM")]
        public ParticleSystem gasParticles;

        [Header("Particle Dash ODM")]
        public ParticleSystem gasDashParticles;

        [Header("Logic Gas ODM")]
        public float maximumGasAmount = 1500;
        public float currentGasAmount = 1000;
        public float gasForce = 15;
        public float swingForce = 5;
        public bool isUsingGas;

        [Header("Logic Hook Separation")]
        public float separation = 0;
        public float maxAngle = 45f;
        public float currentSeparation = 0.2f;

        [Header("Logic Dash")]
        public float dashTimer = 1f;
        public float gasDashForce = 15f;
        public float dashCooldown = 1f;

        [Header("Boost Logic")]
        [SerializeField] private float doubleTapThreshold = 0.3f;
        [SerializeField] private float boostImpulseForce = 10f;

        [Header("Logic Hook Fire")]
        public List<bool> hooksReady = new List<bool>(new bool[] { true, true });
        public List<float> hookCooldownTimes = new List<float>(new float[] { 0.5f, 0.5f });
        public float hookFireCooldownTimeBase = 0.5f;
        public List<int> reelingInOutState = new List<int>(new int[] { 3, 3 });
        public List<Vector3> hookSwingPoints = new List<Vector3>(new Vector3[] { Vector3.zero, Vector3.zero });
        public List<Vector3> hookPositions = new List<Vector3>(new Vector3[] { Vector3.zero, Vector3.zero });
        public List<Transform> hookStartTransforms = new List<Transform>(new Transform[] { null, null });
        public List<SpringJoint> hookJoints = new List<SpringJoint>(new SpringJoint[] { null, null });

        [Header("Hook Fire Visual")]
        public List<LineRenderer> hookWireRenderers = new List<LineRenderer>(new LineRenderer[] { null, null });

        [Header("Hook Visual")]
        public GameObject hookPrefab; // Assign your HookTipPrefab here
        private List<GameObject> _hookVisuals = new List<GameObject>(new GameObject[] { null, null }); // Tracks instantiated visuals per hook

        [Header("Logic Prediction")]
        public LayerMask grappleSurfaces;
        public LayerMask nonGrappleSurfaces;
        public float predictionSeparationNegation = 8f;
        public float predictionSphereRadius = 15f;
        private List<RaycastHit> _hookPredictionHits = new List<RaycastHit>(new RaycastHit[] { new RaycastHit(), new RaycastHit() });

        [Header("UI Prediction")]
        public List<UnityEngine.UI.Image> hookCrosshairs;
        public List<UnityEngine.UI.Image> hookStaticCrosshairs;

        [Header("UI Gas")]
        public UnityEngine.UI.Image gasUI;

        [FormerlySerializedAs("SpeedText")]
        [Header("UI Speed")]
        public TMP_Text speedText;

        [Header("UI Dash Cooldown")]
        public UnityEngine.UI.Image dashCooldownImage;

        // Private fields for boost logic
        private float _lastSpacePressTime = -Mathf.Infinity;
        private bool _isBoosting;
        private bool _wasBoosting;

        // =====================================================================================
        // UNITY LIFECYCLE METHODS
        // =====================================================================================

        void Update()
        {
            currentSpeed = Mathf.Ceil(player.Rigidbody.velocity.magnitude);
            speedText.text = currentSpeed.ToString(CultureInfo.CurrentCulture) + " m/s";

            UpdateCooldownTimers();
            UpdateDashTimers();
            UpdateDashUI();

            CheckInputUpdate();

            ReelingSounds(0);
            ReelingSounds(1);
        }

        private void UpdateGasUI()
        {
            gasUI.fillAmount = currentGasAmount / maximumGasAmount;
        }

        private void UpdateDashUI()
        {
            if (dashCooldownImage != null && currentGasAmount > 0f)
            {
                if (dashTimer > 0f)
                {
                    dashCooldownImage.fillAmount = dashTimer / dashCooldown;
                    dashCooldownImage.gameObject.SetActive(true);
                }
                else
                {
                    dashCooldownImage.gameObject.SetActive(false);
                }
            }
        }

        void FixedUpdate()
        {
            OrbitInputUpdate();

            PredictGrappleSpot(0);
            PredictGrappleSpot(1);

            CheckInputFixed();
            UpdateGasUI();

            CheckHookLocked(0);
            CheckHookLocked(1);

            UpdateSpringSettings(0);
            UpdateSpringSettings(1);

            HandleReelRotation();

            // Force unhook if out of gas
            if (currentGasAmount <= 0)
            {
                if (!hooksReady[0]) StopHook(0);
                if (!hooksReady[1]) StopHook(1);
            }

            // Handle boost impulse
            HandleBoostImpulse();
        }

        // =====================================================================================
        // TIMER UPDATE METHODS
        // =====================================================================================

        void UpdateCooldownTimers()
        {
            if (hookCooldownTimes[0] > 0) hookCooldownTimes[0] -= Time.deltaTime;
            if (hookCooldownTimes[1] > 0) hookCooldownTimes[1] -= Time.deltaTime;
        }

        void UpdateDashTimers()
        {
            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
            }
        }

        // =====================================================================================
        // PREDICTION METHODS
        // =====================================================================================

        void PredictGrappleSpot(int hookIndex)
        {
            if (!hooksReady[hookIndex])
            {
                hookCrosshairs[hookIndex].rectTransform.position = playerCameraTransform.gameObject.GetComponent<Camera>().WorldToScreenPoint(hookPositions[hookIndex]);
            }

            //Check if the hooks are ready
            if (!hooksReady[hookIndex]) return;

            RaycastHit spherecastHit = new RaycastHit();
            RaycastHit raycastHit = new RaycastHit();
            Vector3 realHitVector = Vector3.zero;

            float currentAngle = Mathf.Lerp(-maxAngle, maxAngle, (currentSeparation + 1) / 2); // Map separation to angle

            Vector3 leftDirection = Quaternion.AngleAxis(-currentAngle, playerCameraTransform.up) * playerCameraTransform.forward;
            Vector3 rightDirection = Quaternion.AngleAxis(currentAngle, playerCameraTransform.up) * playerCameraTransform.forward;

            // Shoot hooks based on the current index
            if (hookIndex == 0) // Left hook
            {
                Physics.SphereCast(playerCameraTransform.position, predictionSphereRadius, leftDirection, out spherecastHit, hookMaxDistance, grappleSurfaces);
                Physics.Raycast(playerCameraTransform.position, leftDirection, out raycastHit, hookMaxDistance, grappleSurfaces);
            }
            else if (hookIndex == 1) // Right hook
            {
                Physics.SphereCast(playerCameraTransform.position, predictionSphereRadius, rightDirection, out spherecastHit, hookMaxDistance, grappleSurfaces);
                Physics.Raycast(playerCameraTransform.position, rightDirection, out raycastHit, hookMaxDistance, grappleSurfaces);
            }

            //if raycast hit anything
            if (raycastHit.collider != null)
            {
                realHitVector = Physics.Linecast(playerCameraTransform.position, raycastHit.point, out RaycastHit hit, nonGrappleSurfaces) ? Vector3.zero : raycastHit.point;
            }

            //if raycast hit nothing
            else if (spherecastHit.collider != null)
            {
                realHitVector = Physics.Linecast(playerCameraTransform.position, spherecastHit.point, out RaycastHit hit, nonGrappleSurfaces) ? Vector3.zero : spherecastHit.point;
            }

            //if either of the hits hit anything
            if (realHitVector != Vector3.zero)
            {
                _hookPredictionHits[hookIndex] = raycastHit.point == Vector3.zero ? spherecastHit : raycastHit;

                hookCrosshairs[hookIndex].gameObject.SetActive(true);
                hookCrosshairs[hookIndex].rectTransform.position = playerCameraTransform.gameObject.GetComponent<Camera>().WorldToScreenPoint(_hookPredictionHits[hookIndex].point);
            }

            //if either of the hits hit nothing
            else
            {
                RaycastHit tempHit = new RaycastHit();
                tempHit.point = Vector3.zero;
                _hookPredictionHits[hookIndex] = tempHit;
                hookCrosshairs[hookIndex].gameObject.SetActive(false);
            }

            hookStaticCrosshairs[0].rectTransform.position = playerCameraTransform.gameObject.GetComponent<Camera>().WorldToScreenPoint(playerCameraTransform.position + leftDirection);
            hookStaticCrosshairs[1].rectTransform.position = playerCameraTransform.gameObject.GetComponent<Camera>().WorldToScreenPoint(playerCameraTransform.position + rightDirection);
        }

        // =====================================================================================
        // UTILITY METHODS
        // =====================================================================================

        private static float MapToRange(float value, float minInput, float maxInput, float minOutput, float maxOutput)
        {
            //Debug.Log("CONVERTING");

            // Ensure the value is clamped within the input range
            value = Mathf.Clamp(value, minInput, maxInput);

            // Calculate the ratio of the value's position within the input range
            float ratio = (value - minInput) / (maxInput - minInput);

            // Map the ratio to the output range
            float mappedValue = minOutput + ratio * (maxOutput - minOutput);

            // Return the mapped value
            return mappedValue;
        }

        // =====================================================================================
        // SPRING JOINT UPDATE METHODS
        // =====================================================================================

        void UpdateSpringSettings(int hookIndex)
        {
            if (hooksReady[hookIndex] || !hookJoints[hookIndex])
            {
                return;
            }

            if (Vector3.Distance(player.Rigidbody.transform.position, hookSwingPoints[hookIndex]) >= 5f && !isReeling)
            {
                hookJoints[hookIndex].tolerance = 0.025f;
                hookJoints[hookIndex].spring = MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 300, 7.5f, 20f);
                hookJoints[hookIndex].damper = MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 50, 2.5f, 10f);
                hookJoints[hookIndex].massScale = MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 50, 4.5f, 2f);
            }
            else
            {
                hookJoints[hookIndex].tolerance = 0f;
                hookJoints[hookIndex].spring = 0;
                hookJoints[hookIndex].damper = 0;
                hookJoints[hookIndex].massScale = 0f;
            }
        }

        // =====================================================================================
        // INPUT HANDLING METHODS (UPDATE)
        // =====================================================================================

        void CheckInputUpdate()
        {
            // Boost detection (double tap Space to enable boosting while held)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.time - _lastSpacePressTime < doubleTapThreshold)
                {
                    _isBoosting = true;
                }
                _lastSpacePressTime = Time.time;
            }

            if (Input.GetKeyUp(KeyCode.Space))
            {
                _isBoosting = false;
            }

            // Adjust separation
            if (Input.GetAxis("Mouse ScrollWheel") > 0f)
            {
                separation = Mathf.Clamp(separation + 0.1f, 0f, 1f);
            }
            else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
            {
                separation = Mathf.Clamp(separation - 0.1f, 0f, 1f);
            }

            // Smooth separation
            if (currentSeparation < 0.9f)
            {
                currentSeparation = Mathf.Lerp(currentSeparation, separation, 8 * Time.deltaTime);
            }
            else if (currentSeparation > 0.1f)
            {
                currentSeparation = Mathf.Lerp(currentSeparation, separation, 8 * Time.deltaTime);
            }
            else if (currentSeparation >= 0.9f)
            {
                currentSeparation = 1;
            }
            else if (currentSeparation <= 0.1f)
            {
                currentSeparation = 0;
            }

            // Hook fire detection
            if (Input.GetKeyDown(KeyCode.Mouse0) && hookCooldownTimes[0] <= 0 && hooksReady[0] && _hookPredictionHits[0].point != Vector3.zero && currentGasAmount > 0)
            {
                FireHook(0);
            }
            else if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                StopHook(0);
            }

            if (Input.GetKeyDown(KeyCode.Mouse1) && hookCooldownTimes[1] <= 0 && hooksReady[1] && _hookPredictionHits[1].point != Vector3.zero && currentGasAmount > 0)
            {
                FireHook(1);
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1))
            {
                StopHook(1);
            }

            // Dashing
            if (!isOrbiting && !isReeling && !player.IsGrounded && Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (dashTimer <= 0f)
                {
                    PerformAirDash(player.GetWishDir());
                    dashTimer = dashCooldown;
                }
            }
        }

        // =====================================================================================
        // DASH AND ORBIT INPUT METHODS
        // =====================================================================================

        void OrbitInputUpdate()
        {
            //Orbiting
          
                if (!hooksReady[0] || !hooksReady[1])
                {
                    isOrbiting = false;

                    if (Input.GetKey(KeyCode.W) && isReeling)//UP
                    {
                        HandleOrbitNoDoubleTap(3);
                        isOrbiting = true;
                    }

                    if (Input.GetKey(KeyCode.S) && isReeling)//DOWN
                    {
                        HandleOrbitNoDoubleTap(2);
                        isOrbiting = true;
                    }

                    if (Input.GetKey(KeyCode.A) && isReeling)//LEFt
                    {
                        HandleOrbitNoDoubleTap(1);
                        isOrbiting = true;
                    }

                    if (Input.GetKey(KeyCode.D) && isReeling)//RIGHT
                    {
                        HandleOrbitNoDoubleTap(0);
                        isOrbiting = true;
                    }
                
            }
        }

        void HandleOrbitNoDoubleTap(int buttonIndex)
        {
            PerformOrbit(buttonIndex);
            //dashTimer = dashCooldown;
        }

        Vector3 GetOrbitCenter()
        {
            if (hookJoints[0] && hookJoints[1])
            {
                return (hookSwingPoints[0] + hookSwingPoints[1]) / 2f;
            }
            else if (hookJoints[0])
            {
                return hookSwingPoints[0];
            }
            else if (hookJoints[1])
            {
                return hookSwingPoints[1];
            }

            return Vector3.zero; // Fallback to zero if no hooks are active
        }

        IEnumerator OrbitVelocityChange()
        {
            Vector3 previousVelocity = player.Rigidbody.velocity; // Store current velocity
            Vector3 currentVelocity = player.Rigidbody.velocity.normalized;
            Vector3.Lerp(previousVelocity, currentVelocity, Time.deltaTime * reelOrbitLerp);
            // player.Rigidbody.velocity = newVelocity; // Apply smooth transition

            yield break;
        }

        void PerformOrbit(int buttonIndex)
        {
            if (currentGasAmount < 0) return;

            Vector3 orbitCenter = GetOrbitCenter();
            Vector3 playerPos = playerTransform.position;
            Vector3 toPlayer = playerPos - orbitCenter;
            Vector3 orbitAxis = Vector3.up; // Orbit in the horizontal plane
            Vector3 tangent;

            switch (buttonIndex)
            {
                case 0: // RIGHT Orbit (clockwise around vertical axis)
                    StartCoroutine(OrbitVelocityChange());
                    tangent = Vector3.Cross(toPlayer, orbitAxis).normalized; // Right-hand rule: clockwise when viewed from above
                    player.Rigidbody.AddForce(tangent * gasDashForce / 20f, ForceMode.VelocityChange);
                    break;

                case 1: // LEFT Orbit (counter-clockwise around vertical axis)
                    StartCoroutine(OrbitVelocityChange());
                    tangent = Vector3.Cross(orbitAxis, toPlayer).normalized; // Counter-clockwise
                    player.Rigidbody.AddForce(tangent * gasDashForce / 20f, ForceMode.VelocityChange);
                    break;

                case 2: // DOWN Orbit (clockwise around camera right axis)
                    StartCoroutine(OrbitVelocityChange());
                    orbitAxis = playerCameraTransform.right; // Orbit around camera's right axis
                    tangent = Vector3.Cross(toPlayer, orbitAxis).normalized; // Clockwise when viewed along right axis
                    player.Rigidbody.AddForce(tangent * gasDashForce / 20f, ForceMode.VelocityChange);
                    break;

                case 3: // UP Orbit (counter-clockwise around camera right axis)
                    StartCoroutine(OrbitVelocityChange());
                    orbitAxis = playerCameraTransform.right;
                    tangent = Vector3.Cross(orbitAxis, toPlayer).normalized; // Counter-clockwise
                    player.Rigidbody.AddForce(tangent * gasDashForce / 20f, ForceMode.VelocityChange);
                    break;
            }

            currentGasAmount -= gasDashForce / 100;
            gasDashParticles.Emit(120);
            gasDashParticles.Play();
            //gasDashAudioSource.Play();//this needs to be changed to an orbit sound effect instead of reusing reel boost
        }

        void PerformAirDash(Vector3 wishDir)
        {
            if (currentGasAmount < 0) return;

            Vector3 dashDir;
            float strength;

            if (wishDir.magnitude < 0.1f)
            {
                // No movement input held: Dodge UP (weaker than horizontal for balance)
                dashDir = player.Rigidbody.transform.up;
                strength = gasDashForce / 1.4f;
            }
            else
            {
                // Movement input(s) held: Dodge horizontally in camera-relative wish direction (supports diagonals naturally)
                dashDir = new Vector3(wishDir.x, 0f, wishDir.z).normalized;
                strength = gasDashForce * 2f;
            }

            player.Rigidbody.AddForce(dashDir * strength, ForceMode.VelocityChange);

            currentGasAmount -= gasDashForce / 100f;

            gasDashParticles.Emit(120);
            gasDashParticles.Play();
            // gasDashAudioSource.Play();
        }

        // =====================================================================================
        // INPUT HANDLING METHODS (FIXED UPDATE)
        // =====================================================================================

        void CheckInputFixed()
        {
            // Gas usage
            if (Input.GetKey(KeyCode.LeftShift) && player.IsGrounded == false && !isReeling)
            {
                if (currentGasAmount < 0) return;
            }
            else if (isUsingGas)
            {
                StopUseGas(swingForce);
                isUsingGas = false;
            }

            // Hook reeling
            if (_isProperlyHooked)
            {
                if (hookJoints[0])
                {
                    ReelInHook(0);
                    isReeling = true;
                }

                if (hookJoints[1])
                {
                    ReelInHook(1);
                    isReeling = true;
                }
            }
            else
            {
                isReeling = false;
            }
        }

        // =====================================================================================
        // BOOST IMPULSE METHODS
        // =====================================================================================

        private void HandleBoostImpulse()
        {
            if (_isBoosting && !_wasBoosting)
            {
                if (currentGasAmount <= 0)
                {
                    _isBoosting = false;
                    return;
                }

                Vector3 boostDir;

                if (_isProperlyHooked)
                {
                    Vector3 targetPoint = Vector3.zero;
                    int activeHooks = 0;

                    if (hookJoints[0])
                    {
                        targetPoint += hookSwingPoints[0];
                        activeHooks++;
                    }

                    if (hookJoints[1])
                    {
                        targetPoint += hookSwingPoints[1];
                        activeHooks++;
                    }

                    if (activeHooks > 0)
                    {
                        targetPoint /= activeHooks;
                        boostDir = (targetPoint - playerTransform.position).normalized;
                    }
                    else
                    {
                        boostDir = playerCameraTransform.forward.normalized;
                    }

                    player.Rigidbody.AddForce(boostDir * boostImpulseForce, ForceMode.Impulse);
                    currentGasAmount -= gasDashForce / 100f;
                    gasDashParticles.Emit(120);
                    gasDashParticles.Play();
                    // gasDashAudioSource.Play(); // Uncomment if desired
                }
                else
                {
                    if (dashTimer > 0f)
                    {
                        return; // Cooldown active, skip the dash
                    }

                    player.CurrentState = dash;
                    cameralook.FovBurst(15);
                    boostDir = playerCameraTransform.forward.normalized;
                    UseGas(100);
                    player.Rigidbody.AddForce(boostDir * boostImpulseForce, ForceMode.Impulse);
                    currentGasAmount -= gasDashForce / 100f;
                    gasDashParticles.Emit(120);
                    gasDashParticles.Play();
                    // gasDashAudioSource.Play(); // Uncomment if desired

                    dashTimer = dashCooldown; // Start cooldown
                }

                _wasBoosting = true;
            }
            else if (!_isBoosting && _wasBoosting)
            {
                _wasBoosting = false;
            }
        }

        // =====================================================================================
        // REELING METHODS
        // =====================================================================================

        void ReelInHook(int hookIndex)
        {
            cameralook.FovBurst(0.12f);

            //if grounded and reeling, force slide state
            if (player.IsGrounded)
            {
                player.Rigidbody.AddForce(player.Rigidbody.transform.up * (gasDashForce * 0.5f), ForceMode.Force);
            }

            float targetReelInForce;

            if (_isProperlyHooked && _isBoosting && !isOrbiting)
            {
                targetReelInForce = hookBoostReelInForce;
                StartOdmGasVFX();
            }
            else
            {
                targetReelInForce = hookNormalReelInForce;
            }

            hookCurrentReelInForce = targetReelInForce;

            if (currentGasAmount <= 0) return;

            Vector3 previousVelocity = player.Rigidbody.velocity; //store current velocity

            if (!isReeling)
            {
                player.Rigidbody.AddForce(player.Rigidbody.transform.up * 0.1f, ForceMode.Impulse);
            }

            float distanceFromPoint = Vector3.Distance(transform.position, hookSwingPoints[hookIndex]);

            //float targetMaxDistance = Mathf.Max(0.1f, distanceFromPoint * 0.7f); REDUNDANT OPERATOR

            if (distanceFromPoint > 0.0f)
            {
                //This handles the players vertical and horizontal velocity change upon entering a new reel from previous velocity
                divider = Mathf.Lerp(divider, PL_ResourceManagement.MapToRange(distanceFromPoint, 0, hookMaxDistance, 0.1f, 0.01f), Time.deltaTime * 4f);
                Vector3 reelForceBasedOnDistance = (hookSwingPoints[hookIndex] - transform.position).normalized * (hookCurrentReelInForce * divider);
                Vector3 newVelocity = Vector3.Lerp(previousVelocity, reelForceBasedOnDistance, Time.deltaTime * reelVelocityLerp); // Smoothly transition[The greater the f value the stronger the lerp]

                player.Rigidbody.velocity = newVelocity; // Apply smooth transition
                player.Rigidbody.AddForce(player.Rigidbody.transform.up * 0.1f, ForceMode.VelocityChange);
            }
            else Debug.Log("hook too short");

            currentGasAmount -= 0.1f;
        }

        // =====================================================================================
        // AUDIO METHODS
        // =====================================================================================

        void ReelingSounds(int hookIndex)
        {
            if (reelingInOutState[hookIndex] == 0)
            {
                if (hookSwingPoints[hookIndex] == Vector3.zero) return;

                targetPitch[hookIndex] = PL_ResourceManagement.MapToRange(Vector3.Distance(hookStartTransforms[hookIndex].position, hookPositions[hookIndex]), 0, 100, 0.8f, 1.5f);

                hookReelAudioSources[hookIndex].volume = Mathf.Lerp(hookReelAudioSources[hookIndex].volume, 0.4f, Time.deltaTime * (hookEjectForce * 4));
                hookReelAudioSources[hookIndex].pitch = Mathf.Lerp(hookReelAudioSources[hookIndex].pitch, targetPitch[hookIndex], Time.deltaTime * (hookEjectForce * 4));
            }
            else if (reelingInOutState[hookIndex] == 1)
            {
                if (hookSwingPoints[hookIndex] == Vector3.zero) return;

                Vector3 velocity = player.Rigidbody.velocity;
                float speedInHookAxis = Vector3.Dot(velocity, (hookStartTransforms[hookIndex].position - hookPositions[hookIndex]));

                targetPitch[hookIndex] = PL_ResourceManagement.MapToRange(Mathf.Abs(speedInHookAxis), 0, 200, 0.8f, 1.5f);

                hookReelAudioSources[hookIndex].volume = Mathf.Lerp(hookReelAudioSources[hookIndex].volume, PL_ResourceManagement.MapToRange(Mathf.Abs(speedInHookAxis), 0, 100, 0f, 0.4f), Time.deltaTime * (hookEjectForce * 4));
                hookReelAudioSources[hookIndex].pitch = Mathf.Lerp(hookReelAudioSources[hookIndex].pitch, targetPitch[hookIndex], Time.deltaTime * (hookEjectForce * 4));
            }
            else if (reelingInOutState[hookIndex] == 2)
            {
                if (hookSwingPoints[hookIndex] == Vector3.zero) return;

                targetPitch[hookIndex] = PL_ResourceManagement.MapToRange(Vector3.Distance(hookStartTransforms[hookIndex].position, hookPositions[hookIndex]), 0, 100, 0.8f, 1.5f);

                hookReelAudioSources[hookIndex].volume = Mathf.Lerp(hookReelAudioSources[hookIndex].volume, 0.4f, Time.deltaTime * (hookEjectForce * 4));
                hookReelAudioSources[hookIndex].pitch = Mathf.Lerp(hookReelAudioSources[hookIndex].pitch, targetPitch[hookIndex], Time.deltaTime * (hookEjectForce * 4));
            }
            else if (reelingInOutState[hookIndex] == 3)
            {
                hookReelAudioSources[hookIndex].volume = Mathf.Lerp(hookReelAudioSources[hookIndex].volume, 0, Time.deltaTime * (hookEjectForce * 10));
            }
        }

        // =====================================================================================
        // GAS USAGE METHODS
        // =====================================================================================

        void UseGas(float amountDepleted)
        {
            if (currentGasAmount <= 0) return;

            currentGasAmount -= amountDepleted;

            if (currentGasAmount < 0) currentGasAmount = 0;

            isUsingGas = true;
        }

        void StopUseGas(float force)
        {
            gasAudioSource.volume = Mathf.Lerp(gasAudioSource.volume, 0, Time.deltaTime * 8f);

            Vector3 wishDir = player.GetWishDir();
            Vector3 moveDirection = wishDir;
            moveDirection.y = 0f;
            player.Rigidbody.AddForce(moveDirection * force, ForceMode.Acceleration);
        }

        void StartOdmGasVFX()
        {
            if (currentGasAmount > 0)
            {
                gasParticles.Emit(20);
                gasParticles.Play();
            }
            else
            {
                gasParticles.Stop();
                gasAudioSource.volume = Mathf.Lerp(gasAudioSource.volume, 0, Time.deltaTime * 8f);
            }
        }

        // =====================================================================================
        // HOOK FIRING METHODS
        // =====================================================================================

        void FireHook(int hookIndex)
        {
            PlayHookFireSound(hookFireAudioSources[hookIndex], hookFireAudioClips[0]);

            hookCooldownTimes[hookIndex] = hookFireCooldownTimeBase;
            reelingInOutState[hookIndex] = 0;

            if (_hookPredictionHits[hookIndex].point == Vector3.zero) return;

            hookSwingPoints[hookIndex] = _hookPredictionHits[hookIndex].point;
            hookPositions[hookIndex] = hookStartTransforms[hookIndex].position;

            float distanceFromPoint = Vector3.Distance(playerTransform.position, hookSwingPoints[hookIndex]);

            hookJoints[hookIndex] = playerTransform.gameObject.AddComponent<SpringJoint>();
            hookJoints[hookIndex].autoConfigureConnectedAnchor = false;
            hookJoints[hookIndex].connectedAnchor = hookSwingPoints[hookIndex];
            hookJoints[hookIndex].spring = 0;
            hookJoints[hookIndex].damper = 0;
            hookJoints[hookIndex].massScale = 0;
            hookJoints[hookIndex].maxDistance = distanceFromPoint;
            hookJoints[hookIndex].minDistance = 0;

            StartCoroutine(LaunchAndAttachHook(hookIndex, distanceFromPoint));
        }

        void PlayHookFireSound(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.pitch = Random.Range(0.85f, 1.25f);
            source.Play();
        }

        void PlayerJumpUpOnHookShot()
        {
            if (currentGasAmount > 0 && player.IsGrounded == false)
            {
                player.Rigidbody.AddForce(player.Rigidbody.transform.up * gasDashForce / 2f, ForceMode.VelocityChange);

                //refresh dubleJump
                if (!player.IsGrounded == false)
                {
                }
            }
        }

        void CheckHookLocked(int hookIndex)
        {
            _isProperlyHooked = (reelingInOutState[0] == 1 || reelingInOutState[1] == 1);
        }

        IEnumerator LaunchAndAttachHook(int hookIndex, float distanceToPoint)
        {
            PlayerJumpUpOnHookShot();

            if (!hookJoints[hookIndex])
            {
                StopHook(hookIndex);
                yield break;
            }

            Vector3 initialPosition = hookStartTransforms[hookIndex].position;
            Vector3 targetPosition = hookSwingPoints[hookIndex];
            float distance = Vector3.Distance(initialPosition, targetPosition);
            float travelTime = distance / hookEjectForce;
            float startTime = Time.time;

            while (true)
            {
                float elapsedTime = Time.time - startTime;
                float t = Mathf.Clamp01(elapsedTime / travelTime);
                Vector3 currentPosition = Vector3.Lerp(initialPosition, targetPosition, t);

                float currentDistance = Vector3.Distance(hookStartTransforms[hookIndex].position, hookSwingPoints[hookIndex]);

                if (currentDistance > hookMaxDistance)
                {
                    StopHook(hookIndex);
                    yield break;
                }

                hookPositions[hookIndex] = currentPosition;

                if (t >= 1f) break;

                yield return null;
            }

            reelingInOutState[hookIndex] = 1;

            if (hookJoints[hookIndex] && !hooksLatchAudioSources[hookIndex].isPlaying)
            {
                hooksReady[hookIndex] = false;
                hookJoints[hookIndex].maxDistance = distanceToPoint * 0.9f;

                hooksLatchAudioSources[hookIndex].gameObject.transform.position = hookSwingPoints[hookIndex];
                hooksLatchAudioSources[hookIndex].pitch = Random.Range(0.8f, 1.2f);
                hooksLatchAudioSources[hookIndex].Play();

                // Spawn visual hook tip at attachment point
                if (_hookVisuals[hookIndex] != null)
                {
                    Destroy(_hookVisuals[hookIndex]);
                }

                _hookVisuals[hookIndex] = Instantiate(hookPrefab, hookSwingPoints[hookIndex], Quaternion.LookRotation(playerTransform.position - hookSwingPoints[hookIndex]));

                hookJoints[hookIndex].spring = PL_ResourceManagement.MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 300, 7.5f, 20f);
                hookJoints[hookIndex].damper = PL_ResourceManagement.MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 50, 2.5f, 10f);
                hookJoints[hookIndex].massScale = PL_ResourceManagement.MapToRange(player.Rigidbody.velocity.sqrMagnitude, 1, 50, 4.5f, 2f);

                hookSwingPoints[hookIndex] = hookSwingPoints[hookIndex];
            }
        }

        void StopHook(int hookIndex)
        {
            reelingInOutState[hookIndex] = 3;

            Destroy(hookJoints[hookIndex]);

            // Destroy visual hook tip
            if (_hookVisuals[hookIndex] != null)
            {
                Destroy(_hookVisuals[hookIndex]);
                _hookVisuals[hookIndex] = null;
            }
        }

        // =====================================================================================
        // PLAYER ORIENTATION/ROTATION METHODS
        // =====================================================================================

        private void HandleReelRotation()
        {
            if (!isReeling) return;

            Vector3 targetPoint = Vector3.zero;
            int activeHooks = 0;

            if (hookJoints[0])
            {
                targetPoint += hookSwingPoints[0];
                activeHooks++;
            }

            if (hookJoints[1])
            {
                targetPoint += hookSwingPoints[1];
                activeHooks++;
            }

            if (activeHooks == 0) return; // No hooks, no rotation

            targetPoint /= activeHooks; // Average for dual hooks

            Vector3 dirToTarget = (targetPoint - playerTransform.position).normalized;
            Vector3 flatDir = new Vector3(dirToTarget.x, 0f, dirToTarget.z).normalized;

            if (flatDir.magnitude < 0.1f) return; // Too close or vertical, skip

            Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
            player.Rigidbody.MoveRotation(Quaternion.Slerp(player.Rigidbody.rotation, targetRot, player.GroundRotationSpeed * Time.fixedDeltaTime));
        }

        // =====================================================================================
        // UTILITY / CLEANUP METHODS
        // =====================================================================================

        public void KillPlayer()
        {
            StopHook(0);
            StopHook(1);

            gasAudioSource.Stop();
            gasDashAudioSource.Stop();

            hookFireAudioSources[0].Stop();
            hookFireAudioSources[1].Stop();

            hookReelAudioSources[0].Stop();
            hookReelAudioSources[1].Stop();

            hooksLatchAudioSources[0].Stop();
            hooksLatchAudioSources[1].Stop();

            if (_hookVisuals[0] != null) Destroy(_hookVisuals[0]);
            if (_hookVisuals[1] != null) Destroy(_hookVisuals[1]);

            gasParticles.Stop();
            gasDashParticles.Stop();

            this.enabled = false;
        }
    }
}