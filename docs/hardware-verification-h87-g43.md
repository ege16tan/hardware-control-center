# Hardware-Verifikation MSI H87-G43

**Datum:** 2026-11-02
**Board:** MSI H87-G43 (MS-7816)
**CPU:** Intel Core i7-4770
**GPU:** NVIDIA GeForce GTX 1060 6GB
**Tool:** LibreHardwareMonitorLib 0.9.6, .NET 10.0.403
**Ergebnis:** ❌ Phase A (Lesen) nicht bestanden — der Motherboard-Super-I/O liefert 0 Sensoren.

---

## 1. Zusammenfassung

Die App kommuniziert derzeit **nicht** mit dem Motherboard. LibreHardwareMonitor
erkennt das Board (Name „MSI H87-G43 (MS-7816)"), liest daraus aber **keinen einzigen
Sensor**. Alle Temperatur- und Lüfterdaten des Boards sowie die CPU-Kerntemperatur
fehlen. Nur die GPU (über NVAPI, ohne Kernel-Treiber) liefert Messwerte.

Ursache ist nicht der Code und nicht ein fehlendes Admin-Recht, sondern der blockierte
Kernel-Treiber von LibreHardwareMonitor (WinRing0). Bis das geklärt ist, bleiben alle
Kanäle gesperrt (`MonitorOnly`/`ControlCandidate`), und `LhmFanController` schreibt
weiterhin nichts.

---

## 2. Fehlerlog (Messung)

Zwei Läufe mit identischem Ergebnis — einmal normal, einmal **mit Admin-Rechten (UAC)**.
Admin ändert nichts.

```
Status: (ok)
--- Devices ---
MSI H87-G43 (MS-7816)      Motherboard   sensors=0   <-- hier fehlt alles
Intel Core i7-4770         Cpu           sensors=14  (nur Load/Power, keine Temperatur)
Virtual Memory             Memory        sensors=3
Total Memory               Memory        sensors=3
NVIDIA GeForce GTX 1060    GpuNvidia     sensors=30  (Temp/Fan/Control vorhanden)
```

Gelesene Temperatur-/Lüfter-/Steuerwerte:

```
NVIDIA GeForce GTX 1060 6GB  Temperature  GPU Core        ~52 °C
NVIDIA GeForce GTX 1060 6GB  Fan          GPU               0 RPM
NVIDIA GeForce GTX 1060 6GB  Control      GPU Fan           0 %
NVIDIA GeForce GTX 1060 6GB  Temperature  GPU Hot Spot    ~60 °C
```

Muster: Alles, was einen Kernel-Treiber (Ring0) braucht, ist tot. Alles, was ohne
Treiber geht, funktioniert.

| Braucht Ring0-Treiber | Zustand | Braucht keinen Treiber | Zustand |
|---|---|---|---|
| Super-I/O (Lüfter, Boardtemp) | ❌ 0 Sensoren | CPU-Load % | ✅ ~94 % |
| CPU-Kerntemperatur (MSR) | ❌ fehlt | GPU (NVAPI) | ✅ |
| CPU-Power (RAPL) | ❌ 0 W | RAM | ✅ |

Kein WinRing0-/inpout-Treiber ist als Dienst installiert oder geladen.

---

## 3. Ursache

LibreHardwareMonitor greift für Super-I/O und CPU-MS-Register über den **WinRing0**-
Treiber zu. Dieser Treiber wird von Windows seit ca. 2023 über die *Vulnerable Driver
Blocklist* blockiert (Microsoft Defender meldet ihn als
[`VulnerableDriver:WinNT/Winring0`](https://support.microsoft.com/fr-fr/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0)).

Deshalb hilft „als Administrator ausführen" nicht: Es ist eine Treibersperre, keine
Rechtefrage. FanControl ist aus demselben Grund auf einen neuen Treiber
([PawnIO](https://github.com/Rem0o/FanControl.Releases/discussions/3494)) umgestiegen.

---

## 4. Was geändert werden muss, damit es geht

1. **Gegenprobe HWiNFO64** (eigener, signierter, nicht blockierter Treiber).
   Liest HWiNFO die Board-Lüfter/Temps → Board ist gesund, es liegt 100 % am
   WinRing0-Treiber.
2. **Ring0-Treiber ersetzen** — z. B. PawnIO oder ein anderer signierter, nicht
   blockierter Treiber. Damit funktioniert zuerst das Lesen; die Steuerung nutzt
   später dieselben, öffentlich dokumentierten NCT-Register (Datenblatt, kein
   Reverse Engineering).
3. **Nicht empfohlen:** Secure Boot aus oder die Vulnerable-Driver-Blocklist
   deaktivieren, um WinRing0 wieder zu laden. Das ist ein Sicherheits-Rückbau.

Nach einem erfolgreichen Schritt 2 ist **Phase A erneut** zu fahren (Diagnoseausgabe,
siehe `LUEFTERSTEUERUNG_PLAN.md`). Erst wenn Super-I/O, Lüfterheader und
Control-Sensoren sichtbar sind, ist **Phase B** (Schreibtest auf dem Board) möglich.

---

## 5. Status der Kanäle

- Alle Kanäle bleiben `MonitorOnly`/`ControlCandidate` — kein Kanal ist `ControlVerified`.
- `LhmFanController.SetPercent` und `.Reset` werfen weiterhin `NotSupportedException`.
- Die UI zeigt weiterhin „Steuerung gesperrt".

---

## 6. Referenzen

- `LUEFTERSTEUERUNG_PLAN.md` — Phasen A–D, Abschnitt 9 (Einschränkungen), Abschnitt 4.3 (Doku-Ablage)
- `PHASENPLAN.md` — Gesamtprojekt
- Microsoft: [WinNT/Winring0 Vulnerable Driver](https://support.microsoft.com/fr-fr/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0)
- FanControl: [PawnIO-Installation](https://github.com/Rem0o/FanControl.Releases/discussions/3494)
