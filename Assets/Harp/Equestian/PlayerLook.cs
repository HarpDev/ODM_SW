using System.Collections.Generic;
using Harp.ODMLogic;
using Player.Movement;
using SwiftKraft.Saving.Settings;
using UnityEngine;
using UnityEngine.Serialization;

namespace Harp.Equestian
{
    [DisallowMultipleComponent]
    public class PlayerLook : MonoBehaviour
    {
        public const string SensKey = "c_sensitivity";
        public static readonly List<OverrideLayer> Overrides = new();
        [Header("References")]
        public Transform playerCamera; // switched to referencing transform instead of player camera 1/6/26
        public Transform cameraPivot;
        [FormerlySerializedAs("ODM")] public PL_ODM odm; // this is needed as most camera tilt effects are based on ODM traversal
        public PlayerMotor player;
        [FormerlySerializedAs("Cam")] public Camera cam;
        [Header("Orbit Settings")]
        public float distance = 3.5f;
        public float height = 1.6f;
        public float minPitch = -30f;
        public float maxPitch = 60f;
        public float sensitivity = 1f;
        public float smoothTimePos = 0.05f;
        public float smoothTimeRot = 0.05f;
        [Header("Tilt Settings")]
        public float maxPitchTilt = 5f;
        public float maxRollTilt = 5f;
        public float tiltSmoothTime = 0.2f;
        [Header("Tilt Target")]
        public Transform tiltTarget;
        [Header("Shoulder Settings")]
        public float shoulderOffsetAmount = 1f;
        public float shoulderSmoothTime = 0.1f;
        [Header("Zoom Settings")]
        public float normalFOV = 60f;
        public float zoomFOV = 40f;
        public float zoomSmoothTime = 0.2f;
        [Header("Dynamic FOV Settings")]
        public float dynamicSmoothTime = 0.5f;
        public float dynamicDecayRate = 5f;
        [Header("Optional")]
        public bool lockCursor = true;

        float _pitch = 10f;
        float _yaw;
        float _currSens;
        float _currentShoulderOffset;
        float _shoulderVel;
        float _currentFOV;
        float _fovVel;
        float _dynamicFOVTarget;
        float _currentDynamicFOV;
        float _dynamicVel;

        private void Awake()
        {
            _currSens = Sensitivity;
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (tiltTarget == null && cameraPivot != null)
            {
                GameObject tiltObj = new GameObject("TiltTarget");
                tiltTarget = tiltObj.transform;
                tiltTarget.SetParent(cameraPivot, worldPositionStays: false);
            }
            if (cam == null && playerCamera != null)
            {
                cam = playerCamera.GetComponent<Camera>();
            }
            _currentFOV = normalFOV;
            if (cam != null) cam.fieldOfView = normalFOV;
        }
        private void OnEnable()
        {
            _currSens = Sensitivity;
        }
        private Vector3 _positionVel;
        private void LateUpdate()
        {
            PerformCameraTiltFunctionality();
            PerformShoulderSwapFunctionality();
            PerformFOVAdjustments();

            _currSens = Sensitivity;
            foreach (OverrideLayer layer in Overrides)
            {
                if (layer.Sensitivity > 0)
                {
                    _currSens = layer.Sensitivity;
                    break;
                }
            }
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            // apply sensitivity
            float mx = mouseInput.x * _currSens;
            float my = mouseInput.y * _currSens;
            _yaw += mx;
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            // compute target position/rotation for camera
            if (cameraPivot != null && playerCamera != null && tiltTarget != null)
            {
                Vector3 pivotPos = cameraPivot.position + Vector3.up * height;
                Quaternion baseRot = Quaternion.Euler(_pitch, _yaw, 0f);
                Quaternion rot = baseRot * tiltTarget.localRotation;
                Vector3 offset = Vector3.back * distance + Vector3.right * _currentShoulderOffset;
                Vector3 desiredCamPos = pivotPos + rot * offset;
                // collision check to prevent clipping
                if (Physics.Linecast(pivotPos, desiredCamPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    float adjustedDistance = Mathf.Max(0.5f, (hit.point - pivotPos).magnitude - 0.1f);
                    desiredCamPos = pivotPos + rot * (Vector3.back * adjustedDistance + Vector3.right * _currentShoulderOffset);
                }
                playerCamera.transform.position = Vector3.SmoothDamp(
                    playerCamera.transform.position,
                    desiredCamPos,
                    ref _positionVel,
                    smoothTimePos
                );
                playerCamera.transform.rotation = Quaternion.Slerp(
                    playerCamera.transform.rotation,
                    rot,
                    Time.deltaTime * (1f / Mathf.Max(0.001f, smoothTimeRot))
                );
            }
            if (cam != null)
            {
                cam.fieldOfView = _currentFOV + _currentDynamicFOV;
            }
        }
        private void PerformCameraTiltFunctionality()
        {
            if (tiltTarget == null) return;
            float targetPitchOffset = 0f;
            float targetRoll = 0f;
            if (odm.isReeling && !player.IsGrounded)
            {
                Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (moveInput != Vector2.zero)
                {
                    moveInput.Normalize();
                    float angle = Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg;
                    float snappedAngle = Mathf.Round(angle / 45f) * 45f;
                    moveInput.x = Mathf.Sin(snappedAngle * Mathf.Deg2Rad);
                    moveInput.y = Mathf.Cos(snappedAngle * Mathf.Deg2Rad);
                }
                targetPitchOffset = moveInput.y * maxPitchTilt;
                targetRoll = -moveInput.x * maxRollTilt;
            }
            Quaternion targetTiltRot = Quaternion.Euler(targetPitchOffset, 0f, targetRoll);
            float slerpT = 1f - Mathf.Exp(-Time.deltaTime / tiltSmoothTime);
            tiltTarget.localRotation = Quaternion.Slerp(tiltTarget.localRotation, targetTiltRot, slerpT);
        }
        private void PerformShoulderSwapFunctionality()
        {
            float targetShoulder = 0f;
            
            bool qPressed = Input.GetKey(KeyCode.Mouse0);
            bool ePressed = Input.GetKey(KeyCode.Mouse1);

            if (odm.isReeling)
            {
                if (qPressed && !ePressed && !player.IsGrounded && odm.currentGasAmount > 0)
                {
                    targetShoulder = -shoulderOffsetAmount;
                }
                else if (ePressed && !qPressed && !player.IsGrounded && odm.currentGasAmount > 0)
                {
                    targetShoulder = shoulderOffsetAmount;
                }

                if (!qPressed && !ePressed)
                {
                    targetShoulder = 0f;
                }
            }

            _currentShoulderOffset = Mathf.SmoothDamp(_currentShoulderOffset, targetShoulder, ref _shoulderVel, shoulderSmoothTime);
        }
        private void PerformFOVAdjustments()
        {
            float targetFOV = normalFOV;
            _currentFOV = Mathf.SmoothDamp(_currentFOV, targetFOV, ref _fovVel, zoomSmoothTime);

            _currentDynamicFOV = Mathf.SmoothDamp(_currentDynamicFOV, _dynamicFOVTarget, ref _dynamicVel, dynamicSmoothTime);
            _dynamicFOVTarget = Mathf.Max(0f, _dynamicFOVTarget - dynamicDecayRate * Time.deltaTime);
        }
        private void FixedUpdate()
        {
        }
        public static void RemoveOverride(OverrideLayer layer) => Overrides.Remove(layer);
        public static OverrideLayer SetOverride(float value, int weight)
        {
            OverrideLayer layer = new(value, weight);
            Overrides.Add(layer);
            Overrides.Sort((a, b) => a.Weight.CompareTo(b.Weight));
            return layer;
        }
        public float Sensitivity => SettingsManager.Current.TrySetting("Sensitivity", out SingleSetting<float> sens) ? sens.Value : sensitivity;
        public void FovBurst(float amount)
        {
            if (cam.fieldOfView <= 110)
            _dynamicFOVTarget += amount;
        }
        public class OverrideLayer
        {
            public float Sensitivity;
            public int Weight;
            public OverrideLayer(float sens, int weight)
            {
                Sensitivity = sens;
                Weight = weight;
            }
        }
    }
}