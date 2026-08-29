# Compatibility

DXLog QSO Exporter supports current SQLite-based `.dxn` logs and older SQLite `.dxn` logs using the `qsodata` contract, including the DBVersion 7 layout used by older DXLog exports. It requires the saved recording file index and byte position for each clip. Legacy `.dxl` logs, conversion, and networked multi-computer logs are outside the supported boundary.

DXLog should be closed before export. The exporter opens the log read-only, performs an integrity check, verifies the required current contract, and never writes to the database.

Clips are fixed windows around the time DXLog saved the QSO. They are not exact conversation boundaries because the log does not provide a trustworthy speech start and end for this purpose. The requested window is aligned to complete MP3 frames and is shortened when it reaches the start or end of a recording. Audio is never joined across recording files. Current logs normally use numbered MP3 files; a legacy log may use one unnumbered MP3, which is resolved when it is the only MP3 in the selected folder. Legacy recordings may also contain one recognized DXLog metadata block between frames and an incomplete terminal frame; those are handled without accepting arbitrary internal gaps or malformed audio. Optional radio extraction maps R1 to the left channel and R2 to the right channel, then writes re-encoded mono clips in matching subfolders.

QTC and WAE exports are intentionally unsupported: a QTC group has no saved recording position, so selecting audio from its group timestamp would be an unverifiable approximation. A valid saved position is also required for normal QSOs and XQSOs. Missing, invalid, out-of-range, changing, or corrupt inputs are reported in the run report rather than reconstructed from timestamps or file metadata.

The portable release targets Windows 10 and 11 x64 with .NET Framework 4.8. It does not install or update DXLog, play audio, transcode recordings, or modify source files.