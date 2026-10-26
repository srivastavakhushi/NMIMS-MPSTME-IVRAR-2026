using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CrimeSceneVR.Core;
using CrimeSceneVR.VR;

namespace CrimeSceneVR.Editor
{
    [InitializeOnLoad]
    public static class VRRigSetup
    {
        private const string CrimeScenePath = "Assets/Scenes/CrimeScene.unity";
        private const string OvrRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";

        static VRRigSetup()
        {
            EditorApplication.delayCall += ExecuteSetupIfNeeded;
        }

        [MenuItem("Forensic/Setup Meta XR VR Rig in CrimeScene")]
        public static void SetupVRRigMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VRRigSetup] Setup cannot be run while in Play Mode.");
                return;
            }
            SetupVRRig(force: true);
        }

        [MenuItem("Forensic/Open Meta XR SDK Panel")]
        public static void OpenMetaXRSDKPanel()
        {
            EditorApplication.ExecuteMenuItem("Window/Meta/Meta XR SDK");
        }

        public static void ExecuteSetupIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }
            SetupVRRig(force: false);
        }

        public static bool SetupVRRig(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return false;
            }

            if (!File.Exists(CrimeScenePath))
            {
                Debug.LogWarning($"[VRRigSetup] Scene file '{CrimeScenePath}' not found. Cannot configure VR rig.");
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            bool isCrimeSceneOpen = (activeScene.path == CrimeScenePath);

            if (!isCrimeSceneOpen)
            {
                if (!force)
                {
                    // Only auto-run if CrimeScene is open or if invoked via menu/batch
                    return false;
                }
                activeScene = EditorSceneManager.OpenScene(CrimeScenePath, OpenSceneMode.Single);
            }

            // Clean up any duplicate rigs or temporary desktop test rigs if present
            var desktopRig = GameObject.Find("DesktopDebugRig");
            if (desktopRig != null)
            {
                Undo.DestroyObjectImmediate(desktopRig);
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    if (force)
                    {
                        EditorSceneManager.SaveOpenScenes();
                    }
                }
                Debug.Log("[VRRigSetup] Cleaned up temporary DesktopDebugRig from CrimeScene.unity.");
            }

            var allRigRefs = UnityEngine.Object.FindObjectsByType<VRRigReferences>(FindObjectsSortMode.None);
            if (allRigRefs.Length > 1)
            {
                for (int i = 1; i < allRigRefs.Length; i++)
                {
                    Undo.DestroyObjectImmediate(allRigRefs[i].gameObject);
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(activeScene);
                    if (force)
                    {
                        EditorSceneManager.SaveOpenScenes();
                    }
                }
                allRigRefs = UnityEngine.Object.FindObjectsByType<VRRigReferences>(FindObjectsSortMode.None);
            }

            // Check if VR Rig is already set up and configured
            var existingRigRef = (allRigRefs.Length > 0) ? allRigRefs[0] : null;
            if (existingRigRef != null && !force)
            {
                bool modified = false;

                // Ensure near clip plane is 0.05m
                if (existingRigRef.MainCamera != null && existingRigRef.MainCamera.nearClipPlane > 0.05f)
                {
                    existingRigRef.MainCamera.nearClipPlane = 0.05f;
                    EditorUtility.SetDirty(existingRigRef.MainCamera);
                    modified = true;
                }

                // Ensure CharacterController & VRLocomotionController on rig root
                var evMgr = UnityEngine.Object.FindAnyObjectByType<CrimeSceneEvidenceManager>();
                if (EnsureLocomotionComponents(existingRigRef.gameObject, existingRigRef, evMgr))
                {
                    modified = true;
                }

                // Ensure VREvidenceInteraction is configured on RightHandAnchor
                if (existingRigRef.RightHandAnchor != null)
                {
                    var vrInt = existingRigRef.RightHandAnchor.GetComponent<CrimeSceneVR.Interaction.VREvidenceInteraction>();
                    if (vrInt == null)
                    {
                        vrInt = existingRigRef.RightHandAnchor.gameObject.AddComponent<CrimeSceneVR.Interaction.VREvidenceInteraction>();
                        vrInt.Configure(evMgr, existingRigRef.RightHandAnchor);
                        EditorUtility.SetDirty(existingRigRef.RightHandAnchor.gameObject);
                        modified = true;
                    }
                }

                if (modified)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                }

                // Ensure Research Systems (SessionManager, Telemetry, Timeline, Exporter) exist
                EnsureResearchManagers(activeScene, save: false);

                Debug.Log("[VRRigSetup] Meta XR VR Rig is already configured in CrimeScene.unity.");
                return true;
            }

            Debug.Log("[VRRigSetup] Configuring Meta XR VR Rig in CrimeScene.unity...");

            // 1. Determine spawn position at the start of crime scene floor
            Vector3 spawnPosition = new Vector3(0f, 0.07f, 0f);
            var evidenceManager = UnityEngine.Object.FindAnyObjectByType<CrimeSceneEvidenceManager>();
            if (evidenceManager != null && evidenceManager.HasInvestigationFloor)
            {
                Bounds floorBounds = evidenceManager.InvestigationFloorBounds;
                spawnPosition = new Vector3(floorBounds.center.x, floorBounds.min.y, floorBounds.center.z);
            }

            // 2. Identify old standalone Main Camera
            Camera oldCamera = Camera.main;
            GameObject oldCameraGo = (oldCamera != null && existingRigRef == null) ? oldCamera.gameObject : null;

            // 3. Instantiate or locate OVRCameraRig
            GameObject rigGo = existingRigRef != null ? existingRigRef.gameObject : null;
            if (rigGo == null)
            {
                var ovrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OvrRigPrefabPath);
                if (ovrPrefab != null)
                {
                    rigGo = (GameObject)PrefabUtility.InstantiatePrefab(ovrPrefab, activeScene);
                    rigGo.name = "OVRCameraRig";
                }
                else
                {
                    // Fallback: Build procedural standard OpenXR/Meta VR rig hierarchy
                    rigGo = new GameObject("OVRCameraRig");
                    rigGo.AddComponent<OVRCameraRig>();
                    var ovrMgr = rigGo.AddComponent<OVRManager>();
                    ovrMgr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                    rigGo.AddComponent<OVRHeadsetEmulator>();

                    var trackingSpaceGo = new GameObject("TrackingSpace");
                    trackingSpaceGo.transform.SetParent(rigGo.transform, false);

                    var centerEyeGo = new GameObject("CenterEyeAnchor");
                    centerEyeGo.transform.SetParent(trackingSpaceGo.transform, false);
                    centerEyeGo.tag = "MainCamera";
                    var cam = centerEyeGo.AddComponent<Camera>();
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 1000f;
                    centerEyeGo.AddComponent<AudioListener>();

                    var leftHandGo = new GameObject("LeftHandAnchor");
                    leftHandGo.transform.SetParent(trackingSpaceGo.transform, false);
                    var leftCtrlGo = new GameObject("LeftControllerAnchor");
                    leftCtrlGo.transform.SetParent(leftHandGo.transform, false);

                    var rightHandGo = new GameObject("RightHandAnchor");
                    rightHandGo.transform.SetParent(trackingSpaceGo.transform, false);
                    var rightCtrlGo = new GameObject("RightControllerAnchor");
                    rightCtrlGo.transform.SetParent(rightHandGo.transform, false);
                }
            }

            rigGo.transform.position = spawnPosition;
            rigGo.transform.rotation = Quaternion.identity;

            // 4. Configure OVRManager settings
            var manager = rigGo.GetComponent<OVRManager>();
            if (manager == null)
            {
                manager = rigGo.AddComponent<OVRManager>();
            }
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.usePositionTracking = true;
            manager.useRotationTracking = true;

            // 5. Ensure VRRigReferences, CharacterController & VRLocomotionController components are attached
            var rigRef = rigGo.GetComponent<VRRigReferences>();
            if (rigRef == null)
            {
                rigRef = rigGo.AddComponent<VRRigReferences>();
            }
            EnsureLocomotionComponents(rigGo, rigRef, evidenceManager);
            rigRef.ResolveReferences();

            // 6. Configure CenterEyeAnchor Camera
            Camera centerEyeCam = null;
            Transform centerEye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye != null)
            {
                centerEye.gameObject.tag = "MainCamera";
                centerEyeCam = centerEye.GetComponent<Camera>();
                if (centerEyeCam == null)
                {
                    centerEyeCam = centerEye.gameObject.AddComponent<Camera>();
                }

                centerEyeCam.nearClipPlane = 0.05f;
                centerEyeCam.farClipPlane = 1000f;
                centerEyeCam.clearFlags = CameraClearFlags.Skybox;

                if (centerEye.GetComponent<AudioListener>() == null)
                {
                    centerEye.gameObject.AddComponent<AudioListener>();
                }

                var uacData = centerEye.GetComponent<UniversalAdditionalCameraData>();
                if (uacData == null)
                {
                    uacData = centerEye.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                uacData.allowXRRendering = true;

                // Move / attach EvidenceInteraction so desktop navigation and selection continues to work in Editor
                var interaction = centerEye.GetComponent<EvidenceInteraction>();
                if (interaction == null)
                {
                    interaction = centerEye.gameObject.AddComponent<EvidenceInteraction>();
                }
                interaction.Configure(evidenceManager, centerEyeCam);
            }

            // Wire VREvidenceInteraction on RightHandAnchor
            Transform rightHandAnchor = rigGo.transform.Find("TrackingSpace/RightHandAnchor");
            if (rightHandAnchor != null)
            {
                var vrInt = rightHandAnchor.GetComponent<CrimeSceneVR.Interaction.VREvidenceInteraction>();
                if (vrInt == null)
                {
                    vrInt = rightHandAnchor.gameObject.AddComponent<CrimeSceneVR.Interaction.VREvidenceInteraction>();
                }
                vrInt.Configure(evidenceManager, rightHandAnchor);
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    EditorUtility.SetDirty(rightHandAnchor.gameObject);
                }
            }

            // 7. Remove old redundant standalone Main Camera if separate from rig
            if (oldCameraGo != null && oldCameraGo != rigGo && (centerEye == null || oldCameraGo != centerEye.gameObject))
            {
                Undo.DestroyObjectImmediate(oldCameraGo);
            }

            // 8. Re-wire EvidenceUI to new interaction
            var ui = UnityEngine.Object.FindAnyObjectByType<EvidenceUI>();
            var newInteraction = rigGo.GetComponentInChildren<EvidenceInteraction>();
            if (ui != null && evidenceManager != null && newInteraction != null)
            {
                ui.Configure(evidenceManager, newInteraction);
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                {
                    EditorUtility.SetDirty(ui);
                }
            }

            // Ensure Research Systems (SessionManager, Telemetry, Timeline, Exporter) exist
            EnsureResearchManagers(activeScene, save: force);

            // 9. Save scene only if explicitly forced
            if (force && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
            {
                EditorUtility.SetDirty(rigGo);
                if (evidenceManager != null) EditorUtility.SetDirty(evidenceManager);
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveOpenScenes();
            }

            Debug.Log($"[VRRigSetup] Successfully configured Meta XR VR Rig at {spawnPosition} in {CrimeScenePath} with nearClipPlane=0.05m and VRLocomotionController.");
            return true;
        }

        private static bool EnsureLocomotionComponents(GameObject rigGo, VRRigReferences rigRef, CrimeSceneEvidenceManager evidenceManager, bool save = false)
        {
            bool changed = false;

            var cc = rigGo.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = rigGo.AddComponent<CharacterController>();
                cc.height = 1.7f;
                cc.radius = 0.25f;
                cc.center = new Vector3(0f, 0.85f, 0f);
                cc.skinWidth = 0.02f;
                cc.stepOffset = 0.25f;
                changed = true;
            }

            var loco = rigGo.GetComponent<VRLocomotionController>();
            if (loco == null)
            {
                loco = rigGo.AddComponent<VRLocomotionController>();
                changed = true;
            }
            if (loco.UseSnapTurn)
            {
                loco.UseSnapTurn = false;
                changed = true;
            }
            if (Mathf.Abs(loco.SmoothTurnSpeed - 50f) > 0.1f)
            {
                loco.SmoothTurnSpeed = 50f;
                changed = true;
            }
            if (!loco.EnableTeleport)
            {
                loco.EnableTeleport = true;
                changed = true;
            }
            loco.Configure(rigRef, evidenceManager);
            rigRef.ResolveReferences();

            EnsureBathroomDoorOpen(save);

            if (changed && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
            {
                EditorUtility.SetDirty(rigGo);
            }

            return changed;
        }

        private static void EnsureBathroomDoorOpen(bool save = false)
        {
            var doorRoot = GameObject.Find("FOB_LOD/Doors/Door_Room_LOD (2)");
            if (doorRoot == null)
            {
                var allGos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var g in allGos)
                {
                    if (g.name == "Door_Room_LOD (2)")
                    {
                        doorRoot = g;
                        break;
                    }
                }
            }

            if (doorRoot != null)
            {
                Vector3 targetRot = new Vector3(0f, 90f, 0f);
                if (Quaternion.Angle(doorRoot.transform.localRotation, Quaternion.Euler(targetRot)) > 0.5f)
                {
                    doorRoot.transform.localEulerAngles = targetRot;

                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
                    {
                        EditorUtility.SetDirty(doorRoot);
                        if (doorRoot.scene.IsValid() && doorRoot.scene.isLoaded)
                        {
                            EditorSceneManager.MarkSceneDirty(doorRoot.scene);
                            if (save)
                            {
                                EditorSceneManager.SaveScene(doorRoot.scene);
                            }
                        }
                        Debug.Log("[VRRigSetup] Bathroom door 'Door_Room_LOD (2)' set to open position (0, 90, 0).");
                    }
                    else
                    {
                        Debug.Log("[VRRigSetup] Bathroom door 'Door_Room_LOD (2)' adjusted to open position (0, 90, 0) at runtime.");
                    }
                }
            }
        }

        private static void EnsureResearchManagers(Scene activeScene, bool save = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            var sm = UnityEngine.Object.FindAnyObjectByType<CrimeSceneVR.Session.SessionManager>();
            if (sm == null)
            {
                var researchGo = new GameObject("[ResearchSystem]");
                researchGo.AddComponent<CrimeSceneVR.Session.SessionManager>();
                researchGo.AddComponent<CrimeSceneVR.Telemetry.TelemetryCollector>();
                researchGo.AddComponent<CrimeSceneVR.Analytics.TimelineManager>();
                researchGo.AddComponent<CrimeSceneVR.Export.ResearchDataExporter>();
                EditorUtility.SetDirty(researchGo);
                EditorSceneManager.MarkSceneDirty(activeScene);
                if (save)
                {
                    EditorSceneManager.SaveOpenScenes();
                }
                Debug.Log("[VRRigSetup] Added [ResearchSystem] (SessionManager, TelemetryCollector, TimelineManager, ResearchDataExporter) to CrimeScene.unity.");
            }
        }
    }
}
