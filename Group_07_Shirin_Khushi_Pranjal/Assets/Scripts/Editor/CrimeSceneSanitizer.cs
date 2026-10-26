using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimeSceneVR.Core;

namespace CrimeSceneVR.Editor
{
    [InitializeOnLoad]
    public static class CrimeSceneSanitizer
    {
        private const string ScenePath = "Assets/Scenes/CrimeScene.unity";
        private static readonly Vector3 CanonicalEv005Position = new Vector3(-8.685414f, 1.0364051f, 7.1191926f);

        static CrimeSceneSanitizer()
        {
            EditorApplication.delayCall += () =>
            {
                SanitizeCrimeScene(autoSave: true);
            };
        }

        [MenuItem("Forensic/Sanitize Crime Scene & Fix EV-005")]
        public static void SanitizeMenu()
        {
            SanitizeCrimeScene(autoSave: true);
        }

        public static bool SanitizeCrimeScene(bool autoSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return false;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (!File.Exists(ScenePath))
                    return false;
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            bool modified = false;

            // 1. Fix EV-005 canonical floor position
            var allEvidence = UnityEngine.Object.FindObjectsByType<EvidenceObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ev in allEvidence)
            {
                if (ev.EvidenceId == "EV-005" || ev.name.StartsWith("EV-005"))
                {
                    if (Vector3.Distance(ev.transform.localPosition, CanonicalEv005Position) > 0.001f)
                    {
                        ev.transform.localPosition = CanonicalEv005Position;
                        ev.transform.localRotation = Quaternion.identity;
                        EditorUtility.SetDirty(ev.gameObject);
                        modified = true;
                        Debug.Log($"[CrimeSceneSanitizer] Restored EV-005 position to canonical coordinates: {CanonicalEv005Position}");
                    }
                }
            }

            // 2. Clean out duplicate InvestigationMarkers and orphan Marker GameObjects
            var allGos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var markerRoots = new List<GameObject>();
            int destroyedMarkers = 0;

            foreach (var go in allGos)
            {
                if (go == null) continue;

                if (go.name.StartsWith("Marker ") || (go.name.Contains("Marker") && go.name.Contains("EV-")))
                {
                    Undo.DestroyObjectImmediate(go);
                    destroyedMarkers++;
                    modified = true;
                }
                else if (go.name == "InvestigationMarkers")
                {
                    markerRoots.Add(go);
                }
            }

            if (markerRoots.Count > 1)
            {
                for (int i = 1; i < markerRoots.Count; i++)
                {
                    Undo.DestroyObjectImmediate(markerRoots[i]);
                    modified = true;
                }
            }

            if (destroyedMarkers > 0)
            {
                Debug.Log($"[CrimeSceneSanitizer] Cleaned up {destroyedMarkers} orphan markers and {markerRoots.Count - 1} duplicate InvestigationMarkers roots.");
            }

            // 3. Ensure CrimeSceneEvidenceManager references the single clean root
            var evMgr = UnityEngine.Object.FindAnyObjectByType<CrimeSceneEvidenceManager>();
            if (evMgr != null)
            {
                Transform targetRoot = markerRoots.Count > 0 && markerRoots[0] != null ? markerRoots[0].transform : null;
                var field = typeof(CrimeSceneEvidenceManager).GetField("markersRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var currentRoot = field.GetValue(evMgr) as Transform;
                    if (currentRoot != targetRoot)
                    {
                        field.SetValue(evMgr, targetRoot);
                        EditorUtility.SetDirty(evMgr);
                        modified = true;
                    }
                }
            }

            if (modified && autoSave)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[CrimeSceneSanitizer] CrimeScene.unity successfully sanitized and saved.");
            }

            return modified;
        }
    }
}
