# Member Contribution: Pranjal Thakur

- **Student Researcher:** Pranjal Thakur
- **Roll Number:** N106
- **SAP ID:** 70472400138
- **Programme:** MBA Tech Computer Engineering · Batch B1
- **Declared Studio Role:** Forensic Chain-of-Custody & Timeline Analytics Specialist
- **Assigned Git Branch:** `feat/n106-forensic-chain-of-custody`
- **Agreed Project Effort:** 33.3%

---

## 1. Scope of Contribution & Engineering Responsibilities
Pranjal served as the **Forensic Chain-of-Custody & Timeline Analytics Specialist** for Group 07, engineering the timeline sequencing algorithms, Kendall-Tau rank concordance evaluator, kinematic trajectory telemetry logger, and statistical data exporter:

1. **Kendall-Tau Rank Correlation Engine (`KendallTau.cs`, `ResearchMetrics.cs`, `TimelineManager.cs`):**
   - Implemented algorithmic calculation of Kendall's Tau concordance coefficient ($\tau = \frac{C - D}{\frac{1}{2} n (n - 1)}$) comparing student reconstructed event timelines against ground-truth forensic sequence.
   - Handled concordant and discordant pair evaluation in real-time, providing instant quantitative scoring for investigative chronology accuracy.

2. **Kinematic & Trajectory Telemetry Logger (`TelemetryCollector.cs`, `InvestigationEventLogger.cs`, `TelemetryRecord.cs`):**
   - Developed continuous 10 Hz spatial sampling logging investigator 3D coordinates $(x, y, z)$, head rotation $(\text{yaw}, \text{pitch}, \text{roll})$, and gaze orientation throughout the session.
   - Logged timestamped interaction events (first gaze fixations, inspection durations, marker placement timestamps, and timeline adjustments).

3. **Session Management & Statistical Data Export (`SessionManager.cs`, `SessionData.cs`, `ResearchDataExporter.cs`):**
   - Designed session state machine orchestrating onboarding, investigation phase, evidence examination, timeline reconstruction, and debriefing.
   - Built automated CSV/JSON serialization pipeline streaming anonymized trial records to disk for subsequent Python statistical profiling and hypothesis testing ($N = 50$).

4. **Analytics Test Suite & Validation (`Person3ResearchTests.cs`, `ForensicTestExecutor.cs`):**
   - Authored NUnit test fixtures validating Kendall-Tau mathematical invariants, edge cases (identical sequences, inverted sequences, partial completions), and telemetry serialization integrity.

---

## 2. Attributed Files
- `Assets/Scripts/Analytics/KendallTau.cs`
- `Assets/Scripts/Analytics/ResearchMetrics.cs`
- `Assets/Scripts/Analytics/TimelineManager.cs`
- `Assets/Scripts/Telemetry/TelemetryCollector.cs`
- `Assets/Scripts/Telemetry/InvestigationEventLogger.cs`
- `Assets/Scripts/Telemetry/TelemetryRecord.cs`
- `Assets/Scripts/Export/ResearchDataExporter.cs`
- `Assets/Scripts/Session/SessionManager.cs`
- `Assets/Scripts/Session/SessionData.cs`
- `Assets/Scripts/Editor/Person3ResearchTests.cs`
- `Assets/Scripts/Editor/ForensicTestExecutor.cs`
