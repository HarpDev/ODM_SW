using SwiftKraft.Utils;
using System.Linq;
using UnityEngine;
using System.Collections;
using Harp.ODMLogic;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("Camera")] [Header("Third Person Orientation")]
        public new Transform camera;
        [FormerlySerializedAs("GroundRotationSpeed")] public float groundRotationSpeed = 12f;
        [FormerlySerializedAs("AirborneRotationSpeed")] public float airborneRotationSpeed = 6f;

        [FormerlySerializedAs("ODM")] [SerializeField] private PL_ODM odm;
        #endregion

        #region Public Fields
        [FormerlySerializedAs("DefaultState")] public PlayerMovementStateBase defaultState;
        [FormerlySerializedAs("SlideState")] public PlayerMovementStateBase slideState;
        [FormerlySerializedAs("RecentJumpTimer")] public Timer recentJumpTimer;
        [FormerlySerializedAs("OriginalCameraHeight")] public float originalCameraHeight = 1.7f;
        [FormerlySerializedAs("CameraSmoothTime")] public float cameraSmoothTime = 0.1f;
        [FormerlySerializedAs("CameraRoot")] public Transform cameraRoot;
        [FormerlySerializedAs("GroundPoint")] public Transform groundPoint;
        [FormerlySerializedAs("GroundRadius")] public float groundRadius;
        [FormerlySerializedAs("GroundLayers")] public LayerMask groundLayers;
        [FormerlySerializedAs("Audio")] public AudioSource[] audioSources;
        [FormerlySerializedAs("Particles")] public ParticleSystem[] particles;
        public float currentSpeed;
        
        [Header("Animation")]
         public Animator animator;
         public static readonly int AnimJump = Animator.StringToHash("Jump");
        #endregion

        #region Hidden Fields
        [FormerlySerializedAs("TargetCameraHeight")] [HideInInspector] public float targetCameraHeight;
        [HideInInspector] public float slideBoostTimer;
        #endregion

        #region Private Fields
        private PlayerMovementStateBase _state;
        private bool _isGrounded;
        private float _cameraVel;

        public PlayerMotor(float targetCameraHeight)
        {
            this.targetCameraHeight = targetCameraHeight;
        }

        #endregion

        #region Properties
        public PlayerMovementStateBase CurrentState
        {
            get
            {
                if (_state == null)
                {
                    _state = defaultState;
                    _state.StateStarted(this);
                }
                return _state;
            }
            set
            {
                if (_state == value || (value == null && _state == defaultState))
                    return;
                if (_state != null)
                    _state.StateEnded(this);
                _state = value != null ? value : defaultState;
                _state.StateStarted(this);
            }
        }

        public Rigidbody Rigidbody { get; private set; }
        public CapsuleCollider Collider { get; private set; }

        public bool IsGrounded
        {
            get => _isGrounded;
            protected set
            {
                if (_isGrounded == value)
                    return;
                bool prev = _isGrounded;
                _isGrounded = value;
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
            get => cameraRoot.localPosition.y;
            protected set => cameraRoot.localPosition = new(0f, value, 0f);
        }

        public float ViableHeight
        {
            get
            {
                if (Physics.Raycast(groundPoint.position + (Vector3.up * 0.01f), Vector3.up, out RaycastHit hit, Mathf.Infinity, groundLayers, QueryTriggerInteraction.Ignore))
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
            targetCameraHeight = originalCameraHeight;
            if (defaultState == null)
                enabled = false;
            if (camera == null)
            {
                camera = cameraRoot.GetComponentInChildren<Camera>()?.transform;
                if (camera == null)
                    Debug.LogWarning("PlayerMotor: No Camera assigned or found! Movement will be broken.");
            }
        }

        private void Start()
        {
            StartCoroutine(UpdateSpeed());
        }

        private void Update()
        {
            CameraHeight = Mathf.SmoothDamp(CameraHeight, targetCameraHeight, ref _cameraVel, cameraSmoothTime);
            CurrentState.InputUpdate(this);
        }

        private void FixedUpdate()
        {
            recentJumpTimer.Tick(Time.fixedDeltaTime);
            Collider[] cols = Physics.OverlapSphere(groundPoint.position, groundRadius, groundLayers)
                .OrderBy(c => (c.transform.position - transform.position).sqrMagnitude).ToArray();
            IsGrounded = cols.Length > 0;
            GroundObject = IsGrounded ? cols[0].gameObject : null;
            if (slideBoostTimer > 0f)
                slideBoostTimer -= Time.fixedDeltaTime;
            else if (slideBoostTimer < 0f)
                slideBoostTimer = 0f;
            CurrentState.TickUpdate(this);
            if (!odm.isReeling)
                HandleCameraFacingRotation();
            if (slideState != null)
            {
                PlayerMovementStateBase desiredState = (IsGrounded && odm.isReeling) ? slideState : CurrentState;
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
                yield return new WaitForSeconds(0.1f);
            }

            
        }
        #endregion

        #region Public Methods
        public Vector3 GetWishDir()
        {
            if (camera == null) return Vector3.zero;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 camForward = camera.forward;
            Vector3 camRight = camera.right;
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
            if (IsGrounded && Physics.Raycast(groundPoint.position + Vector3.up, Vector3.down, out RaycastHit hit, 2f, groundLayers, QueryTriggerInteraction.Ignore))
                return hit.normal;
            return Vector3.zero;
           
        }

        public bool GetWishJump() => Input.GetKeyDown(KeyCode.Space);

        public void CallJumpAnimation()
        {
            animator.SetTrigger(AnimJump);
        }

        public void PlayMotorSound(int index)
        {
            if (TryGetSound(index, out AudioSource au))
                au.Play();
        }

        public AudioSource GetSound(int index)
        {
            if (index >= audioSources.Length || index < 0)
                return null;
            return audioSources[index];
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
            if (index >= particles.Length || index < 0)
                return null;
            return particles[index];
        }

        public bool TryGetParticle(int index, out ParticleSystem au)
        {
            au = GetParticle(index);
            return au != null;
        }

        public void TriggerJumpAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(AnimJump);
                animator.SetTrigger(AnimJump);
            }
        }
        #endregion

        #region Private Methods
        private void HandleCameraFacingRotation()
        {
            if (camera == null || odm.isReeling) return;
            Vector3 wishDir = GetWishDir();
            bool hasInput = wishDir.magnitude >= 0.1f;
            // For grounded, skip if no input; for air, always rotate
            if (IsGrounded && !hasInput) return;
            Vector3 camForward = camera.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.0001f) return;
            camForward.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(camForward, Vector3.up);
            float rotationSpeed = IsGrounded ? groundRotationSpeed : airborneRotationSpeed;
            float rotationStep = rotationSpeed * Time.fixedDeltaTime;
            Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRotation, rotationStep));
        }
        #endregion
    }
}