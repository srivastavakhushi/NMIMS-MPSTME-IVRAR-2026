# Member Contribution: Shirin Sharma

- **Student Researcher:** Shirin Sharma
- **Roll Number:** N094
- **SAP ID:** 70472400106
- **Programme:** MBA Tech Computer Engineering · Batch B1
- **Declared Studio Role:** Spatial Forensics & Photogrammetry Lead
- **Assigned Git Branch:** `feat/n094-spatial-forensics-photogrammetry`
- **Agreed Project Effort:** 33.3%

---

## 1. Scope of Contribution & Engineering Responsibilities
Shirin served as the **Spatial Forensics & Photogrammetry Lead** for Group 07, responsible for crime scene environmental reconstruction, 3D asset optimization, physical collider topology, and spatial sanitization:

1. **Crime Scene Architectural Environment (`FreeOpenBuilding_1.1.2`, `Scenes/SampleScene.unity`):**
   - Constructed the multi-room forensic mock investigation site including master bedroom, living area, entryway, and bathroom.
   - Positioned and calibrated canonical physical props (spent cartridge casings, shattered glass, blood spatter decals, forced-entry door frames, and digital smartphones).
   - Configured Universal Render Pipeline (URP) baked and real-time lighting profiles to recreate realistic ambient crime scene illumination conditions.

2. **Mesh Optimization & Physics Collider Generation:**
   - Performed polygon budget decimation and LOD generation across interior building components to maintain steady 72–90 FPS on Meta Quest hardware.
   - Assigned simplified convex mesh colliders and box trigger volumes ensuring stable raycast hit registration without floating or z-fighting.

3. **Crime Scene Sanitizer & Coordinate Calibration (`CrimeSceneSanitizer.cs`):**
   - Engineered automated scene cleanup scripts ensuring evidence anchors strictly preserve ground-truth spatial coordinates across scene reloads.
   - Sanitized non-interactive clutter and calibrated collision matrices for forensic evidence tagging volumes.

4. **Integration & Scene Verification Tests (`ForensicInvestigationSetup.cs`, `ForensicInvestigationEditModeTests.cs`):**
   - Formulated verification routines to confirm zero missing script references, proper URP material bindings, and valid camera floor offsets.

---

## 2. Attributed Files
- `Assets/FreeOpenBuilding_1.1.2/`
- `Assets/Materials/`
- `Assets/Prefabs/`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Scripts/Core/CrimeSceneSanitizer.cs`
- `Assets/Scripts/Editor/ForensicInvestigationSetup.cs`
- `Assets/Scripts/Editor/ForensicInvestigationEditModeTests.cs`
