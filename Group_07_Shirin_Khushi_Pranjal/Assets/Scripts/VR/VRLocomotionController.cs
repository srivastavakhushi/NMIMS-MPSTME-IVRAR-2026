using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CrimeSceneVR.Core;

namespace CrimeSceneVR.VR
{
    /// <summary>
    /// Person 1 VR Movement & Teleportation Controller for Meta Quest / OpenXR.
    /// Provides:
    /// - Left Thumbstick smooth locomotion (head-relative horizontal movement with collision & gravity)
    /// - Right Thumbstick comfortable continuous smooth turning around player's head (default, 45-60 deg/s)
    /// - Optional comfort snap turning toggle (Right Thumbstick Click or T key)
    /// - Parabolic arc teleportation with visual line indicator, landing clearance validation, and floor normal checks
    /// - Automatic CharacterController capsule alignment with headset local XZ offset
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Crime Scene VR/VR/VR Locomotion Controller")]
    public class VRLocomotionController : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("Reference to VRRigReferences on the OVRCameraRig. Auto-resolved if null.")]
        [SerializeField] private VRRigReferences _rigReferences;

        [Tooltip("Optional reference to CrimeSceneEvidenceManager for floor bounds validation.")]
        [SerializeField] private CrimeSceneEvidenceManager _evidenceManager;

        [Header("Smooth Locomotion (Left Thumbstick)")]
        [SerializeField] private bool _enableSmoothMove = true;
        [SerializeField] private float _moveSpeed = 2.2f;
        [SerializeField] private float _deadzone = 0.18f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Turning (Right Thumbstick)")]
        [Tooltip("When true, snap turns by _snapTurnAngle. When false, continuous smooth turning at _smoothTurnSpeed.")]
        [SerializeField] private bool _useSnapTurn = false;
        [SerializeField] private float _smoothTurnSpeed = 50f;
        [SerializeField] private float _snapTurnAngle = 45f;
        [SerializeField] private float _snapTurnThreshold = 0.65f;
        [SerializeField] private float _snapTurnResetThreshold = 0.30f;
        [SerializeField] private float _turnDeadzone = 0.15f;

        [Header("Teleportation (Parabolic Arc & Landing Clearance)")]
        [SerializeField] private bool _enableTeleport = true;
        [SerializeField] private float _maxTeleportDistance = 12f;
        [SerializeField] private float _minFloorNormalY = 0.70f;
        [SerializeField] private float _teleportStickThreshold = 0.75f;
        [SerializeField] private float _arcInitialSpeed = 9.0f;
        [SerializeField] private int _arcStepCount = 30;
        [SerializeField] private float _arcStepTime = 0.05f;
        [SerializeField] private float _capsuleRadius = 0.25f;
        [SerializeField] private float _capsuleHeight = 1.65f;
        [SerializeField] private LayerMask _teleportRaycastMask = ~0;

        private bool _snapTurnReady = true;
        private bool _isAimingTeleport;
        private bool _hasValidTeleportTarget;
        private Vector3 _pendingTeleportPoint;
        private float _verticalVelocity;

        private GameObject _teleportReticle;
        private Material _reticleMaterial;
        private LineRenderer _arcRenderer;
        private Material _arcMaterial;

        private static readonly Color ValidArcColor = new Color(0.0f, 1.0f, 0.7f, 0.90f);
        private static readonly Color InvalidArcColor = new Color(1.0f, 0.25f, 0.25f, 0.90f);

        public VRRigReferences RigReferences => _rigReferences;
        public bool EnableSmoothMove { get => _enableSmoothMove; set => _enableSmoothMove = value; }
        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = Mathf.Max(0.1f, value); }
        public bool UseSnapTurn { get => _useSnapTurn; set => _useSnapTurn = value; }
        public float SmoothTurnSpeed { get => _smoothTurnSpeed; set => _smoothTurnSpeed = Mathf.Max(1f, value); }
        public float SnapTurnAngle { get => _snapTurnAngle; set => _snapTurnAngle = value; }
        public bool EnableTeleport { get => _enableTeleport; set => _enableTeleport = value; }
        public float MinFloorNormalY => _minFloorNormalY;
        public float ArcInitialSpeed { get => _arcInitialSpeed; set => _arcInitialSpeed = Mathf.Max(1f, value); }
        public int ArcStepCount { get => _arcStepCount; set => _arcStepCount = Mathf.Clamp(value, 5, 100); }
        public float CapsuleRadius { get => _capsuleRadius; set => _capsuleRadius = Mathf.Max(0.05f, value); }
        public float CapsuleHeight { get => _capsuleHeight; set => _capsuleHeight = Mathf.Max(0.2f, value); }

        public bool IsAimingTeleport => _isAimingTeleport;
        public bool HasValidTeleportTarget => _hasValidTeleportTarget;
        public Vector3 PendingTeleportPoint => _pendingTeleportPoint;
        public LineRenderer ArcRenderer => _arcRenderer;

        public event Action<Vector3> OnTeleported;
        public event Action<float> OnSnapTurned;
        public event Action<bool> OnTurnModeChanged;

        public void Configure(VRRigReferences rigReferences, CrimeSceneEvidenceManager evidenceManager = null)
        {
            _rigReferences = rigReferences;
            if (evidenceManager != null)
                _evidenceManager = evidenceManager;
        }

        public void ToggleTurnMode()
        {
            _useSnapTurn = !_useSnapTurn;
            OnTurnModeChanged?.Invoke(_useSnapTurn);
            Debug.Log($"[VRLocomotionController] Turn mode switched to: {(_useSnapTurn ? "Comfort Snap Turn (45°)" : $"Smooth Continuous Turn ({_smoothTurnSpeed:F0}°/s)")}");
        }

        private void Awake()
        {
            ResolveReferences();
            SetupTeleportReticle();
            SetupTeleportArcRenderer();
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            SyncCharacterControllerToHead();

            Vector2 leftStick = ReadLeftThumbstick();
            Vector2 rightStick = ReadRightThumbstick();
            bool teleportButtonHeld = ReadTeleportButtonHeld();
            bool teleportButtonReleased = ReadTeleportButtonReleased();
            bool rightStickClick = ReadRightStickClick();

            if (rightStickClick)
            {
                ToggleTurnMode();
            }

            if (_enableSmoothMove)
            {
                SimulateMove(leftStick, Time.unscaledDeltaTime);
            }

            ProcessTurning(rightStick, Time.unscaledDeltaTime);

            if (_enableTeleport)
            {
                ProcessTeleportInput(rightStick, teleportButtonHeld, teleportButtonReleased);
            }
        }

        private void OnDisable()
        {
            if (_teleportReticle != null)
                _teleportReticle.SetActive(false);
            if (_arcRenderer != null)
                _arcRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_reticleMaterial != null)
            {
                DestroyImmediate(_reticleMaterial);
                _reticleMaterial = null;
            }
            if (_arcMaterial != null)
            {
                DestroyImmediate(_arcMaterial);
                _arcMaterial = null;
            }
            if (_teleportReticle != null)
            {
                DestroyImmediate(_teleportReticle);
                _teleportReticle = null;
            }
        }

        /// <summary>
        /// Moves the VR rig horizontally relative to the head's facing direction.
        /// Public for deterministic unit testing and external invocation.
        /// </summary>
        public void SimulateMove(Vector2 stickInput, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Transform rigRoot = _rigReferences != null ? _rigReferences.Root : transform;
            Transform head = (_rigReferences != null && _rigReferences.CenterEyeAnchor != null)
                ? _rigReferences.CenterEyeAnchor
                : rigRoot;

            Vector3 horizontalVelocity = Vector3.zero;
            if (stickInput.magnitude >= _deadzone)
            {
                Vector3 forward = head.forward;
                Vector3 right = head.right;
                forward.y = 0f;
                right.y = 0f;

                if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
                else forward = Vector3.forward;

                if (right.sqrMagnitude > 0.0001f) right.Normalize();
                else right = Vector3.right;

                Vector3 moveDir = forward * stickInput.y + right * stickInput.x;
                if (moveDir.sqrMagnitude > 1f)
                    moveDir.Normalize();

                float currentSpeed = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                    ? _moveSpeed * 2.0f
                    : _moveSpeed;

                horizontalVelocity = moveDir * currentSpeed;
            }

            CharacterController cc = _rigReferences != null ? _rigReferences.CharacterController : GetComponent<CharacterController>();
            if (cc != null && cc.enabled && cc.gameObject.activeInHierarchy)
            {
                if (cc.isGrounded && _verticalVelocity < 0f)
                    _verticalVelocity = -0.5f;
                else
                    _verticalVelocity += _gravity * deltaTime;

                Vector3 displacement = (horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime;
                if (horizontalVelocity.sqrMagnitude > 0.0001f || !cc.isGrounded)
                {
                    cc.Move(displacement);
                }
            }
            else if (horizontalVelocity.sqrMagnitude > 0.0001f)
            {
                rigRoot.position += horizontalVelocity * deltaTime;
            }
        }

        /// <summary>
        /// Processes right-thumbstick horizontal input for snap or continuous smooth turning.
        /// Rotates the rig root around the player's head vertical axis.
        /// Public for deterministic unit testing.
        /// </summary>
        public bool ProcessTurning(Vector2 rightStick, float deltaTime)
        {
            float turnX = rightStick.x;

            if (_useSnapTurn)
            {
                if (Mathf.Abs(turnX) < _snapTurnResetThreshold)
                {
                    _snapTurnReady = true;
                    return false;
                }

                if (_snapTurnReady && Mathf.Abs(turnX) >= _snapTurnThreshold)
                {
                    _snapTurnReady = false;
                    float signedAngle = Mathf.Sign(turnX) * _snapTurnAngle;
                    RotateRigAroundHead(signedAngle);
                    OnSnapTurned?.Invoke(signedAngle);
                    return true;
                }

                return false;
            }

            if (Mathf.Abs(turnX) >= _turnDeadzone && deltaTime > 0f)
            {
                float angle = turnX * _smoothTurnSpeed * deltaTime;
                RotateRigAroundHead(angle);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Simulates a parabolic arc starting from origin with initialVelocity.
        /// Returns true if a scene collider is hit, along with hit info and sampled trajectory points.
        /// </summary>
        public bool SimulateTeleportArc(Vector3 origin, Vector3 initialVelocity, out RaycastHit hitInfo, out List<Vector3> points)
        {
            points = new List<Vector3>(_arcStepCount + 1) { origin };
            Vector3 currentPos = origin;
            Vector3 currentVel = initialVelocity;
            Vector3 gravityAcc = Vector3.up * _gravity;
            hitInfo = default;

            Transform rigRoot = _rigReferences != null ? _rigReferences.Root : transform;

            for (int i = 0; i < _arcStepCount; i++)
            {
                Vector3 nextVel = currentVel + gravityAcc * _arcStepTime;
                Vector3 nextPos = currentPos + currentVel * _arcStepTime + 0.5f * gravityAcc * (_arcStepTime * _arcStepTime);

                Vector3 segment = nextPos - currentPos;
                float dist = segment.magnitude;
                if (dist > 0.0001f)
                {
                    if (Physics.Raycast(currentPos, segment / dist, out RaycastHit hit, dist, _teleportRaycastMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.root != rigRoot)
                        {
                            hitInfo = hit;
                            points.Add(hit.point);
                            return true;
                        }
                    }
                }

                currentPos = nextPos;
                currentVel = nextVel;
                points.Add(currentPos);
            }

            return false;
        }

        /// <summary>
        /// Verifies whether the player's capsule can fit cleanly at targetFloorPoint
        /// without overlapping walls, door frames, or furniture.
        /// </summary>
        public bool HasCapsuleClearance(Vector3 targetFloorPoint)
        {
            float radius = _capsuleRadius;
            float height = _capsuleHeight;

            Vector3 bottom = targetFloorPoint + Vector3.up * (radius + 0.05f);
            Vector3 top = targetFloorPoint + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);

            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, _teleportRaycastMask, QueryTriggerInteraction.Ignore);
            Transform rigRoot = _rigReferences != null ? _rigReferences.Root : transform;

            foreach (var col in overlaps)
            {
                if (col == null || col.isTrigger)
                    continue;

                if (col.transform.root == rigRoot)
                    continue;

                // Hit a solid wall, obstacle, or closed door
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates whether a raycast hit represents a walkable floor surface with sufficient capsule clearance.
        /// </summary>
        public bool IsValidTeleportSurface(RaycastHit hit)
        {
            if (hit.collider == null)
                return false;

            // 1. Normal check: Must be walkable horizontal surface (not vertical wall or steep incline)
            if (hit.normal.y < _minFloorNormalY)
                return false;

            // 2. Capsule clearance check: Must not overlap walls, door frames, or furniture
            if (!HasCapsuleClearance(hit.point))
                return false;

            // 3. Floor bounds validation: Must be within investigation floor if assigned
            if (_evidenceManager != null && _evidenceManager.HasInvestigationFloor)
            {
                Bounds floorBounds = _evidenceManager.InvestigationFloorBounds;
                floorBounds.Expand(new Vector3(1.5f, 2.0f, 1.5f));
                if (!floorBounds.Contains(hit.point))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Teleports the VR rig so the player's head lands horizontally over targetWorldPosition
        /// while placing the rig base at the target floor height.
        /// </summary>
        public bool TeleportTo(Vector3 targetWorldPosition)
        {
            Transform rigRoot = _rigReferences != null ? _rigReferences.Root : transform;
            Transform head = (_rigReferences != null && _rigReferences.CenterEyeAnchor != null)
                ? _rigReferences.CenterEyeAnchor
                : rigRoot;

            Vector3 headOffset = head.position - rigRoot.position;
            headOffset.y = 0f;

            Vector3 destination = targetWorldPosition - headOffset;

            CharacterController cc = _rigReferences != null ? _rigReferences.CharacterController : GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (wasEnabled)
                cc.enabled = false;

            rigRoot.position = destination;
            _verticalVelocity = 0f;

            if (wasEnabled)
                cc.enabled = true;

            OnTeleported?.Invoke(targetWorldPosition);
            return true;
        }

        /// <summary>
        /// Casts an arc/ray from the controller and teleports if it hits a valid floor surface.
        /// </summary>
        public bool TryTeleportFromRay(out Vector3 teleportedPoint)
        {
            teleportedPoint = Vector3.zero;
            Transform rayOrigin = GetTeleportRayOrigin();
            if (rayOrigin == null)
                return false;

            Vector3 origin = rayOrigin.position;
            Vector3 velocity = rayOrigin.forward * _arcInitialSpeed;

            if (!SimulateTeleportArc(origin, velocity, out RaycastHit hit, out _))
            {
                Ray ray = new Ray(origin, rayOrigin.forward);
                if (!Physics.Raycast(ray, out hit, _maxTeleportDistance, _teleportRaycastMask, QueryTriggerInteraction.Ignore))
                    return false;
            }

            if (!IsValidTeleportSurface(hit))
                return false;

            teleportedPoint = hit.point;
            return TeleportTo(hit.point);
        }

        private void ProcessTeleportInput(Vector2 rightStick, bool buttonHeld, bool buttonReleased)
        {
            bool stickAiming = rightStick.y >= _teleportStickThreshold && Mathf.Abs(rightStick.x) < 0.45f;
            bool aimingNow = stickAiming || buttonHeld;

            if (aimingNow)
            {
                _isAimingTeleport = true;
                Transform rayOrigin = GetTeleportRayOrigin();
                Vector3 origin = rayOrigin.position;
                Vector3 initialVel = rayOrigin.forward * _arcInitialSpeed;

                bool hitObstacle = SimulateTeleportArc(origin, initialVel, out RaycastHit hit, out List<Vector3> points);

                if (_arcRenderer != null)
                {
                    _arcRenderer.enabled = true;
                    _arcRenderer.positionCount = points.Count;
                    _arcRenderer.SetPositions(points.ToArray());
                }

                _hasValidTeleportTarget = hitObstacle && IsValidTeleportSurface(hit);

                if (_hasValidTeleportTarget)
                {
                    _pendingTeleportPoint = hit.point;
                    SetArcVisualState(true, hit.point, hit.normal);
                }
                else
                {
                    SetArcVisualState(false, hitObstacle ? hit.point : Vector3.zero, hitObstacle ? hit.normal : Vector3.up);
                }
            }
            else if (_isAimingTeleport || buttonReleased)
            {
                if (_hasValidTeleportTarget)
                {
                    TeleportTo(_pendingTeleportPoint);
                }

                _isAimingTeleport = false;
                _hasValidTeleportTarget = false;

                if (_arcRenderer != null)
                {
                    _arcRenderer.enabled = false;
                    _arcRenderer.positionCount = 0;
                }

                if (_teleportReticle != null)
                {
                    _teleportReticle.SetActive(false);
                }
            }
        }

        private void SetArcVisualState(bool isValid, Vector3 hitPoint, Vector3 hitNormal)
        {
            Color color = isValid ? ValidArcColor : InvalidArcColor;

            if (_arcRenderer != null)
            {
                _arcRenderer.startColor = color;
                _arcRenderer.endColor = color;
            }

            if (_reticleMaterial != null)
            {
                _reticleMaterial.color = color;
            }

            if (_teleportReticle != null)
            {
                if (isValid)
                {
                    _teleportReticle.SetActive(true);
                    _teleportReticle.transform.position = hitPoint + Vector3.up * 0.015f;
                    if (hitNormal.sqrMagnitude > 0.001f)
                        _teleportReticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
                }
                else
                {
                    _teleportReticle.SetActive(false);
                }
            }
        }

        private void RotateRigAroundHead(float angleDegrees)
        {
            Transform rigRoot = _rigReferences != null ? _rigReferences.Root : transform;
            Transform head = (_rigReferences != null && _rigReferences.CenterEyeAnchor != null)
                ? _rigReferences.CenterEyeAnchor
                : rigRoot;

            Vector3 pivot = new Vector3(head.position.x, rigRoot.position.y, head.position.z);
            rigRoot.RotateAround(pivot, Vector3.up, angleDegrees);
        }

        private void SyncCharacterControllerToHead()
        {
            if (_rigReferences == null || _rigReferences.CharacterController == null || _rigReferences.CenterEyeAnchor == null)
                return;

            CharacterController cc = _rigReferences.CharacterController;
            if (!cc.enabled)
                return;

            Vector3 localHead = transform.InverseTransformPoint(_rigReferences.CenterEyeAnchor.position);
            float height = Mathf.Clamp(localHead.y, 1.0f, 2.2f);
            cc.height = height;
            cc.center = new Vector3(localHead.x, height * 0.5f, localHead.z);
        }

        private Transform GetTeleportRayOrigin()
        {
            if (_rigReferences != null)
            {
                if (_rigReferences.RightHandAnchor != null)
                    return _rigReferences.RightHandAnchor;
                if (_rigReferences.LeftHandAnchor != null)
                    return _rigReferences.LeftHandAnchor;
                if (_rigReferences.CenterEyeAnchor != null)
                    return _rigReferences.CenterEyeAnchor;
            }
            return transform;
        }

        private Vector2 ReadLeftThumbstick()
        {
            try
            {
                Vector2 ovrStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
                if (ovrStick.sqrMagnitude > 0.001f)
                    return ovrStick;

                Vector2 rawLeft = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
                if (rawLeft.sqrMagnitude > 0.001f)
                    return rawLeft;
            }
            catch
            {
                // Fallback when OVR runtime is not active
            }

            if (Gamepad.current != null)
            {
                Vector2 gp = Gamepad.current.leftStick.ReadValue();
                if (gp.sqrMagnitude > 0.001f)
                    return gp;
            }

            // Keyboard WASD support for Windows Standalone and Editor desktop mode
            if (Keyboard.current != null)
            {
                Vector2 kb = Vector2.zero;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kb.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kb.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kb.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kb.x -= 1f;
                if (kb.sqrMagnitude > 0.001f)
                    return kb.normalized;
            }

            return Vector2.zero;
        }

        private Vector2 ReadRightThumbstick()
        {
            try
            {
                Vector2 ovrStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
                if (ovrStick.sqrMagnitude > 0.001f)
                    return ovrStick;

                Vector2 rawRight = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
                if (rawRight.sqrMagnitude > 0.001f)
                    return rawRight;
            }
            catch
            {
                // Fallback when OVR runtime is not active
            }

            if (Gamepad.current != null)
            {
                Vector2 gp = Gamepad.current.rightStick.ReadValue();
                if (gp.sqrMagnitude > 0.001f)
                    return gp;
            }

            // Keyboard Q/E turn support for Windows Standalone and Editor desktop mode
            if (Keyboard.current != null)
            {
                Vector2 kbTurn = Vector2.zero;
                if (Keyboard.current.qKey.isPressed) kbTurn.x -= 1f;
                if (Keyboard.current.eKey.isPressed) kbTurn.x += 1f;
                if (kbTurn.sqrMagnitude > 0.001f)
                    return kbTurn;
            }

            return Vector2.zero;
        }

        private bool ReadRightStickClick()
        {
            try
            {
                if (OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick) ||
                    OVRInput.GetDown(OVRInput.RawButton.RThumbstick))
                {
                    return true;
                }
            }
            catch
            {
                // Fallback when OVR runtime is not active
            }

            if (Gamepad.current != null && Gamepad.current.rightStickButton.wasPressedThisFrame)
                return true;

            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                return true;

            return false;
        }

        private bool ReadTeleportButtonHeld()
        {
            try
            {
                if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                    OVRInput.Get(OVRInput.RawButton.B))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore when OVR runtime is inactive
            }

            return false;
        }

        private bool ReadTeleportButtonReleased()
        {
            try
            {
                if (OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                    OVRInput.GetUp(OVRInput.RawButton.B))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore when OVR runtime is inactive
            }

            return false;
        }

        private void SetupTeleportReticle()
        {
            if (_teleportReticle != null)
                return;

            _teleportReticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _teleportReticle.name = "VRTeleportReticle";
            _teleportReticle.transform.SetParent(transform, false);
            _teleportReticle.transform.localScale = new Vector3(0.35f, 0.005f, 0.35f);

            Collider col = _teleportReticle.GetComponent<Collider>();
            if (col != null)
                DestroyImmediate(col);

            Renderer rend = _teleportReticle.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _reticleMaterial = new Material(shader) { name = "TeleportReticleMat" };
                    _reticleMaterial.color = ValidArcColor;
                    rend.sharedMaterial = _reticleMaterial;
                }
            }

            _teleportReticle.SetActive(false);
        }

        private void SetupTeleportArcRenderer()
        {
            if (_arcRenderer != null)
                return;

            Transform existingArc = transform.Find("VRArcRenderer");
            GameObject arcGo = existingArc != null ? existingArc.gameObject : new GameObject("VRArcRenderer");
            arcGo.transform.SetParent(transform, false);

            _arcRenderer = arcGo.GetComponent<LineRenderer>();
            if (_arcRenderer == null)
                _arcRenderer = arcGo.AddComponent<LineRenderer>();

            _arcRenderer.startWidth = 0.03f;
            _arcRenderer.endWidth = 0.03f;
            _arcRenderer.positionCount = 0;
            _arcRenderer.useWorldSpace = true;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _arcMaterial = new Material(shader) { name = "TeleportArcMat" };
                _arcRenderer.sharedMaterial = _arcMaterial;
            }

            _arcRenderer.startColor = ValidArcColor;
            _arcRenderer.endColor = ValidArcColor;
            _arcRenderer.enabled = false;
        }

        private void ResolveReferences()
        {
            if (_rigReferences == null)
                _rigReferences = GetComponent<VRRigReferences>();
            if (_rigReferences == null)
                _rigReferences = VRRigReferences.Instance;

            if (_evidenceManager == null)
                _evidenceManager = FindAnyObjectByType<CrimeSceneEvidenceManager>();
        }
    }
}
