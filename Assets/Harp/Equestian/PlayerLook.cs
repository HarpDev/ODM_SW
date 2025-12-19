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
        public Camera playerCamera;              // main camera used for world rendering
        public Transform cameraPivot;            // transform placed at shoulder/head height on the player (target to orbit around)

        [Header("Orbit Settings")]
        public float distance = 3.5f;
        public float height = 1.6f;
        public float minPitch = -30f;
        public float maxPitch = 60f;
        public float sensitivity = 1f;
        public float smoothTimePos = 0.05f;
        public float smoothTimeRot = 0.05f;

        [Header("Optional")]
        public bool lockCursor = true;

        float pitch = 10f;
        float yaw = 0f;
        float pitchVel;
        float yawVel;
        float currSens;

        // convenience accessor to be used by movement code
        public Camera playerCam => playerCamera;

        private void Awake()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main;

            currSens = Sensitivity;
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnEnable()
        {
            currSens = Sensitivity;
        }

        private Vector3 positionVel; // <-- add this line near the top of the class, after pitchVel/yawVel



        private void LateUpdate()//ISSUES Fixed Update has input issues but follows smoothly- Late and Update have jitter, but better input
        {
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
            if (cameraPivot != null && playerCamera != null)
            {

                Vector3 pivotPos = cameraPivot.position + Vector3.up * height;
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
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
