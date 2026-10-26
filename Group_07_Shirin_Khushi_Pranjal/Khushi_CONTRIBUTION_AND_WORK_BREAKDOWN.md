# Member Contribution: Khushi Srivastava

- **Student Researcher:** Khushi Srivastava
- **Roll Number:** N101
- **SAP ID:** 70472400012
- **Programme:** MBA Tech Computer Engineering · Batch B1
- **Declared Studio Role:** XR Systems Architect & Evidence Interaction Lead
- **Assigned Git Branch:** `feat/n101-xr-systems-architect`
- **Authenticated GitHub Identity:** `srivastavakhushi` (`khushisrivastavaggn@gmail.com`)
- **Agreed Project Effort:** 33.3%

---

## 1. Scope of Contribution & Engineering Responsibilities
Khushi served as the **XR Systems Architect & Evidence Interaction Lead** for Group 07, engineering the core VR interaction mechanics, 6-DoF raycasting, evidence tagging lifecycle, simulated SHA-256 chain-of-custody digital hashing, and VR locomotion rig integration:

1. **XR Evidence Interaction Engine (`EvidenceInteraction.cs`, `VREvidenceInteraction.cs`):**
   - Implemented 6-DoF raycasting and direct interactor selection for physical crime scene evidence markers.
   - Designed multi-hit raycasting logic to prevent occlusion bugs with transparent or complex meshes.
   - Connected XR controller trigger inputs to real-time 3D evidence inspection and tag placement.

2. **Core Crime Scene Evidence Manager (`CrimeSceneEvidenceManager.cs`):**
   - Architected the central evidence registry managing ground-truth coordinates, evidence IDs (EV-001 through EV-008), classification types (ballistics, biological, trace, digital, document), and discovery states.
   - Built simulated SHA-256 digital fingerprinting: generates an immutable cryptographic signature upon tag placement to preserve virtual chain-of-custody integrity.
   - Implemented spatial localization error evaluation calculating real-time Euclidean distance residuals ($\Delta d$) between participant tagged locations and photogrammetric ground truth.

3. **VR Rig Architecture & Locomotion (`VRRigSetup.cs`, `VRLocomotionController.cs`, `VRRigReferences.cs`):**
   - Configured OpenXR and Meta Quest 2 / PC Link rig integration with dual hand controllers.
   - Integrated ergonomic teleportation locomotion and discrete snap/smooth turning to minimize vestibulo-ocular mismatch and simulator sickness ($SSQ < 15$).
   - Built desktop testing rig fallback (`DesktopTestingRig.cs`) enabling headless / keyboard-mouse development workflows.

4. **In-VR Heads-Up Display & Interaction UI (`EvidenceUI.cs`, `EvidenceTag.cs`, `EvidenceObject.cs`):**
   - Designed world-space high-contrast billboarded evidence tags with dynamic numbering and category iconography.
   - Developed contextual evidence examination panels displaying forensic descriptions, custody hashes, and timestamping.

5. **Automated Unit & Interaction Tests (`Person2VRInteractionTests.cs`, `VRRigTests.cs`):**
   - Created NUnit EditMode / PlayMode test fixtures verifying raycast hit detection, marker instancing, tag state transitions, and custody hash determinism.

---

## 2. Attributed Files
- `Assets/Scripts/Interaction/EvidenceInteraction.cs`
- `Assets/Scripts/Interaction/VREvidenceInteraction.cs`
- `Assets/Scripts/Interaction/DesktopTestingRig.cs`
- `Assets/Scripts/Core/CrimeSceneEvidenceManager.cs`
- `Assets/Scripts/Evidence/EvidenceObject.cs`
- `Assets/Scripts/Evidence/EvidenceTag.cs`
- `Assets/Scripts/UI/EvidenceUI.cs`
- `Assets/Scripts/VR/VRLocomotionController.cs`
- `Assets/Scripts/VR/VRRigSetup.cs`
- `Assets/Scripts/VR/VRRigReferences.cs`
- `Assets/Scripts/Editor/Person2VRInteractionTests.cs`
- `Assets/Scripts/Editor/VRRigTests.cs`
