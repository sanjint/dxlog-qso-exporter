# DXLog QSO Exporter - Schnellanleitung

[English](user-guide.md) | [Bosanski](user-guide.bs.md) | **Deutsch**

**DXLog QSO Exporter** ist ein kompaktes, portables Windows-Tool für Contester. Es schneidet aus deinen DXLog-Dateien und MP3-Aufnahmen automatisch für jedes geloggte QSO einen eigenen Audioclip heraus.

---

## 1. Was du brauchst

- **System:** Windows 10 oder 11 (64-Bit).
- **.NET:** .NET Framework 4.8 (ist bei aktuellem Windows normalerweise schon da).
- **Wichtig:** **Beende DXLog vor dem Export.** Solange DXLog läuft, sperrt es die Datenbankdatei.

---

## 2. Loslegen

Das Tool braucht keine Installation:
1. Lade `DxLogQsoExporter-vX.Y.Z-win-x64.zip` von GitHub herunter.
2. Entpacke die ZIP-Datei in einen Ordner deiner Wahl (z. B. auf den Desktop oder in deine Contest-Tools).
3. Starte `DxLogQsoExporter.exe`.

---

## 3. Bedienung

Die Oberfläche ist auf das Wesentliche reduziert:

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

### Schritt 1: `.dxn`-Logdatei wählen
Klicke neben **DXN log file** auf **Browse...** und wähle deine Contest-Logdatei aus.
- Das Tool schlägt dir automatisch einen Zielordner `<DeinLog>_QSO_Audio` vor.

### Schritt 2: MP3-Aufnahmeordner wählen
Klicke neben **MP3 recordings** auf **Browse...** und wähle den Ordner mit den Audioaufnahmen aus DXLog.
- Normalerweise sind das nummerierte Dateien wie `000.mp3`, `001.mp3`, etc.
- Ältere Logs mit nur einer einzelnen MP3-Datei im Ordner werden genauso unterstützt.

### Schritt 3: Exportordner bestätigen
Du kannst den vorgeschlagenen Pfad übernehmen oder über **Browse...** einen anderen Ordner wählen. Der Ordner wird beim Exportstart automatisch angelegt.

### Schritt 4: Clip-Dauer einstellen
- **Time before QSO (s):** Vorlaufzeit in Sekunden vor dem Logzeitpunkt (Standard: `30` Sekunden).
- **Total clip duration (s):** Gesamtlänge des Clips (Standard: `60` Sekunden). Bei 30s Vorlauf und 60s Gesamtdauer bekommst du 30s vor und 30s nach dem Eintrag.
- **Reset defaults:** Setzt die Zeiten wieder auf den DXLog-Standard (30s / 60s) zurück.

### Schritt 5: (SO2R / 2R) Stereo-Kanaltrennung nach Funkgerät
Wenn du in Stereo aufgenommen hast (Radio 1 links, Radio 2 rechts):
- Aktiviere **Extract one channel from stereo recording per radio (R1 = left, R2 = right)**.
- Das Tool trennt R1-QSOs in einen Unterordner `R1/` und R2-QSOs in `R2/` und exportiert sie jeweils als saubere Mono-MP3.

### Schritt 6: Export starten
Klicke auf **Export**. Wenn der Vorgang fertig ist, bringt dich ein Klick auf **Open export folder** direkt zum Zielordner im Windows Explorer.

---

## 4. Normaler Export vs. Kanaltrennung

| Modus | Arbeitsweise | Audioqualität | Speicherort |
| :--- | :--- | :--- | :--- |
| **Standard (Häkchen aus)** | **100% Lossless:** Kopiert die exakten MP3-Frames ohne Neukodierung direkt aus der Aufnahme. | Originalqualität, kein Qualitätsverlust. | Clips landen direkt im Exportordner. |
| **Kanaltrennung (Häkchen an)** | **Stereo zu Mono:** Dekodiert Stereo, isoliert den Kanal (R1=L, R2=R) und speichert als Mono-MP3. | Sauberer Monoton pro Radio. | Clips landen getrennt in `R1/` und `R2/`. |

---

## 5. Dateinamen

Die exportierten Dateien heißen standardmäßig:

```text
<QSO-Nummer>_<Datum>_<Uhrzeit>_<Call>_<Band>_<Mode>.mp3
```

- **Normales QSO:** `000123_2026-10-24_1423_W1AW_20M_CW.mp3`
- **XQSO (gestrichen/ungültig):** `000124_2026-10-24_1424_K3LR_20M_CW_XQSO.mp3`
- **Mit Kanaltrennung:** `R1/000123_2026-10-24_1423_W1AW_20M_CW.mp3`

---

## 6. Was die Statusanzeige bedeutet

Am Ende des Laufs siehst du die Auswertung:

- **Total QSOs:** Gesamtzahl der QSOs in der Datenbank.
- **Exported:** Erfolgreich exportierte Clips in voller Länge.
- **Shortened:** Clip wurde exportiert, ist aber kürzer als eingestellt, weil das QSO zu nah am Anfang oder Ende einer MP3-Datei lag (Audio wird bewusst nicht dateiübergreifend gestückelt).
- **Skipped:** Übersprungene QSOs (z. B. Aufnahme lief beim QSO noch nicht, MP3-Datei fehlt oder Clip existiert bereits im Zielordner).
- **Failed:** Unerwartete Lese-/Schreibfehler.
- **XQSOs:** Anzahl der exportierten XQSOs.

---

## 7. Report & Datenschutz

Jeder Durchlauf erzeugt eine Textdatei `_export-report_YYYYMMDDTHHMMSSZ.txt` im Exportordner.

- Enthält eine Liste aller übersprungenen oder verkürzten QSOs mit Begründung.
- **Datenschutz:** Der Report schwärzt automatisch deinen Windows-Benutzernamen und absolute Dateipfade. Du kannst ihn also problemlos in Foren oder auf GitHub teilen, wenn du ein Problem analysieren willst.

---

## 8. Schnelle Problemlösung

- **"DXLog appears to be using the selected log":** Schließe DXLog und klicke nochmal auf Export.
- **"Output file already present":** Das Tool überschreibt niemals bestehende Clips, um Datenverlust zu verhindern. Lösche alte Clips oder wähle einen neuen Zielordner.
- **Fehlende Clips:** Prüfe, ob alle `000.mp3`, `001.mp3`... Dateien im Aufnahmeordner liegen.
- **QTCs (WAE Contest):** DXLog speichert für QTC-Gruppen keine Byte-Offsets in den Aufnahmen, daher werden QTCs nicht unterstützt.
- **Multi-Op / Netzwerk:** Führe das Tool auf jedem Stations-PC einzeln für die jeweiligen lokalen Aufnahmen aus.

