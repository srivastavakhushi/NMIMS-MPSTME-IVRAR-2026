using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using CrimeSceneVR.Core;
using CrimeSceneVR.Session;
using CrimeSceneVR.Telemetry;
using CrimeSceneVR.VR;

namespace CrimeSceneVR.Interaction
{
    /// <summary>
    /// Desktop Testing Rig for CrimeScene.unity.
    /// Allows exploring the crime scene, selecting, inspecting, and tagging evidence
    /// using mouse and keyboard without requiring a physical VR headset or Meta XR Simulator.
    /// Includes CharacterController physics support to accurately reproduce VR locomotion collision.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("Crime Scene VR/Desktop/Desktop Testing Rig")]
    public class DesktopTestingRig : MonoBehaviour
    {
        private static DesktopTestingRig _instance;
        public static DesktopTestingRig Instance => _instance;

        [Header("Mode Configuration")]
        [Tooltip("If true, activates desktop testing mode by default in the Editor.")]
        [SerializeField] private bool _forceDesktopMode = true;

        [Tooltip("If true, automatically activates desktop testing mode when no VR HMD is detected.")]
        [SerializeField] private bool _autoActivateWhenNoVR = true;

        [Header("Camera & Visuals")]
        [SerializeField] private Camera _desktopCamera;
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private Vector3 _initialSpawnPosition = new Vector3(-5.637f, 2.68f, 0.145f);

        [Header("Movement & Look")]
        [SerializeField] private float _moveSpeed = 3.0f;
        [SerializeField] private float _sprintMultiplier = 2.2f;
        [SerializeField] private float _verticalSpeed = 2.5f;
        [SerializeField] private float _lookSensitivity = 0.15f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private bool _lockCursorOnLeftClick = false;

        [Header("Evidence Interaction")]
        [SerializeField] private EvidenceInteraction _evidenceInteraction;
        [SerializeField] private CrimeSceneEvidenceManager _evidenceManager;

        private bool _isDesktopModeActive = false;
        private Camera _vrCenterCamera;
        private AudioListener _vrAudioListener;
        private string _previousVrCameraTag = "MainCamera";
        private float _yaw;
        private float _pitch;
        private bool _lookInitialized;
        private bool _cursorLocked = false;
        private EvidenceUI _evidenceUi;
        private CharacterController _characterController;
        private float _verticalVelocity = 0f;

        // Collision state
        private Collider _lastBlockingCollider;

        public bool IsDesktopModeActive => _isDesktopModeActive;
        public Camera DesktopCamera => _desktopCamera;
        public CharacterController CharacterController => _characterController;
        public Collider LastBlockingCollider => _lastBlockingCollider;

        private void Awake()
        {
            if (gameObject.name.StartsWith("Test_") && Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance == null || _instance == this || !Application.isPlaying)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            EnsureComponents();
        }

        private void Start()
        {
            EnsureComponents();

            bool vrPresent = IsVrHeadsetPresent();
            // In Editor desktop testing, prefer desktop mode unless force-disabled
            if (_forceDesktopMode || (_autoActivateWhenNoVR && !vrPresent))
            {
                SetDesktopModeActive(true);
            }
            else
            {
                SetDesktopModeActive(false);
            }
        }

        private void Update()
        {
            if (!_isDesktopModeActive) return;

            Keyboard keyboard = Keyboard.current ?? InputSystem.GetDevice<Keyboard>();
            Mouse mouse = Mouse.current ?? InputSystem.GetDevice<Mouse>();

            // Optional cursor lock toggle with Tab key
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                ToggleCursorLock();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && _cursorLocked)
            {
                SetCursorLock(false);
            }

            // Keyboard and Mouse Navigation
            UpdateMovementAndLook(keyboard, mouse);

            // Evidence Interaction Hotkeys
            ProcessEvidenceHotkeys(keyboard, mouse);
        }

        public void SetDesktopModeActive(bool active)
        {
            _isDesktopModeActive = active;
            LocateVrRigCamera();

            if (_desktopCamera == null)
            {
                EnsureComponents();
            }

            if (active)
            {
                if (_characterController == null)
                {
                    _characterController = GetComponent<CharacterController>();
                }
                if (_characterController != null)
                {
                    _characterController.enabled = true;
                }
                // 1. Configure and enable Desktop Camera
                if (_desktopCamera != null)
                {
                    _desktopCamera.gameObject.SetActive(true);
                    _desktopCamera.enabled = true;
                    _desktopCamera.tag = "MainCamera";
                    _desktopCamera.targetDisplay = 0;
                    _desktopCamera.nearClipPlane = 0.05f;
                    _desktopCamera.farClipPlane = 1000f;

                    var uac = _desktopCamera.GetComponent<UniversalAdditionalCameraData>();
                    if (uac == null)
                        uac = _desktopCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    uac.allowXRRendering = false;
                    uac.renderType = CameraRenderType.Base;
                    uac.renderPostProcessing = false;
                }

                // 2. Enable Desktop AudioListener & disable VR AudioListener to prevent duplicate listener warning
                if (_audioListener != null) _audioListener.enabled = true;
                if (_vrAudioListener != null) _vrAudioListener.enabled = false;

                // 3. Disable VR Center Eye camera to avoid multiple MainCameras rendering
                if (_vrCenterCamera != null)
                {
                    _previousVrCameraTag = _vrCenterCamera.tag;
                    _vrCenterCamera.tag = "Untagged";
                    _vrCenterCamera.enabled = false;
                }

                // 4. Update VRRigReferences so TelemetryCollector tracks desktop camera
                var rigRef = VRRigReferences.Instance;
                if (rigRef != null && _desktopCamera != null)
                {
                    rigRef.SetMainCamera(_desktopCamera);
                }
                if (rigRef != null && rigRef.CharacterController != null)
                {
                    rigRef.CharacterController.enabled = false;
                }

                // 5. Connect EvidenceUI
                ConnectEvidenceUi();

                // 6. Reset look rotation
                if (_desktopCamera != null)
                {
                    Vector3 euler = _desktopCamera.transform.eulerAngles;
                    _yaw = euler.y;
                    _pitch = euler.x;
                    if (_pitch > 180f) _pitch -= 360f;
                    _lookInitialized = true;
                }
            }
            else
            {
                // Disable Desktop Camera & Listener
                if (_desktopCamera != null)
                {
                    _desktopCamera.tag = "Untagged";
                    _desktopCamera.enabled = false;
                    _desktopCamera.gameObject.SetActive(false);
                }
                if (_audioListener != null) _audioListener.enabled = false;

                // Re-enable VR Rig camera and listener
                if (_vrCenterCamera != null)
                {
                    _vrCenterCamera.tag = string.IsNullOrEmpty(_previousVrCameraTag) ? "MainCamera" : _previousVrCameraTag;
                    _vrCenterCamera.enabled = true;
                }
                if (_vrAudioListener != null) _vrAudioListener.enabled = true;

                // Restore VRRigReferences
                var rigRef = VRRigReferences.Instance;
                if (rigRef != null && _vrCenterCamera != null)
                {
                    rigRef.SetMainCamera(_vrCenterCamera);
                }
                if (rigRef != null && rigRef.CharacterController != null)
                {
                    rigRef.CharacterController.enabled = true;
                }

                SetCursorLock(false);
            }
        }

        private void UpdateMovementAndLook(Keyboard keyboard, Mouse mouse)
        {
            if (_desktopCamera == null) return;

            Transform camTransform = _desktopCamera.transform;

            // Mouse Look (active when holding Right Mouse Button or when cursor is locked)
            if (!_lookInitialized)
            {
                Vector3 euler = camTransform.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
                if (_pitch > 180f) _pitch -= 360f;
                _lookInitialized = true;
            }

            if (_lockCursorOnLeftClick && mouse != null && mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
            {
                SetCursorLock(true);
            }

            bool shouldLook = (mouse != null) && (mouse.rightButton.isPressed || _cursorLocked);
            if (shouldLook && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * _lookSensitivity;
                _pitch -= delta.y * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
                camTransform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            // Keyboard Movement Input (WASD/Arrows + Q/E/Space/C)
            Vector3 input = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

                // Vertical movement / elevation
                if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed || keyboard.pageUpKey.isPressed) input.y += 1f;
                if (keyboard.qKey.isPressed || keyboard.cKey.isPressed || keyboard.pageDownKey.isPressed) input.y -= 1f;
            }

            bool isSprinting = (keyboard != null && keyboard.leftShiftKey.isPressed);

            // Movement via CharacterController or direct translation
            SimulateMove(input, Time.unscaledDeltaTime, isSprinting);
        }

        /// <summary>
        /// Moves the rig by a given input vector and duration. Useful for both unit testing and programmatic movement.
        /// </summary>
        public Vector3 SimulateMove(Vector3 input, float deltaTime, bool sprint = false)
        {
            if (_desktopCamera == null)
            {
                EnsureComponents();
            }

            Transform camTransform = _desktopCamera != null ? _desktopCamera.transform : transform;
            float sprintMul = sprint ? _sprintMultiplier : 1f;
            float speed = _moveSpeed * sprintMul;

            Vector3 forward = camTransform.forward;
            Vector3 right = camTransform.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
            else forward = Vector3.forward;

            if (right.sqrMagnitude > 0.0001f) right.Normalize();
            else right = Vector3.right;

            Vector3 horizontalMotion = (forward * input.z + right * input.x);
            if (horizontalMotion.sqrMagnitude > 1f)
                horizontalMotion.Normalize();

            Vector3 posBefore = transform.position;
            if (_characterController != null && _characterController.enabled)
            {
                if (_characterController.isGrounded && _verticalVelocity < 0f)
                    _verticalVelocity = -0.5f;
                else
                    _verticalVelocity += _gravity * deltaTime;

                Vector3 displacement = (horizontalMotion * speed + Vector3.up * _verticalVelocity) * deltaTime;

                // Support vertical fly override with Q/E
                if (Mathf.Abs(input.y) > 0.001f)
                {
                    displacement.y = input.y * _verticalSpeed * deltaTime;
                    _verticalVelocity = 0f;
                }

                _characterController.Move(displacement);
            }
            else
            {
                Vector3 motion = (horizontalMotion * speed) + (Vector3.up * (input.y * _verticalSpeed));
                transform.position += motion * deltaTime;
            }

            Vector3 posAfter = transform.position;
            return posAfter - posBefore;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider == null) return;

            // Vertical surface or obstacle
            if (hit.normal.y < 0.7f)
            {
                _lastBlockingCollider = hit.collider;
            }
        }

        private void ProcessEvidenceHotkeys(Keyboard keyboard, Mouse mouse)
        {
            if (_evidenceInteraction == null)
            {
                _evidenceInteraction = GetComponent<EvidenceInteraction>();
                if (_evidenceInteraction == null) return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // Raycast from camera center
                Ray ray = _desktopCamera != null
                    ? _desktopCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                    : new Ray(transform.position, transform.forward);

                RaycastHit[] hits = Physics.RaycastAll(ray, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (hits != null && hits.Length > 0)
                {
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    EvidenceObject evidence = null;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var ev = hits[i].collider.GetComponentInParent<EvidenceObject>();
                        if (ev != null && hits[i].distance <= hits[0].distance + 1.0f)
                        {
                            evidence = ev;
                            break;
                        }
                    }
                    if (evidence != null)
                    {
                        _evidenceInteraction.SelectEvidence(evidence);
                    }
                }
            }

            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                if (_evidenceInteraction.SelectedEvidence != null)
                {
                    _evidenceManager?.TryInspect(_evidenceInteraction.SelectedEvidence);
                }
            }

            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                if (_evidenceInteraction.SelectedEvidence != null && _desktopCamera != null)
                {
                    Ray ray = _desktopCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    RaycastHit[] hits = Physics.RaycastAll(ray, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (hits != null && hits.Length > 0)
                    {
                        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        RaycastHit chosenHit = hits[0];
                        for (int i = 0; i < hits.Length; i++)
                        {
                            var ev = hits[i].collider.GetComponentInParent<EvidenceObject>();
                            if (ev != null && hits[i].distance <= hits[0].distance + 1.0f)
                            {
                                chosenHit = hits[i];
                                break;
                            }
                        }
                        _evidenceManager?.TryTag(_evidenceInteraction.SelectedEvidence, chosenHit.point);
                    }
                }
            }
        }

        private void ConnectEvidenceUi()
        {
            if (_evidenceManager == null)
                _evidenceManager = FindAnyObjectByType<CrimeSceneEvidenceManager>();

            if (_evidenceUi == null)
                _evidenceUi = FindAnyObjectByType<EvidenceUI>();

            if (_evidenceInteraction == null)
                _evidenceInteraction = GetComponent<EvidenceInteraction>();

            if (_evidenceInteraction != null)
            {
                _evidenceInteraction.EnableDesktopNavigation = false;
            }

            if (_evidenceUi != null && _evidenceManager != null && _evidenceInteraction != null)
            {
                _evidenceUi.Configure(_evidenceManager, _evidenceInteraction);
            }
        }

        private void LocateVrRigCamera()
        {
            if (_vrCenterCamera != null) return;

            var rigRef = VRRigReferences.Instance;
            if (rigRef != null && rigRef.CenterEyeAnchor != null)
            {
                _vrCenterCamera = rigRef.CenterEyeAnchor.GetComponent<Camera>();
                _vrAudioListener = rigRef.CenterEyeAnchor.GetComponent<AudioListener>();
            }

            if (_vrCenterCamera == null)
            {
                var centerGO = GameObject.Find("CenterEyeAnchor");
                if (centerGO != null)
                {
                    _vrCenterCamera = centerGO.GetComponent<Camera>();
                    _vrAudioListener = centerGO.GetComponent<AudioListener>();
                }
            }
        }

        private void EnsureComponents()
        {
            if (_desktopCamera == null)
            {
                _desktopCamera = GetComponent<Camera>();
                if (_desktopCamera == null)
                {
                    _desktopCamera = gameObject.AddComponent<Camera>();
                }
            }

            _desktopCamera.nearClipPlane = 0.05f;
            _desktopCamera.farClipPlane = 1000f;
            _desktopCamera.clearFlags = CameraClearFlags.Skybox;
            _desktopCamera.cullingMask = ~0; // Everything
            _desktopCamera.fieldOfView = 60f;
            _desktopCamera.targetDisplay = 0;

            var uac = _desktopCamera.GetComponent<UniversalAdditionalCameraData>();
            if (uac == null)
            {
                uac = _desktopCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            uac.allowXRRendering = false;
            uac.renderType = CameraRenderType.Base;
            uac.renderPostProcessing = false;

            // CharacterController configuration matching VR rig
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
                if (_characterController == null)
                {
                    _characterController = gameObject.AddComponent<CharacterController>();
                }
            }
            _characterController.height = 1.65f;
            _characterController.radius = 0.25f;
            _characterController.center = new Vector3(0f, -0.80f, 0f);
            _characterController.skinWidth = 0.02f;
            _characterController.stepOffset = 0.25f;
            _characterController.enabled = true;

            if (_audioListener == null)
            {
                _audioListener = GetComponent<AudioListener>();
                if (_audioListener == null)
                {
                    _audioListener = gameObject.AddComponent<AudioListener>();
                }
            }

            if (_evidenceInteraction == null)
            {
                _evidenceInteraction = GetComponent<EvidenceInteraction>();
                if (_evidenceInteraction == null)
                {
                    _evidenceInteraction = gameObject.AddComponent<EvidenceInteraction>();
                }
            }

            if (_evidenceManager == null)
            {
                _evidenceManager = FindAnyObjectByType<CrimeSceneEvidenceManager>();
            }
        }

        public void ToggleCursorLock()
        {
            SetCursorLock(!_cursorLocked);
        }

        public void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private bool IsVrHeadsetPresent()
        {
            try
            {
                return OVRManager.isHmdPresent;
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            if (_cursorLocked)
            {
                SetCursorLock(false);
            }
        }
    }
}
