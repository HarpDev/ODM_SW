using SwiftKraft.Utils;
using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Player.Movement
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMotor : MonoBehaviour
    {
        #region Delegates and Events
        public delegate void OnPlayerGroundedChanged(bool curr, bool prev);
        public OnPlayerGroundedChanged OnGroundedChanged;
        #endregion

        #region Serialized Fields
        [Header("Third Person Orientation")]
        public Transform Camera;
        public float GroundRotationSpeed = 12f;
        public float AirborneRotationSpeed = 6f;

        [SerializeField] private PL_ODM ODM;
        #endregion

        #region Public Fields
        public PlayerMovementStateBase DefaultState;
        public PlayerMovementStateBase SlideState;
        public Timer RecentJumpTimer;
        public float OriginalCameraHeight = 1.7f;
        public float CameraSmoothTime = 0.1f;
        public Transform CameraRoot;
        public Transform GroundPoint;
        public float GroundRadius;
        public LayerMask GroundLayers;
        public AudioSource[] Audio;
        public ParticleSystem[] Particles;
        public float currentSpeed;
        #endregion

        #region Hidden Fields
        [HideInInspector] public float TargetCameraHeight;
        [HideInInspector] public float slideBoostTimer;
        #endregion

        #region Private Fields
        private PlayerMovementStateBase state;
        private bool isGrounded;
        private float cameraVel;
        #endregion

        #region Properties
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

        public Rigidbody Rigidbody { get; private set; }
        public CapsuleCollider Collider { get; private set; }

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
            protected set => CameraRoot.localPosition = new(0f, value, 0f);
        }

        public float ViableHeight
        {
            get
            {
                if (Physics.Raycast(GroundPoint.position + (Vector3.up * 0.01f), Vector3.up, out RaycastHit hit, Mathf.Infinity, GroundLayers, QueryTriggerInteraction.Ignore))
                    return hit.distance;
                return Mathf.Infinity;
            }
        }

        public GameObject GroundObject { get; private set; }
        #endregion

        #region Unity Methods
        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Collider = GetComponent<CapsuleCollider>();
            TargetCameraHeight = OriginalCameraHeight;
            if (DefaultState == null)
                enabled = false;
            if (Camera == null)
            {
                Camera = CameraRoot.GetComponentInChildren<Camera>()?.transform;
                if (Camera == null)
                    Debug.LogWarning("PlayerMotor: No Camera assigned or found! Movement will be broken.");
            }
        }

        private void Start()
        {
            StartCoroutine(UpdateSpeed());
        }

        private void Update()
        {
            CameraHeight = Mathf.SmoothDamp(CameraHeight, TargetCameraHeight, ref cameraVel, CameraSmoothTime);
            CurrentState.InputUpdate(this);
        }

        private void FixedUpdate()
        {
            RecentJumpTimer.Tick(Time.fixedDeltaTime);
            Collider[] cols = Physics.OverlapSphere(GroundPoint.position, GroundRadius, GroundLayers)
                .OrderBy(c => (c.transform.position - transform.position).sqrMagnitude).ToArray();
            IsGrounded = cols.Length > 0;
            GroundObject = IsGrounded ? cols[0].gameObject : null;
            if (slideBoostTimer > 0f)
                slideBoostTimer -= Time.fixedDeltaTime;
            else if (slideBoostTimer < 0f)
                slideBoostTimer = 0f;
            CurrentState.TickUpdate(this);
            if (!ODM.isReeling)
                HandleCameraFacingRotation();
            if (SlideState != null)
            {
                PlayerMovementStateBase desiredState = (IsGrounded && ODM.isReeling) ? SlideState : CurrentState;
                CurrentState = desiredState;
            }
        }
        #endregion

        #region Coroutines
        private IEnumerator UpdateSpeed()
        {
            while (true)
            {
                currentSpeed = Mathf.Ceil(Rigidbody.velocity.magnitude);
                yield return new WaitForSeconds(1f);
            }
        }
        #endregion

        #region Public Methods
        public Vector3 GetWishDir()
        {
            if (Camera == null) return Vector3.zero;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 camForward = Camera.forward;
            Vector3 camRight = Camera.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward = camForward.normalized;
            camRight = camRight.normalized;
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
            if (IsGrounded && Physics.Raycast(GroundPoint.position + Vector3.up, Vector3.down, out RaycastHit hit, 2f, GroundLayers, QueryTriggerInteraction.Ignore))
                return hit.normal;
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
        #endregion

        #region Private Methods
        private void HandleCameraFacingRotation()
        {
            if (Camera == null || ODM.isReeling) return;
            Vector3 wishDir = GetWishDir();
            bool hasInput = wishDir.magnitude >= 0.1f;
            // For grounded, skip if no input; for air, always rotate
            if (IsGrounded && !hasInput) return;
            Vector3 camForward = Camera.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.0001f) return;
            camForward.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(camForward, Vector3.up);
            float rotationSpeed = IsGrounded ? GroundRotationSpeed : AirborneRotationSpeed;
            float rotationStep = rotationSpeed * Time.fixedDeltaTime;
            Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRotation, rotationStep));
        }
        #endregion
    }
}