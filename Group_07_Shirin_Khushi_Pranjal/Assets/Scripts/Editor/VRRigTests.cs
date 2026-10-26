using NUnit.Framework;
using UnityEngine;
using CrimeSceneVR.Core;
using CrimeSceneVR.VR;
using CrimeSceneVR.Interaction;

namespace CrimeSceneVR.Tests
{
    [TestFixture]
    public class VRRigTests
    {
        private GameObject _rigRoot;
        private GameObject _desktopRigGo;
        private GameObject _testFloorGo;
        private GameObject _testWallGo;

        [TearDown]
        public void TearDown()
        {
            if (_testWallGo != null)
            {
                Object.DestroyImmediate(_testWallGo);
                _testWallGo = null;
            }
            if (_testFloorGo != null)
            {
                Object.DestroyImmediate(_testFloorGo);
                _testFloorGo = null;
            }
            if (_desktopRigGo != null)
            {
                Object.DestroyImmediate(_desktopRigGo);
                _desktopRigGo = null;
            }
            if (_rigRoot != null)
            {
                Object.DestroyImmediate(_rigRoot);
                _rigRoot = null;
            }
            VRRigReferences.Instance = null;

            var strayRig = GameObject.Find("Test_DesktopDebugRig");
            if (strayRig != null)
            {
                Object.DestroyImmediate(strayRig);
            }
            var strayOvr = GameObject.Find("Test_OVRCameraRig");
            if (strayOvr != null)
            {
                Object.DestroyImmediate(strayOvr);
            }
        }

        [Test]
        public void VRRigReferences_ResolvesAnchorsAndCamera_Correctly()
        {
            _rigRoot = CreateTestRigHierarchy(out Transform trackingSpace, out Transform centerEye, out Transform leftHand, out Transform rightHand, out Camera cam);

            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            rigRef.ResolveReferences();

            Assert.IsNotNull(VRRigReferences.Instance, "VRRigReferences singleton instance must be set.");
            Assert.AreEqual(_rigRoot.transform, rigRef.Root);
            Assert.AreEqual(trackingSpace, rigRef.TrackingSpace);
            Assert.AreEqual(centerEye, rigRef.CenterEyeAnchor);
            Assert.AreEqual(leftHand, rigRef.LeftHandAnchor);
            Assert.AreEqual(rightHand, rigRef.RightHandAnchor);
            Assert.AreEqual(cam, rigRef.MainCamera);
            Assert.AreEqual(0.05f, rigRef.MainCamera.nearClipPlane, 0.001f);
        }

        [Test]
        public void VRRigReferences_NearClipPlane_EnforcedAtFiveCentimeters()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out Camera cam);
            cam.nearClipPlane = 0.3f;

            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            rigRef.ResolveReferences();

            Assert.AreEqual(0.05f, rigRef.MainCamera.nearClipPlane, 0.001f, "ResolveReferences must enforce nearClipPlane to 0.05m.");
        }

        [Test]
        public void VRLocomotionController_SimulateMove_TranslatesRigForward()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out Transform centerEye, out _, out _, out _);
            centerEye.rotation = Quaternion.identity;

            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);
            loco.MoveSpeed = 2.0f;

            Vector3 initialPos = _rigRoot.transform.position;
            loco.SimulateMove(new Vector2(0f, 1f), 0.5f); // Move forward for 0.5s at 2 m/s = 1.0m

            Assert.AreEqual(initialPos.z + 1.0f, _rigRoot.transform.position.z, 0.01f, "Rig must translate forward along head direction.");
        }

        [Test]
        public void VRLocomotionController_DefaultsTo_ContinuousSmoothTurn()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);

            Assert.IsFalse(loco.UseSnapTurn, "VR locomotion must default to smooth continuous turning.");
            Assert.AreEqual(50f, loco.SmoothTurnSpeed, 0.1f, "Default smooth turn speed must be 50 deg/s (within 45-60 deg/s range).");
            Assert.AreEqual(45f, loco.SnapTurnAngle, 0.1f, "Fallback snap turn angle must be 45 degrees.");
        }

        [Test]
        public void VRLocomotionController_SmoothTurn_RotatesRigContinuously()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);
            loco.UseSnapTurn = false;
            loco.SmoothTurnSpeed = 50f;

            // Rotate with stick = +1.0 for 0.5s at 50 deg/s -> expected 25 degrees
            bool turned = loco.ProcessTurning(new Vector2(1.0f, 0f), 0.5f);

            Assert.IsTrue(turned, "Smooth turning must process non-zero stick input beyond deadzone.");
            Assert.AreEqual(25f, _rigRoot.transform.eulerAngles.y, 0.2f, "Rig must rotate continuously at smoothTurnSpeed * deltaTime.");
        }

        [Test]
        public void VRLocomotionController_TurnModeToggle_SwitchesBetweenSmoothAndSnap()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);

            Assert.IsFalse(loco.UseSnapTurn, "Initially smooth turn mode.");

            bool eventFired = false;
            bool eventValue = false;
            loco.OnTurnModeChanged += (snap) => { eventFired = true; eventValue = snap; };

            loco.ToggleTurnMode();
            Assert.IsTrue(loco.UseSnapTurn, "Toggle must switch to snap turn mode.");
            Assert.IsTrue(eventFired, "OnTurnModeChanged event must fire.");
            Assert.IsTrue(eventValue, "Event value must reflect new snap mode.");

            loco.ToggleTurnMode();
            Assert.IsFalse(loco.UseSnapTurn, "Second toggle must switch back to smooth turn mode.");
        }

        [Test]
        public void VRLocomotionController_ParabolicArc_SimulatesTrajectoryAndDetectsHit()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            _rigRoot.transform.position = new Vector3(100f, 100f, 100f);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);

            // Create an isolated test floor collider at Y = 100
            _testFloorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _testFloorGo.name = "TestFloor";
            _testFloorGo.transform.position = new Vector3(100f, 99.5f, 105f);
            _testFloorGo.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();

            Vector3 origin = new Vector3(100f, 101.6f, 100f);
            Vector3 forwardVelocity = new Vector3(0f, 0f, 8f); // Fired horizontally forward at 8 m/s

            bool hit = loco.SimulateTeleportArc(origin, forwardVelocity, out RaycastHit hitInfo, out var points);

            Assert.IsTrue(hit, "Parabolic arc must intersect the floor plane under gravity.");
            Assert.IsNotNull(hitInfo.collider, "Hit collider must not be null.");
            Assert.AreEqual(_testFloorGo.GetComponent<Collider>(), hitInfo.collider, "Arc must hit test floor.");
            Assert.Greater(points.Count, 2, "Trajectory must contain multiple simulation steps.");
            Assert.AreEqual(100f, hitInfo.point.y, 0.1f, "Floor surface hit Y should match test floor surface.");
        }

        [Test]
        public void VRLocomotionController_CapsuleClearance_RejectsWhenBlockedByWall()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            _rigRoot.transform.position = new Vector3(100f, 100f, 100f);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);

            // Place an isolated solid obstacle at (100, 101, 105)
            _testWallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _testWallGo.name = "TestWall";
            _testWallGo.transform.position = new Vector3(100f, 101f, 105f);
            _testWallGo.transform.localScale = new Vector3(2f, 2f, 0.5f);
            Physics.SyncTransforms();

            // Target inside the wall
            bool clearanceInsideWall = loco.HasCapsuleClearance(new Vector3(100f, 100f, 105f));
            Assert.IsFalse(clearanceInsideWall, "Landing location inside a wall must fail capsule clearance.");

            // Target in open space away from the wall
            bool clearanceOpenSpace = loco.HasCapsuleClearance(new Vector3(100f, 100f, 115f));
            Assert.IsTrue(clearanceOpenSpace, "Open space without obstacles must pass capsule clearance.");
        }

        [Test]
        public void VRLocomotionController_ProcessTurning_SnapTurnsOnceUntilReset()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out _, out _, out _, out _);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);
            loco.UseSnapTurn = true;
            loco.SnapTurnAngle = 45f;

            // Flick right stick to +1.0
            bool firstTurn = loco.ProcessTurning(new Vector2(1.0f, 0f), 0.1f);
            Assert.IsTrue(firstTurn, "First flick above threshold must trigger snap turn.");
            Assert.AreEqual(45f, _rigRoot.transform.eulerAngles.y, 0.1f);

            // Holding right stick at +1.0 must not re-trigger until released below reset threshold
            bool heldTurn = loco.ProcessTurning(new Vector2(1.0f, 0f), 0.1f);
            Assert.IsFalse(heldTurn, "Holding thumbstick must not repeat snap turn until reset.");

            // Release stick to 0, then flick again
            loco.ProcessTurning(Vector2.zero, 0.1f);
            bool secondTurn = loco.ProcessTurning(new Vector2(1.0f, 0f), 0.1f);
            Assert.IsTrue(secondTurn, "Snap turn must trigger again after stick resets to neutral.");
            Assert.AreEqual(90f, _rigRoot.transform.eulerAngles.y, 0.1f);
        }

        [Test]
        public void VRLocomotionController_TeleportTo_AlignsHeadWithTargetPoint()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out Transform centerEye, out _, out _, out _);
            // Offset head 0.2m on X inside tracking space
            centerEye.localPosition = new Vector3(0.2f, 1.6f, -0.1f);

            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            var loco = _rigRoot.AddComponent<VRLocomotionController>();
            loco.Configure(rigRef);

            Vector3 targetFloorPoint = new Vector3(3.0f, 0.0f, 4.0f);
            bool teleported = loco.TeleportTo(targetFloorPoint);

            Assert.IsTrue(teleported);
            Assert.AreEqual(targetFloorPoint.x, centerEye.position.x, 0.001f, "Head world X must match teleport target X.");
            Assert.AreEqual(targetFloorPoint.z, centerEye.position.z, 0.001f, "Head world Z must match teleport target Z.");
            Assert.AreEqual(targetFloorPoint.y, _rigRoot.transform.position.y, 0.001f, "Rig root Y must match floor target Y.");
        }

        [Test]
        public void DesktopTestingRig_Activation_ConfiguresCameraAndUpdatesVRRigReferences()
        {
            _rigRoot = CreateTestRigHierarchy(out _, out Transform centerEye, out _, out _, out Camera vrCam);
            var rigRef = _rigRoot.AddComponent<VRRigReferences>();
            rigRef.ResolveReferences();
            VRRigReferences.Instance = rigRef;

            _desktopRigGo = new GameObject("Test_DesktopDebugRig");
            try
            {
                var desktopRig = _desktopRigGo.AddComponent<DesktopTestingRig>();

                desktopRig.SetDesktopModeActive(true);

                Assert.IsTrue(desktopRig.IsDesktopModeActive, "Desktop mode should be active.");
                Assert.IsNotNull(desktopRig.DesktopCamera, "Desktop camera must be configured.");
                Assert.IsFalse(desktopRig.DesktopCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering, "Desktop camera must disable XR rendering.");
                Assert.AreEqual("MainCamera", desktopRig.DesktopCamera.tag, "Desktop camera should have MainCamera tag.");
                Assert.AreEqual(desktopRig.DesktopCamera, rigRef.MainCamera, "VRRigReferences.MainCamera should point to desktop camera.");
                Assert.AreEqual(desktopRig.DesktopCamera.transform.position, rigRef.HeadPosition, "HeadPosition should follow desktop camera.");

                // Deactivate and verify restore
                desktopRig.SetDesktopModeActive(false);
                Assert.IsFalse(desktopRig.IsDesktopModeActive);
                Assert.AreEqual(vrCam, rigRef.MainCamera, "VRRigReferences.MainCamera should revert to VR center camera.");
            }
            finally
            {
                if (_desktopRigGo != null)
                {
                    Object.DestroyImmediate(_desktopRigGo);
                    _desktopRigGo = null;
                }
            }
        }

        [Test]
        public void DesktopTestingRig_SimulateMove_Forward_TranslatesPosition()
        {
            _desktopRigGo = new GameObject("Test_DesktopDebugRig");
            try
            {
                var desktopRig = _desktopRigGo.AddComponent<DesktopTestingRig>();
                Vector3 initialPos = _desktopRigGo.transform.position;
                Vector3 delta = desktopRig.SimulateMove(new Vector3(0f, 0f, 1f), 0.5f);

                Assert.Greater(delta.z, 0f, "SimulateMove forward must produce positive Z displacement.");
                Assert.AreEqual(initialPos.z + 1.5f, _desktopRigGo.transform.position.z, 0.05f, "Forward move at 3 m/s for 0.5s must equal 1.5m.");
            }
            finally
            {
                if (_desktopRigGo != null)
                {
                    Object.DestroyImmediate(_desktopRigGo);
                    _desktopRigGo = null;
                }
            }
        }

        [Test]
        public void DesktopTestingRig_SimulateMove_Strafe_TranslatesPosition()
        {
            _desktopRigGo = new GameObject("Test_DesktopDebugRig");
            try
            {
                var desktopRig = _desktopRigGo.AddComponent<DesktopTestingRig>();
                Vector3 initialPos = _desktopRigGo.transform.position;
                Vector3 delta = desktopRig.SimulateMove(new Vector3(1f, 0f, 0f), 0.5f);

                Assert.Greater(delta.x, 0f, "SimulateMove rightward must produce positive X displacement.");
                Assert.AreEqual(initialPos.x + 1.5f, _desktopRigGo.transform.position.x, 0.05f, "Right strafe at 3 m/s for 0.5s must equal 1.5m.");
            }
            finally
            {
                if (_desktopRigGo != null)
                {
                    Object.DestroyImmediate(_desktopRigGo);
                    _desktopRigGo = null;
                }
            }
        }

        [Test]
        public void DesktopTestingRig_EvidenceInteraction_DesktopNavigationDisabled()
        {
            _desktopRigGo = new GameObject("Test_DesktopDebugRig");
            try
            {
                var evidenceInteraction = _desktopRigGo.AddComponent<EvidenceInteraction>();
                var desktopRig = _desktopRigGo.AddComponent<DesktopTestingRig>();

                desktopRig.SetDesktopModeActive(true);

                Assert.IsFalse(evidenceInteraction.EnableDesktopNavigation, "EvidenceInteraction.EnableDesktopNavigation must be false to avoid conflicting transform translation.");
            }
            finally
            {
                if (_desktopRigGo != null)
                {
                    Object.DestroyImmediate(_desktopRigGo);
                    _desktopRigGo = null;
                }
            }
        }

        private static GameObject CreateTestRigHierarchy(
            out Transform trackingSpace,
            out Transform centerEye,
            out Transform leftHand,
            out Transform rightHand,
            out Camera cam)
        {
            var rigRoot = new GameObject("Test_OVRCameraRig");
            var tsGo = new GameObject("TrackingSpace");
            tsGo.transform.SetParent(rigRoot.transform, false);
            trackingSpace = tsGo.transform;

            var ceGo = new GameObject("CenterEyeAnchor");
            ceGo.transform.SetParent(trackingSpace, false);
            ceGo.tag = "MainCamera";
            cam = ceGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            centerEye = ceGo.transform;

            var lhGo = new GameObject("LeftHandAnchor");
            lhGo.transform.SetParent(trackingSpace, false);
            leftHand = lhGo.transform;

            var rhGo = new GameObject("RightHandAnchor");
            rhGo.transform.SetParent(trackingSpace, false);
            rightHand = rhGo.transform;

            return rigRoot;
        }
    }
}
