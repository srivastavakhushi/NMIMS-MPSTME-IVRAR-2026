using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CrimeSceneVR.Core;
using CrimeSceneVR.Interaction;

namespace CrimeSceneVR.Tests
{
    [TestFixture]
    public class Person2VRInteractionTests
    {
        private GameObject _rootGo;
        private CrimeSceneEvidenceManager _evidenceManager;
        private EvidenceObject _evidence1;
        private EvidenceObject _evidence2;
        private VREvidenceInteraction _vrInteraction;
        private GameObject _rigGo;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("Test_InvestigationRoot");
            _evidenceManager = _rootGo.AddComponent<CrimeSceneEvidenceManager>();

            // Create test evidence objects
            var ev1Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ev1Go.name = "EV-001 Ballistic casing";
            ev1Go.transform.SetParent(_rootGo.transform);
            ev1Go.transform.position = new Vector3(0f, 500f, 2f);
            _evidence1 = ev1Go.AddComponent<EvidenceObject>();
            _evidence1.Configure("EV-001", "Ballistic casing", "Test casing", EvidenceCategory.Ballistic);

            var ev2Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ev2Go.name = "EV-002 Weapon";
            ev2Go.transform.SetParent(_rootGo.transform);
            ev2Go.transform.position = new Vector3(2f, 500f, 2f);
            _evidence2 = ev2Go.AddComponent<EvidenceObject>();
            _evidence2.Configure("EV-002", "Weapon", "Test weapon", EvidenceCategory.Weapon);

            // Register evidence
            _evidenceManager.RegisterEvidence(_evidence1);
            _evidenceManager.RegisterEvidence(_evidence2);

            // Create VR pointer rig
            _rigGo = new GameObject("Test_VRPointer");
            _vrInteraction = _rigGo.AddComponent<VREvidenceInteraction>();
            _vrInteraction.Configure(_evidenceManager, _rigGo.transform);

            // Start session so actions are accepted
            _evidenceManager.StartSession();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rigGo != null)
                Object.DestroyImmediate(_rigGo);
            if (_rootGo != null)
                Object.DestroyImmediate(_rootGo);
        }

        [Test]
        public void VREvidenceInteraction_Raycast_DetectsEvidenceObject()
        {
            // Position pointer directly facing evidence 1
            _rigGo.transform.position = new Vector3(0f, 500f, 0f);
            _rigGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            bool hasHit = _vrInteraction.ProcessRaycast(out RaycastHit hit, out EvidenceObject evidence);

            Assert.IsTrue(hasHit, "Raycast must hit the cube collider.");
            Assert.IsNotNull(evidence, "EvidenceObject must be resolved from hit collider.");
            Assert.AreEqual("EV-001", evidence.EvidenceId);
        }

        [Test]
        public void VREvidenceInteraction_Inspect_SelectsAndDiscoversEvidence()
        {
            Assert.IsFalse(_evidence1.IsDiscovered, "Evidence must initially be undiscovered.");

            bool inspected = _vrInteraction.InspectEvidence(_evidence1);

            Assert.IsTrue(inspected, "InspectEvidence must return true for registered evidence.");
            Assert.IsTrue(_evidence1.IsInspected, "Evidence must be marked inspected.");
            Assert.IsTrue(_evidence1.IsDiscovered, "Inspecting undiscovered evidence must automatically discover it.");
            Assert.AreEqual(_evidence1, _evidenceManager.CurrentSelection, "Evidence must be set as current selection.");
        }

        [Test]
        public void VREvidenceInteraction_Tag_CalculatesSpatialErrorAndMarksTagged()
        {
            Vector3 groundTruth = _evidence1.GroundTruthWorldPosition;
            // Tag slightly offset by 2cm (0.02m) along x
            Vector3 tagPoint = groundTruth + new Vector3(0.02f, 0f, 0f);

            bool tagged = _vrInteraction.TagEvidence(_evidence1, tagPoint);

            Assert.IsTrue(tagged, "TagEvidence must succeed.");
            Assert.IsTrue(_evidence1.IsTagged, "Evidence must be marked tagged.");
            Assert.AreEqual(0.02f, _evidence1.SpatialErrorMetres, 0.001f, "Spatial error must equal Euclidean distance from ground truth.");
            Assert.IsTrue(_evidenceManager.IsWithinAccuracyTolerance(_evidence1.SpatialErrorMetres), "2cm offset must fall within 5cm tolerance.");
        }

        [Test]
        public void VREvidenceInteraction_Tag_PreventsDuplicateTagging()
        {
            Vector3 tagPoint = _evidence1.GroundTruthWorldPosition;
            bool firstTag = _vrInteraction.TagEvidence(_evidence1, tagPoint);
            Assert.IsTrue(firstTag, "First tag must succeed.");

            // Attempting second tag on same evidence must be blocked
            bool secondTag = _vrInteraction.TagEvidence(_evidence1, tagPoint + new Vector3(0.01f, 0f, 0f));

            Assert.IsFalse(secondTag, "Second tag attempt on already tagged evidence must be prevented.");
            Assert.AreEqual(1, _evidenceManager.TaggedCount, "TaggedCount must remain 1.");
        }

        [Test]
        public void VREvidenceInteraction_FailsGracefully_WhenNoVRHeadsetPresent()
        {
            // When run in editor test runner without active VR hardware, component must remain safe
            Assert.DoesNotThrow(() =>
            {
                _vrInteraction.ProcessRaycast(out _, out _);
                _vrInteraction.InspectEvidence(null);
                _vrInteraction.TagEvidence(null, Vector3.zero);
            }, "VREvidenceInteraction must not throw unhandled exceptions in desktop/headless test environments.");
        }

        [Test]
        public void VREvidenceInteraction_Raycast_DetectsGlassClusterChildShard_EV005()
        {
            // Create a glass cluster hierarchy matching EV-005
            var glassRoot = new GameObject("EV-005 Broken glass");
            glassRoot.transform.SetParent(_rootGo.transform);
            glassRoot.transform.position = new Vector3(-8.685414f, 500f, 7.1191926f);

            var rootBox = glassRoot.AddComponent<BoxCollider>();
            rootBox.center = new Vector3(0f, 0.02f, 0f);
            rootBox.size = new Vector3(0.16f, 0.05f, 0.16f);

            var ev005 = glassRoot.AddComponent<EvidenceObject>();
            ev005.Configure("EV-005", "Broken glass", "Placeholder cluster of broken glass fragments.", EvidenceCategory.Glass);
            _evidenceManager.RegisterEvidence(ev005);

            // Add child shards with individual BoxColliders (like EV-005 prefab)
            var shard1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard1.name = "Glass fragment";
            shard1.transform.SetParent(glassRoot.transform, false);
            shard1.transform.localPosition = new Vector3(0.035f, 0.012f, 0.01f);
            shard1.transform.localScale = new Vector3(0.045f, 0.012f, 0.03f);

            // Position ray pointer looking at the child shard
            _rigGo.transform.position = glassRoot.transform.position + new Vector3(0f, 1f, -1f);
            _rigGo.transform.rotation = Quaternion.LookRotation(shard1.transform.position - _rigGo.transform.position);

            bool hitSuccess = _vrInteraction.ProcessRaycast(out RaycastHit hit, out EvidenceObject hitEv);

            Assert.IsTrue(hitSuccess, "Raycast must hit the glass cluster/shard.");
            Assert.IsNotNull(hitEv, "Hit must resolve to EV-005 EvidenceObject from child collider.");
            Assert.AreEqual("EV-005", hitEv.EvidenceId);

            // Test inspecting and tagging EV-005
            bool inspectSuccess = _vrInteraction.InspectEvidence(hitEv);
            Assert.IsTrue(inspectSuccess, "EV-005 must be inspectable.");
            Assert.IsTrue(hitEv.IsDiscovered, "EV-005 must be discovered.");

            bool tagSuccess = _vrInteraction.TagEvidence(hitEv, hit.point);
            Assert.IsTrue(tagSuccess, "EV-005 must be taggable via raycast hit point.");
            Assert.IsTrue(hitEv.IsTagged, "EV-005 must be marked tagged.");
            Assert.GreaterOrEqual(hitEv.SpatialErrorMetres, 0f, "Spatial error must be computed.");
            Assert.Less(hitEv.SpatialErrorMetres, 0.20f, "Tag point on glass shard must be within cluster radius (20cm).");
        }

        [Test]
        public void VREvidenceInteraction_Raycast_PrioritizesEvidence_OverOverlappingFurniture()
        {
            // Position evidence
            var evRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            evRoot.name = "EV-005 InRoom";
            evRoot.transform.SetParent(_rootGo.transform);
            evRoot.transform.position = new Vector3(10f, 500f, 10f);
            var evObj = evRoot.AddComponent<EvidenceObject>();
            evObj.Configure("EV-005", "Broken glass", "Test glass", EvidenceCategory.Glass);
            _evidenceManager.RegisterEvidence(evObj);

            // Create a large furniture collider that encloses or intersects in front
            var furniture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            furniture.name = "BedFurniture";
            furniture.transform.SetParent(_rootGo.transform);
            furniture.transform.position = new Vector3(10f, 500f, 9.8f);
            furniture.transform.localScale = new Vector3(3f, 3f, 1f);

            // Aim ray from 9m towards 10m through the furniture collider
            _rigGo.transform.position = new Vector3(10f, 500f, 8.5f);
            _rigGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            bool hitSuccess = _vrInteraction.ProcessRaycast(out RaycastHit hit, out EvidenceObject hitEv);

            Assert.IsTrue(hitSuccess, "Raycast must register hits.");
            Assert.IsNotNull(hitEv, "EvidenceObject must be prioritized over overlapping furniture.");
            Assert.AreEqual("EV-005", hitEv.EvidenceId);
        }
    }
}
