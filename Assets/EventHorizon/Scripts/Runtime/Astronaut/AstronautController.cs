using EventHorizon.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EventHorizon.Astronaut
{
    /// <summary>
    /// First/third-person astronaut movement in Mars gravity (3.72 m/s^2, so a 2 m/s jump rises ~0.54 m).
    /// Works with an animated rig (Quaternius astronaut) or a static model (Kenney astronaut).
    /// Animator states are played by name: Idle, Walk, Run, Jump, Interact. Missing states are skipped.
    /// Controls: WASD / left stick, mouse / right stick, Shift run, Space jump, V switch view, Esc free cursor.
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
        public float jumpSpeed = 2.0f;
        public float turnSmoothing = 10f;
        public float lookSensitivity = 0.12f;

        [Header("Camera")]
        public bool firstPerson = true;
        public float eyeHeight = 1.65f;
        public Vector3 thirdPersonPivot = new Vector3(0.35f, 1.6f, 0f);
        public float thirdPersonDistance = 3.5f;
        public LayerMask cameraCollision = ~0;

        CharacterController _cc;
        InputAction _move, _look, _run, _jump, _toggleView;
        float _yaw, _pitch, _verticalSpeed;
        string _state;
        Vector3 _headScale = Vector3.one;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _yaw = transform.eulerAngles.y;

            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
            _run = new InputAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            _run.AddBinding("<Gamepad>/leftStickPress");
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");
            _toggleView = new InputAction("ToggleView", InputActionType.Button, "<Keyboard>/v");
            _toggleView.AddBinding("<Gamepad>/select");

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
        }

        void OnEnable()
        {
            foreach (var a in new[] { _move, _look, _run, _jump, _toggleView }) a.Enable();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void OnDisable()
        {
            foreach (var a in new[] { _move, _look, _run, _jump, _toggleView }) a.Disable();
            Cursor.lockState = CursorLockMode.None;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            if (_toggleView.WasPressedThisFrame()) firstPerson = !firstPerson;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, -80f, 80f);
            }

            Vector2 input = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            bool running = _run.IsPressed();
            var yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 wish = yawRot * new Vector3(input.x, 0f, input.y) * (running ? runSpeed : walkSpeed);

            if (_cc.isGrounded)
            {
                _verticalSpeed = -0.5f; // keep contact on slopes
                if (_jump.WasPressedThisFrame()) _verticalSpeed = jumpSpeed;
            }
            _verticalSpeed += Physics.gravity.y * Time.deltaTime;
            _cc.Move((wish + Vector3.up * _verticalSpeed) * Time.deltaTime);

            // First person: the body faces where you look. Third person: it turns toward the movement direction.
            if (firstPerson) transform.rotation = yawRot;
            else if (wish.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), turnSmoothing * Time.deltaTime);

            float speed = new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude;
            if (!_cc.isGrounded && _verticalSpeed > 0.1f) Play("Jump");
            else if (_state != "Interact" || IsStateFinished())
                Play(speed > (walkSpeed + runSpeed) * 0.5f ? "Run" : speed > 0.2f ? "Walk" : "Idle");
        }

        void LateUpdate()
        {
            if (headBone != null) headBone.localScale = firstPerson ? _headScale * 0.001f : _headScale;
            if (viewCamera == null) return;

            var lookRot = Quaternion.Euler(_pitch, _yaw, 0f);
            if (firstPerson)
            {
                Vector3 eye = headBone != null ? headBone.position : transform.position + Vector3.up * eyeHeight;
                viewCamera.transform.SetPositionAndRotation(eye + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * 0.12f, lookRot);
            }
            else
            {
                Vector3 pivot = transform.position + Quaternion.Euler(0f, _yaw, 0f) * thirdPersonPivot;
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
