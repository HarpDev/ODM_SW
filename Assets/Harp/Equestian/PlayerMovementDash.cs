//using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;

namespace Player.Movement
{
    [CreateAssetMenu(menuName = "Movement State/Dash")]
    public class PlayerMovementDash : PlayerMovementBasic
    {
        private static float dashSpeed;
        public const int DashLayer = 21;
        public const int PlayerLayer = 3;
        private PlayerMotor motor;
        public PlayerMovementStateBase ExitState;
        public float Duration = 2f;
        public float DashEndMultiplier = 0.3f;
        float timer;
        bool dashJumped;
        bool canDashJump;
        Vector3 direction;
        public override void StateStarted(PlayerMotor parent)
        {
            dashSpeed =  Speed + parent.currentSpeed;
            motor = parent.GetComponent<PlayerMotor>();
            timer = Duration;
            parent.Collider.gameObject.layer = DashLayer;
            parent.Rigidbody.useGravity = false;
        }
        public override void StateEnded(PlayerMotor parent)
        {
            base.StateEnded(parent);
            parent.Collider.gameObject.layer = PlayerLayer;
            parent.Rigidbody.useGravity = true;
            if (dashJumped)
                dashJumped = false;
            else
                parent.Rigidbody.velocity *= DashEndMultiplier;
        }
        public override void TickUpdate(PlayerMotor parent)
        {
            base.TickUpdate(parent);
            if (motor.camera == null)
                direction = parent.transform.forward;
            else
            {
                direction = motor.camera.forward.normalized;
            }
            canDashJump = timer > 0f;
            if (timer > 0f)
                timer -= Time.fixedDeltaTime;
            else
            {
                timer = 0f;
                parent.CurrentState = ExitState;
                return;
            }
            
            parent.Rigidbody.velocity = direction * dashSpeed;
            if (CurrentJumpBuffer > 0f)
            {
                TryJump(parent);
                CurrentJumpBuffer -= Time.fixedDeltaTime;
            }
            else if (CurrentJumpBuffer < 0f)
                CurrentJumpBuffer = 0f;
        }
        public override void TryJump(PlayerMotor parent, float speed = -1)
        {
            if (!canDashJump)
                return;
            base.TryJump(parent, speed);
            dashJumped = true;
            parent.CurrentState = ExitState;
        }
    }
}