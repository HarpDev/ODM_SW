using SwiftKraft.Utils;
using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Player.Movement
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMotor : MonoBehaviour
    {
        public delegate void OnPlayerGroundedChanged(bool curr, bool prev);
        public PlayerMovementStateBase DefaultState;
        public PlayerMovementStateBase SlideState;
      
        public PlayerMovementStateBase CurrentState
        {
            get
            {
                if (state == null)
                {
                    state = DefaultState;
                    state.StateStarted(this);
                }
                return state;
            }
            set
            {
                if (state == value || (value == null && state == DefaultState))
                    return;
                if (state != null)
                    state.StateEnded(this);
                state = value != null ? value : DefaultState;
                state.StateStarted(this);
            }
        }
        public Rigidbody Rigidbody
        {
            get;
            private set;
        }
        public CapsuleCollider Collider
        {
            get;
            private set;
        }
        public bool IsGrounded
        {
            get => isGrounded;
            protected set
            {
                if (isGrounded == value)
                    return;
                bool prev = isGrounded;
                isGrounded = value;
                CurrentState.GroundedChanged(this, value, prev);
                OnGroundedChanged?.Invoke(value, prev);
            }
        }
        public float Height
        {
            get => Collider.height;
            set
            {
                Collider.height = value;
                Collider.center = new(0f, value / 2f, 0f);
            }
        }
        public float CameraHeight
        {
            get => CameraRoot.localPosition.y;
            protected set
            {
                CameraRoot.localPosition = new(0f, value, 0f);
            }
        }
        public float ViableHeight
        {
            get
            {
                if (Physics.Raycast(GroundPoint.position + (Vector3.up * 0.01f), Vector3.up, out RaycastHit _hit, Mathf.Infinity, GroundLayers, QueryTriggerInteraction.Ignore))
                    return _hit.distance;
                return Mathf.Infinity;
            }
        }
        public Timer RecentJumpTimer;
        public GameObject GroundObject { get; private set; }
        [HideInInspector]
        public float TargetCameraHeight;
        [HideInInspector]
        public float slideBoostTimer;
        public float OriginalCameraHeight = 1.7f;
        public float CameraSmoothTime = 0.1f;
        public OnPlayerGroundedChanged OnGroundedChanged;
        public Transform CameraRoot;
        public Transform GroundPoint;
        public float GroundRadius;
        public LayerMask GroundLayers;
        public AudioSource[] Audio;
        public ParticleSystem[] Particles;
        public float currentSpeed;

        [Header("Third Person Orientation")]
        public Transform Camera;  
        public float RotationSpeed = 12f;  
        public float RotationSpeedAirborneMultiplier = 0.5f;
        public bool RotateOnlyWhenGrounded = true;  // Prevent mid-air spinning 
        [SerializeField]
        private PL_ODM ODM;

        PlayerMovementStateBase state;
        bool isGrounded;
        float cameraVel;
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Collider = GetComponent<CapsuleCollider>();
          
            TargetCameraHeight = OriginalCameraHeight;
            if (DefaultState == null)
                enabled = false;

            if (Camera == null)
            {
                
                if (Camera == null)
                    Camera = CameraRoot.GetComponentInChildren<Camera>()?.transform;
                
                if (Camera == null)
                    Debug.LogWarning("PlayerMotor: No Camera assigned or found! Movement will be broken.");
            }
        }
        private void Start()
        {
            StartCoroutine(UpdateSpeed());
        }
        private IEnumerator UpdateSpeed()
        {
            while (true) // This will run indefinitely
            {
                currentSpeed = Mathf.Ceil(Rigidbody.velocity.magnitude);
                yield return new WaitForSeconds(1f); // Wait for 1 second
            }
        }
     
        private void Update()
        {
            CameraHeight = Mathf.SmoothDamp(CameraHeight, TargetCameraHeight, ref cameraVel, CameraSmoothTime);
            CurrentState.InputUpdate(this);

            if (Input.GetMouseButtonDown(1))
            {
                StartCoroutine(HandleAiming());
            }
        }
        private void FixedUpdate()
        {
            RecentJumpTimer.Tick(Time.fixedDeltaTime);
            Collider[] cols = Physics.OverlapSphere(GroundPoint.position, GroundRadius, GroundLayers).OrderBy(c => (c.transform.position - c.transform.position).sqrMagnitude).ToArray();
            IsGrounded = cols.Length > 0;
            GroundObject = IsGrounded ? cols[0].gameObject : null;
            if (slideBoostTimer > 0f)
                slideBoostTimer -= Time.fixedDeltaTime;
            else if (slideBoostTimer < 0f)
                slideBoostTimer = 0f;
            CurrentState.TickUpdate(this);

            if (!ODM.isReeling) { HandleRotation(); }
            if (SlideState != null)
            {
                PlayerMovementStateBase desiredState = (IsGrounded && ODM.isReeling) ? SlideState : CurrentState;
                CurrentState = desiredState;
            }
        }
        public Vector3 GetWishDir()
        {
            if (Camera == null) return Vector3.zero;
    
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
    
            Vector3 camForward = Camera.forward;
            Vector3 camRight = Camera.right;
    
            // Flatten to horizontal plane (ignore Y/pitch)
            camForward.y = 0f;
            camRight.y = 0f;
            camForward = camForward.normalized;
            camRight = camRight.normalized;
    
            // Optional: Project onto ground normal for slopes (more accurate on uneven terrain)
            Vector3 groundNormal = GetGroundNormal();
            if (groundNormal != Vector3.zero)
            {
                camForward = Vector3.ProjectOnPlane(camForward, groundNormal).normalized;
                camRight = Vector3.ProjectOnPlane(camRight, groundNormal).normalized;
            }
    
            return (h * camRight + v * camForward).normalized;
        }
        public Vector3 GetGroundNormal()
        {
            if (IsGrounded && Physics.Raycast(GroundPoint.position + Vector3.up, Vector3.down, out RaycastHit _hit, 2f, GroundLayers, QueryTriggerInteraction.Ignore))
                return _hit.normal;
            return Vector3.zero;
        }
        public bool GetWishJump() => Input.GetKeyDown(KeyCode.Space);
        public void PlayMotorSound(int index)
        {
            if (TryGetSound(index, out AudioSource au))
                au.Play();
        }
        public AudioSource GetSound(int index)
        {
            if (index >= Audio.Length || index < 0)
                return null;
            return Audio[index];
        }
        public bool TryGetSound(int index, out AudioSource au)
        {
            au = GetSound(index);
            return au != null;
        }
        public void PlayMotorParticle(int index)
        {
            if (TryGetParticle(index, out ParticleSystem particle))
                particle.Play();
        }
        public ParticleSystem GetParticle(int index)
        {
            if (index >= Particles.Length || index < 0)
                return null;
            return Particles[index];
        }
        public bool TryGetParticle(int index, out ParticleSystem au)
        {
            au = GetParticle(index);
            return au != null;
        }
        private void HandleRotation()
        {
            if (Camera == null) return;

            Vector3 wishDir = GetWishDir();
            if (wishDir.magnitude < 0.1f) return;  // No input, no rotate

            if (RotateOnlyWhenGrounded && !IsGrounded) return;  // Optional: Lock rotation mid-air

            // Project wishDir onto ground (handles slopes)
            //Vector3 groundNormal = GetGroundNormal();
            Vector3 moveDir;



            moveDir = new Vector3(wishDir.x, 0f, wishDir.z).normalized;


            if (moveDir.magnitude < 0.1f) return;

            // Target rotation (yaw only, up vector preserved)
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            if (!IsGrounded)
            {
                // Reduce rotation speed when airborne
                Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRot, RotationSpeed * RotationSpeedAirborneMultiplier * Time.fixedDeltaTime));
                return;
            }
            else
            {
                                Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRot, RotationSpeed * Time.fixedDeltaTime));
            }
           
        }

        private IEnumerator HandleAiming()
        {
            while (Input.GetMouseButton(1))
            {
                if (Camera == null || ODM.isReeling)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                // Face the camera's forward direction (flattened to horizontal plane)
                Vector3 camForward = Camera.forward;
                camForward.y = 0f;
                camForward.Normalize();

                if (camForward.magnitude < 0.01f)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                Quaternion targetRot = Quaternion.LookRotation(camForward, Vector3.up);

                // Slerp to target rotation (use a higher speed for quicker snapping during aiming)
                float aimingRotationSpeed = RotationSpeed * 2f; // Adjust multiplier as needed for responsiveness
                Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRot, aimingRotationSpeed * Time.fixedDeltaTime));

                // Movement remains relative to camera (as handled by GetWishDir())

                yield return new WaitForFixedUpdate(); // Sync with physics updates
            }
        }
    }
}