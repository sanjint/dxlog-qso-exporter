# DXLog QSO Exporter - Uputstvo za upotrebu

[English](user-guide.md) | **Bosanski** | [Deutsch](user-guide.de.md)

**DXLog QSO Exporter** je jednostavan i prenosiv (portable) Windows alat za contest operatore. Iz tvog DXLog dnevnika i MP3 snimaka automatski reže i sprema zaseban audio isječak za svaki odrađeni QSO.

---

## 1. Šta ti treba prije početka

- **OS:** Windows 10 ili 11 (64-bit).
- **.NET:** .NET Framework 4.8 (većina Windowsa ga već ima).
- **Važno:** **Ugasi DXLog prije nego pokreneš export.** DXLog drži bazu zaključanom dok radi, pa je program ne može pročitati.

---

## 2. Pokretanje

Nema instalacije:
1. Skini `DxLogQsoExporter-vX.Y.Z-win-x64.zip` sa GitHub-a.
2. Otpakuj ZIP gdje god želiš (npr. na desktop ili u folder sa contest alatima).
3. Pokreni `DxLogQsoExporter.exe`.

---

## 3. Kako se koristi

Interfejs je skroz jednostavan:

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

### Korak 1: Izaberi `.dxn` log
Klikni na **Browse...** kod **DXN log file** i izaberi svoj contest log.
- Program će ti automatski predložiti izlazni folder naziva `<TvojLog>_QSO_Audio`.

### Korak 2: Izaberi folder sa MP3 snimcima
Klikni na **Browse...** kod **MP3 recordings** i odaberi folder gdje DXLog snima audio tokom contesta.
- DXLog obično sprema numerisane datoteke (`000.mp3`, `001.mp3`, itd.).
- Ako imaš stariji log sa samo jednim MP3 fajlom u folderu, i to radi bez problema.

### Korak 3: Potvrdi izlazni folder
Možeš ostaviti automatski predloženi **Export folder** ili izabrati svoj preko **Browse...**. Folder će se sam napraviti kad krene export.

### Korak 4: Podesi dužinu isječaka
- **Time before QSO (s):** Koliko sekundi prije unosa QSO-a želiš uhvatiti (default je `30` sekundi).
- **Total clip duration (s):** Ukupno trajanje isječka (default je `60` sekundi). Sa 30s prije i 60s ukupno, dobijaš 30s prije unosa i 30s poslije.
- **Reset defaults:** Vraća vrijednosti na DXLog standard (30s / 60s).

### Korak 5: (SO2R / 2R) Izdvajanje kanala po radiju
Ako si snimao stereo (Radio 1 lijevo, Radio 2 desno):
- Uključi **Extract one channel from stereo recording per radio (R1 = left, R2 = right)**.
- Program će razvrstati R1 veze u `R1/` podfolder, a R2 veze u `R2/` podfolder i prebaciti ih u mono MP3.

### Korak 6: Pokreni Export
Klikni na **Export**. Kad završi, klikni na **Open export folder** da odmah otvoriš folder sa isječcima u Exploreru.

---

## 4. Default vs. Izdvajanje kanala

| Opcija | Kako radi | Kvalitet zvuka | Folder |
| :--- | :--- | :--- | :--- |
| **Default (isključeno)** | **100% Lossless:** Kopira direktno MP3 okvire iz snimka bez rekodiranja. | Originalni kvalitet bez ikakvog gubitka. | Isječci idu direktno u export folder. |
| **Extract per radio (uključeno)** | **Stereo u Mono:** Dekodira stereo, uzima odgovarajući kanal (R1=L, R2=R) i enkodira u mono MP3. | Čist mono zvuk za svaki radio posebno. | Isječci idu u `R1/` i `R2/` foldere. |

---

## 5. Imena fajlova

Fajlovi se automatski imenuju ovako:

```text
<QSO-Broj>_<Datum>_<Vrijeme>_<Znak>_<Band>_<Mod>.mp3
```

- **Normalan QSO:** `000123_2026-10-24_1423_W1AW_20M_CW.mp3`
- **XQSO (precrtan/poništen):** `000124_2026-10-24_1424_K3LR_20M_CW_XQSO.mp3`
- **Razdvojeni kanali:** `R1/000123_2026-10-24_1423_W1AW_20M_CW.mp3`

---

## 6. Šta znače rezultati exporta

Nakon exporta na dnu prozora vidiš statistiku:

- **Total QSOs:** Ukupno nađenih QSO-ova u bazi.
- **Exported:** Uspješno izrezani isječci u punom trajanju.
- **Shortened:** Isječak je uspješno izvezen, ali je skraćen jer je veza bila preblizu početka ili kraja pojedinačnog MP3 fajla (program namjerno ne lijepi audio preko različitih fajlova).
- **Skipped:** Preskočeni QSO-ovi (npr. nisi uključio snimanje na početku contesta, fali MP3 fajl, ili fajl već postoji u izlaznom folderu).
- **Failed:** Greške pri čitanju/pisanju.
- **XQSOs:** Broj XQSO veza u exportu.

---

## 7. Izvještaj i privatnost

Nakon svakog exporta dobijaš i tekstualni fajl `_export-report_YYYYMMDDTHHMMSSZ.txt`.

- Sadrži detaljnu listu svih preskočenih ili skraćenih veza s razlogom.
- **Privatnost:** Izvještaj automatski sakriva tvoje Windows korisničko ime i pune putanje, tako da ga možeš bez brige okačiti na forum ili GitHub ako tražiš pomoć.

---

## 8. Brzi savjeti i problemi

- **"DXLog appears to be using the selected log":** Samo ugasi DXLog i probaj opet.
- **"Output file already present":** Program nikad ne presnimava postojeće fajlove da ne bi izgubio podatke. Obriši stare isječke ili izaberi novi folder.
- **Nedostaju isječci:** Provjeri jesu li svi `000.mp3`, `001.mp3`... fajlovi u folderu sa snimcima.
- **QTC (WAE Contest):** DXLog ne sprema tačne bajt-pozicije za QTC grupe u audio snimku, pa QTC export nije podržan.
- **Multi-Op mreža:** Pokreni exporter na svakom računaru posebno za njegove lokalne snimke.

