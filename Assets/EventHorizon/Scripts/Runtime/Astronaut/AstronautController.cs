using EventHorizon.Core;
using EventHorizon.Interaction;
using UnityEngine;

namespace EventHorizon.Astronaut
{
    /// <summary>
    /// First/third-person astronaut movement in Mars gravity (3.72 m/s^2, so a 2 m/s jump rises ~0.54 m).
    /// Input comes from <see cref="GameInput"/>, so rebinding and menus (which block gameplay input) just work.
    /// Animator states are played by name: Idle, Walk, Run, Jump, Interact. Missing states are skipped.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AstronautController : MonoBehaviour
    {
        public Camera viewCamera;
        public Animator animator;
        [Tooltip("Head bone. Hidden in first person so the camera doesn't see inside the helmet.")]
        public Transform headBone;

        [Header("Movement")]
        public float walkSpeed = 1.4f;
        public float runSpeed = 3.0f;
        public float crouchSpeed = 0.7f;
        public float jumpSpeed = 2.0f;
        public float turnSmoothing = 10f;

        [Header("Camera")]
        public bool firstPerson = true;
        public float eyeHeight = 1.65f;
        public float crouchEyeDrop = 0.5f;
        public Vector3 thirdPersonPivot = new Vector3(0.35f, 1.6f, 0f);
        public float thirdPersonDistance = 3.5f;
        public LayerMask cameraCollision = ~0;

        public float Yaw => _yaw;
        public bool Crouching { get; private set; }

        CharacterController _cc;
        Renderer[] _bodyRenderers;
        bool? _bodyHidden;
        float _yaw, _pitch, _verticalSpeed, _standHeight;
        string _state;
        Vector3 _headScale = Vector3.one;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _standHeight = _cc.height;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _yaw = transform.eulerAngles.y;
            firstPerson = SettingsService.Current.gameplay.firstPerson;
            var interactor = GetComponent<Interactor>();
            if (interactor != null) interactor.Interacted += _ => Play("Interact", force: true);
        }

        // In Start, not Awake: AstronautSuit finds the generic-rig head bone in its own Awake.
        void Start()
        {
            if (headBone == null && animator != null && animator.isHuman) headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
            {
                var suit = GetComponent<AstronautSuit>();
                if (suit != null) headBone = suit.head;
            }
            if (headBone != null) _headScale = headBone.localScale;
            _bodyRenderers = GetComponentsInChildren<Renderer>(true);
            GameInput.UpdateCursor();
        }

        /// <summary>Moves the player (save loading, respawn) without the CharacterController fighting it.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _cc.enabled = true;
            _yaw = yaw;
            _verticalSpeed = 0f;
        }

        /// <summary>Points the view (cutscenes, save loading, screenshot tours).</summary>
        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Update()
        {
            if (GameInput.ToggleView.WasPressedThisFrame()) firstPerson = !firstPerson;

            var controls = SettingsService.Current.controls;
            Vector2 look = GameInput.Look.ReadValue<Vector2>() * controls.mouseSensitivity;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch + (controls.invertY ? look.y : -look.y), -80f, 80f);

            Crouching = GameInput.Crouch.IsPressed();
            _cc.height = Crouching ? _standHeight * 0.65f : _standHeight;
            _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);

            Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
            bool running = GameInput.Run.IsPressed() && !Crouching;
            float speed = Crouching ? crouchSpeed : running ? runSpeed : walkSpeed;
            var yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 wish = yawRot * new Vector3(input.x, 0f, input.y) * speed;

            if (_cc.isGrounded)
            {
                _verticalSpeed = -0.5f; // keep contact on slopes
                if (GameInput.Jump.WasPressedThisFrame() && !Crouching) _verticalSpeed = jumpSpeed;
            }
            _verticalSpeed += Physics.gravity.y * Time.deltaTime;
            _cc.Move((wish + Vector3.up * _verticalSpeed) * Time.deltaTime);

            // First person: the body faces where you look. Third person: it turns toward the movement direction.
            if (firstPerson) transform.rotation = yawRot;
            else if (wish.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), turnSmoothing * Time.deltaTime);

            float horizontal = new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude;
            if (!_cc.isGrounded && _verticalSpeed > 0.1f) Play("Jump");
            else if (_state != "Interact" || IsStateFinished())
                Play(horizontal > (walkSpeed + runSpeed) * 0.5f ? "Run" : horizontal > 0.2f ? "Walk" : "Idle");
        }

        void LateUpdate()
        {
            // First person: the body renders only into shadows, so the camera never sees inside the suit
            // and the astronaut still casts a shadow on the ground.
            if (_bodyHidden != firstPerson && _bodyRenderers != null)
            {
                foreach (var r in _bodyRenderers)
                    if (r != null) r.shadowCastingMode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                _bodyHidden = firstPerson;
            }
            if (viewCamera == null) return;

            var lookRot = Quaternion.Euler(_pitch, _yaw, 0f);
            if (firstPerson)
            {
                Vector3 eye = headBone != null ? headBone.position : transform.position + Vector3.up * eyeHeight;
                if (Crouching && headBone == null) eye -= Vector3.up * crouchEyeDrop;
                viewCamera.transform.SetPositionAndRotation(eye + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * 0.12f, lookRot);
            }
            else
            {
                Vector3 pivot = transform.position + Quaternion.Euler(0f, _yaw, 0f) * thirdPersonPivot;
                if (Crouching) pivot -= Vector3.up * crouchEyeDrop;
                Vector3 back = lookRot * Vector3.back;
                float dist = thirdPersonDistance;
                if (Physics.SphereCast(pivot, 0.2f, back, out var hit, dist, cameraCollision, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(transform))
                    dist = hit.distance;
                viewCamera.transform.SetPositionAndRotation(pivot + back * dist, lookRot);
            }
        }

        void Play(string state, bool force = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!force && _state == state) return;
            if (!animator.HasState(0, Animator.StringToHash(state))) return;
            animator.CrossFadeInFixedTime(state, 0.15f);
            _state = state;
        }

        bool IsStateFinished()
        {
            if (animator == null) return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return !animator.IsInTransition(0) && info.normalizedTime >= 1f;
        }
    }
}
