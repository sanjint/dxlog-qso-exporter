# DXLog QSO Exporter User Guide

**English** | [Bosanski](user-guide.bs.md) | [Deutsch](user-guide.de.md)

**DXLog QSO Exporter** is a lightweight, portable Windows utility for contesters. It automatically cuts and exports an individual MP3 audio clip for every logged QSO from your DXLog contest recordings.

## 1. Requirements & Prerequisites

Before running the application, make sure your system meets the following requirements:

- **Operating System:** Windows 10 or Windows 11 (64-bit / x64).
- **Runtime:** Microsoft .NET Framework 4.8 (pre-installed on modern Windows 10 and 11 systems).
- **Log State:** **DXLog must be closed** before running the export. DXLog locks its SQLite database file while running, which prevents the exporter from safely reading the log.

---

## 2. Installation & Quick Start

DXLog QSO Exporter is distributed as a portable standalone application — no installer or administrative privileges required.

1. **Download:** Grab the latest `DxLogQsoExporter-vX.Y.Z-win-x64.zip` release archive from GitHub.
2. **Extract:** Extract the entire ZIP archive to a folder of your choice (for example, `C:\Tools\DxLogQsoExporter` or your desktop).
3. **Launch:** Double-click `DxLogQsoExporter.exe` to start the application.

---

## 3. Step-by-Step Walkthrough

The user interface is organized into simple, clear sections:

```
+-------------------------------------------------------------------------+
| Files                                                                   |
|   DXN log file:    [ C:\Contests\CQWW_2026.dxn                  ] [Browse] |
|   MP3 recordings:  [ C:\Contests\CQWW_2026_Audio                ] [Browse] |
|   Export folder:   [ C:\Contests\CQWW_2026_QSO_Audio            ] [Browse] |
+-------------------------------------------------------------------------+
| Clip settings                                                           |
|   Time before QSO (s): [ 30 ]   Total clip duration (s): [ 60 ]  [Reset]  |
|   [ ] Extract one channel from stereo recording per radio (R1=left, R2=right)|
+-------------------------------------------------------------------------+
| [ Export ]   [ Cancel ]                             [ Open export folder ]|
+-------------------------------------------------------------------------+
| Status: Ready                                                           |
| [=====================================================================] |
| Summary results...                                                      |
+-------------------------------------------------------------------------+
```

### Step 1: Select Your DXLog Log (`.dxn`)
Click **Browse...** next to **DXN log file** and select your contest `.dxn` database file.
- When selected, the exporter automatically suggests a destination folder named `<YourLog>_QSO_Audio` in the same directory.

### Step 2: Select the MP3 Recordings Folder
Click **Browse...** next to **MP3 recordings** and select the folder where DXLog saved the contest audio.
- Modern DXLog versions store audio as consecutively numbered files (e.g., `000.mp3`, `001.mp3`, `002.mp3`).
- Older single-file contest logs are also supported if the folder contains a single `.mp3` file.

### Step 3: Select or Confirm the Export Folder
The **Export folder** indicates where the extracted clips and summary report will be saved. You can keep the suggested folder or click **Browse...** to select a custom destination. The directory is created automatically when the export begins.

### Step 4: Configure Clip Timing
- **Time before QSO (s):** Number of seconds of audio to capture *before* the QSO was logged (default: `30` seconds).
- **Total clip duration (s):** Total length of each exported MP3 file (default: `60` seconds). For example, with 30s before and 60s total, each clip captures 30 seconds before the log entry and 30 seconds after.
- **Reset defaults:** Restores timing to DXLog's standard 30-second lead-in and 60-second total duration.

### Step 5: (Optional) Radio Channel Extraction (SO2R / 2R)
If you recorded in stereo with Radio 1 on the left audio channel and Radio 2 on the right audio channel:
- Check **Extract one channel from stereo recording per radio (R1 = left, R2 = right)**.
- When enabled, the exporter routes R1 QSOs to an `R1/` subfolder and R2 QSOs to an `R2/` subfolder, re-encoding each clip into a clean mono MP3.

### Step 6: Start the Export
Click **Export**. The application will:
1. Verify the `.dxn` database structure and integrity.
2. Index the MP3 audio frames.
3. Extract each QSO clip.
4. Generate a detailed export report.

Once complete, click **Open export folder** to open Windows Explorer directly to your exported clips.

---

## 4. Export Modes: Lossless vs. Channel Extraction

| Mode | Audio Processing | Quality | Subfolders |
| :--- | :--- | :--- | :--- |
| **Default Export (Unchecked)** | **100% Lossless Bit-Exact Copy:** Exact MPEG audio frames are extracted directly from the source recordings without decoding or re-encoding. | Original audio quality preserved; zero generational loss. | Clips placed directly in the export folder. |
| **Channel Extraction (Checked)** | **Stereo-to-Mono Extraction:** The stereo stream is decoded, the corresponding radio channel (R1=Left, R2=Right) is isolated, and re-encoded as mono MP3. | Clean single-radio mono audio. | Clips separated into `R1/` and `R2/` subfolders. |

---

## 5. Output Files & Naming Convention

Each exported QSO clip is saved with a structured, human-readable filename:

```text
<QSO-Number>_<Date>_<Time>_<Callsign>_<Band>_<Mode>.mp3
```

### Examples:
- **Standard QSO:** `000123_2026-10-24_1423_W1AW_20M_CW.mp3`
- **XQSO (Invalid / Cross-out):** `000124_2026-10-24_1424_K3LR_20M_CW_XQSO.mp3`
- **Channel Extracted:** `R1/000123_2026-10-24_1423_W1AW_20M_CW.mp3`

---

## 6. Understanding Export Results

After an export run finishes, the summary panel displays category counts:

- **Total QSOs:** The total number of QSO records found in the database.
- **Exported:** Contacts whose audio was successfully extracted for the full requested duration.
- **Shortened:** Contacts whose audio was extracted successfully, but the clip was shortened because the contact occurred too close to the beginning or end of a recording file (audio is never stitched across separate files).
- **Skipped:** Contacts that could not be extracted (e.g., QSO logged before audio recording was activated, missing MP3 file, or file already exists in destination).
- **Failed:** Contacts that encountered an unexpected read/write error during extraction.
- **XQSOs:** Total count of XQSO (unscored or cancelled) contacts included in the export.

---

## 7. Export Report & Privacy

Every export produces a detailed text report named `_export-report_YYYYMMDDTHHMMSSZ.txt` in your export folder.

### What is in the report?
- Run timestamp and application version.
- Summary metrics and issue counts.
- Itemized list of any contacts that were skipped, shortened, or encountered errors.

### Privacy Safeguards:
- The report **redacts all absolute file paths and Windows usernames** to protect your privacy if you share the report on forums, email, or GitHub issues.
- Only the log filename and generic folder labels appear in the report header.

---

## 8. Troubleshooting & Common Questions

### 1. "DXLog appears to be using the selected log" / File Locked Error
- **Cause:** DXLog (or another SQLite browser) currently has the `.dxn` file open.
- **Solution:** Close DXLog completely and click **Export** again.

### 2. Missing Audio Clips / "Missing recording" Count in Report
- **Cause:** DXLog was not recording audio when those specific contacts were logged, or some numbered MP3 files (e.g., `003.mp3`) are missing from the folder.
- **Solution:** Verify that all MP3 files created during the contest are present in the recordings directory.

### 3. "Output file already present"
- **Cause:** A clip with the same filename already exists in the destination folder.
- **Solution:** The exporter never overwrites existing clips to prevent accidental data loss. Choose a new export folder or delete/move previous exports.

### 4. Are QTCs (WAE Contest) Supported?
- **Answer:** No. QTC records in DXLog do not contain saved byte positions in audio recordings. To maintain 100% precision, QTC approximation is intentionally unsupported.

### 5. Are Networked Multi-Op Logs Supported?
- **Answer:** DXLog QSO Exporter supports single-computer logs. In networked multi-computer environments, each station PC maintains its own local audio index numbers which can collide; networked log audio should be exported from individual station machine logs.