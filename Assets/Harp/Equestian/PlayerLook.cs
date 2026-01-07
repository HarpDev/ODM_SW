using SwiftKraft.Saving;
using SwiftKraft.Saving.Settings;
using System.Collections.Generic;
using UnityEngine;

namespace Player.Movement
{
    [DisallowMultipleComponent]
    public class PlayerLook : MonoBehaviour
    {
        public const string SensKey = "c_sensitivity";
        public static readonly List<OverrideLayer> Overrides = new();
        [Header("References")]
        public Transform playerCamera; // switched to referencing transform instead of player camera 1/6/26
        public Transform cameraPivot;
        public PL_ODM ODM; // this is needed as most camera tilt effects are based on ODM traversal
        public PlayerMotor player;
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
        [Header("Optional")]
        public bool lockCursor = true;
        float pitch = 10f;
        float yaw = 0f;
        float currSens;

        private void Awake()
        {
            currSens = Sensitivity;
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
        }

        private void OnEnable()
        {
            currSens = Sensitivity;
        }

        private Vector3 positionVel;

        private void LateUpdate()
        {
            PerformCameraTiltFunctionality();

            currSens = Sensitivity;
            foreach (OverrideLayer layer in Overrides)
            {
                if (layer.Sensitivity > 0)
                {
                    currSens = layer.Sensitivity;
                    break;
                }
            }
            Vector2 mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            // apply sensitivity
            float mx = mouseInput.x * currSens;
            float my = mouseInput.y * currSens;
            yaw += mx;
            pitch -= my;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            // compute target position/rotation for camera
            if (cameraPivot != null && playerCamera != null && tiltTarget != null)
            {
                Vector3 pivotPos = cameraPivot.position + Vector3.up * height;
                Quaternion baseRot = Quaternion.Euler(pitch, yaw, 0f);
                Quaternion rot = baseRot * tiltTarget.localRotation;
                Vector3 desiredCamPos = pivotPos + rot * (Vector3.back * distance);
                // optional collision check
                if (Physics.Linecast(pivotPos, desiredCamPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    float adjustedDistance = Mathf.Max(0.5f, (hit.point - pivotPos).magnitude - 0.1f);
                    desiredCamPos = pivotPos + rot * (Vector3.back * adjustedDistance);
                }
                // ? FIX: use Vector3 velocity, not float
                playerCamera.transform.position = Vector3.SmoothDamp(
                    playerCamera.transform.position,
                    desiredCamPos,
                    ref positionVel,
                    smoothTimePos
                );
                playerCamera.transform.rotation = Quaternion.Slerp(
                    playerCamera.transform.rotation,
                    rot,
                    Time.deltaTime * (1f / Mathf.Max(0.001f, smoothTimeRot))
                );
            }
        }

        private void PerformCameraTiltFunctionality()
        {
            if (tiltTarget == null) return;

            float targetPitchOffset = 0f;
            float targetRoll = 0f;

            if (ODM.isReeling && !player.IsGrounded)
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