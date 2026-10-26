# Group 07 Capstone Submission Manifest

- **Course:** Introduction to Virtual Reality & Augmented Reality (IVRAR - Course Code: 702COI002)
- **Institution:** SVKM's NMIMS Mukesh Patel School of Technology Management & Engineering (MPSTME)
- **Academic Term:** Academic Year 2026–2027 (Semester V)
- **Group:** Group 07 (Batch B1)
- **Research Title:** *To what extent does an interactive VR spatial crime scene reconstruction improve evidence tagging accuracy and timeline sequencing for student forensic investigators compared to traditional 2D photographic logs?*
- **Submission Date:** Monday, 26-Oct-2026, 23:59 IST
- **Target Release Tag:** `group-07-v1.0`

---

## 1. Student Research Team Roster

| Member Name | Roll No. | SAP ID | Programme | Studio Role | Assigned Branch | Authenticated Account |
| :--- | :---: | :---: | :--- | :--- | :--- | :--- |
| **Shirin Sharma** | N094 | 70472400106 | MBA Tech Computer Engg | Spatial Forensics & Photogrammetry Lead | `feat/n094-spatial-forensics-photogrammetry` | Individual contributor |
| **Khushi Srivastava** | N101 | 70472400012 | MBA Tech Computer Engg | XR Systems Architect & Evidence Interaction Lead | `feat/n101-xr-systems-architect` | `srivastavakhushi` (authenticated) |
| **Pranjal Thakur** | N106 | 70472400138 | MBA Tech Computer Engg | Forensic Chain-of-Custody & Timeline Analytics Specialist | `feat/n106-forensic-chain-of-custody` | Individual contributor |

---

## 2. Directory Structure & Included Files

```
Group_07_Shirin_Khushi_Pranjal/
├── README.md                                          # Master group documentation & research summary
├── submission_manifest.md                             # This submission manifest
├── QUEST2_LINK_READINESS.md                           # Meta Quest 2 / PC Link execution checklist
├── group7_logs.txt                                    # Build & execution runtime verification logs
├── Khushi_CONTRIBUTION_AND_WORK_BREAKDOWN.md          # N101 individual work breakdown
├── Shirin_CONTRIBUTION_AND_WORK_BREAKDOWN.md          # N094 individual work breakdown
├── Pranjal_CONTRIBUTION_AND_WORK_BREAKDOWN.md         # N106 individual work breakdown
├── Assets/                                            # Authoritative Unity assets, scenes, materials, scripts
│   ├── FreeOpenBuilding_1.1.2/                        # 3D interior crime scene environment & props
│   ├── Materials/                                     # Surface shaders, blood decals, markers
│   ├── Prefabs/                                       # Evidence tags, UI panels, rigs
│   ├── Scenes/                                        # SampleScene.unity (Authoritative crime scene)
│   ├── Scripts/                                       # Modular C# forensic codebase
│   │   ├── Analytics/                                 # KendallTau, ResearchMetrics, TimelineManager
│   │   ├── Core/                                      # CrimeSceneEvidenceManager, CrimeSceneSanitizer
│   │   ├── Editor/                                    # Automated NUnit test fixtures & setups
│   │   ├── Evidence/                                  # EvidenceObject, EvidenceTag
│   │   ├── Export/                                    # ResearchDataExporter
│   │   ├── Interaction/                               # EvidenceInteraction, VREvidenceInteraction
│   │   ├── Session/                                   # SessionManager, SessionData
│   │   ├── Telemetry/                                 # TelemetryCollector, InvestigationEventLogger
│   │   ├── UI/                                        # EvidenceUI
│   │   └── VR/                                        # VRLocomotionController, VRRigSetup
│   ├── Settings/                                      # URP profiles and renderer settings
│   └── XR/                                            # OpenXR loader and package configurations
├── Packages/                                          # Unity Package Manager manifest & lockfile
├── ProjectSettings/                                   # Complete Unity project settings & tags
├── docs/                                              # Technical documentation, figures & blueprints
│   ├── TEAM_ROSTER.json                               # Machine-readable group metadata
│   ├── toolchain_spec.md                              # Toolchain specifications
│   ├── LITERATURE_REVIEW_AND_FORENSIC_FRAMEWORK.md    # 6 CrossRef verified DOI foundational papers
│   ├── RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md         # 4-page IEEE research manuscript blueprint
│   └── figures/                                       # Publication-grade 300 DPI evaluation figures
└── telemetry/                                         # Empirical data processing & Python evaluators
    ├── forensic_investigation_telemetry.csv           # Telemetry records
    ├── generate_paper_figures.py                      # Figure generation pipeline
    ├── kennedy_ssq_calculator.py                      # Simulator Sickness Questionnaire evaluator
    ├── nasa_tlx_calculator.py                         # NASA-TLX workload calculator
    └── sus_calculator.py                              # System Usability Scale calculator
```

---

## 3. Verification & Compliance Checklist

- [x] **Aalborg Problem Formulation:** Addressed via 6-DoF interactive VR crime scene reconstruction vs 2D baseline.
- [x] **Unity Version:** Verified Unity 6000.0.33f1 LTS with Universal Render Pipeline (URP).
- [x] **Hardware Target:** Meta Quest 2 / Quest 3 via OpenXR and PC Link runtime.
- [x] **C# Scripting Standards:** Clean namespace modularity across Analytics, Core, Evidence, Interaction, Telemetry, and VR.
- [x] **Automated Tests:** Comprehensive EditMode & PlayMode test coverage across interaction, Kendall-Tau, and rig calibration.
- [x] **Git LFS Compliance:** Large 3D models and textures tracked through Git LFS rules; no binaries or APK committed to git history.
- [x] **Runnable Executable:** Standalone Meta Quest build `group7.apk` generated (75.8 MB) and ready for GitHub Release `group-07-v1.0`.
