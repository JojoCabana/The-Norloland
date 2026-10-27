# The Norloland – Ausrichtung und Übergabe vom 27.10.2026

Diese Datei fasst die Absprachen der gemeinsamen Sprachsitzung mit Gordon und Jamie zusammen. Sie ist eine inhaltliche Zusammenfassung, kein wortgetreues Transkript.

## Ziel und Arbeitsweise

- Eine zunehmend realistisch wirkende Open World mit erkennbaren natürlichen Materialien, Pflanzen und Ressourcen. Der vorhandene Stand ist ein prozeduraler, höhenbasierter Geländeprototyp, noch kein vollständiges dreidimensionales Voxelsystem mit Höhlen.
- Performance ist ein Schwerpunkt. Änderungen sollen vermeidbare Arbeit reduzieren und anschließend auf dem Zielrechner geprüft werden. Durchschnitts-FPS allein reichen nicht: Lade-Ruckler und Bildzeiten betrachten.
- Multiplayer ist ein ernsthaftes späteres Ziel. Die Architektur soll ihn berücksichtigen; die Umsetzung wurde zugunsten des Buildfehlers und der aktuellen Spielsysteme ausdrücklich zurückgestellt.
- Vor größeren weiteren Systemen Weltzustand, Spielregeln, Speicherung und Oberfläche trennen. Perspektivisch prüft eine maßgebliche Spielinstanz Aktionen wie Graben, Sammeln und Stapelverschieben; im Multiplayer übernimmt der Server diese Rolle. Aktuell gibt es noch keine Netzwerkimplementierung oder serverseitige Autorität.
- Jede zukünftige Code-/Funktionsänderung erhöht die sichtbare AppVersion in Scripts/Main.cs. Reine Dokumentationsnachträge verändern die ausführbare Version nicht.
- Auf dem Arbeitsbranch weiterarbeiten und Änderungen für den Laptop hochladen. Der Hauptbranch wurde in dieser Sitzung nicht verändert oder mit dem Arbeitsbranch zusammengeführt.

## Technische Basis und Zugriff

- Repository: https://github.com/JojoCabana/The-Norloland
- Arbeitsbranch: perf/shared-meshes-hud
- Aktuelle ausführbare Version: **0.1.5**
- Godot **4.7.2 .NET**, Godot.NET.Sdk/4.7.2, Desktop-Zielframework net8.0. Editor und Exportvorlagen im GitHub-Build sind auf 4.7.2 abgestimmt.
- Jamie hat Gordon als Collaborator eingeladen; nach Annahme war Schreiben über die GitHub-Anbindung möglich. Ein Fork war deshalb nicht erforderlich. Public allein gewährt keine Schreibrechte.
- Jamie hat den Branch nach Fetch lokal ausgecheckt und den Wechsel bestätigt. Für weitere Updates auf diesem Branch genügt `git pull`.

## Umgesetzte Änderungen dieser Sitzung

| Version | Änderung | Nachweis / Grenze |
| --- | --- | --- |
| erstes Paket | Gemeinsame Baum- und Busch-Meshes; HUD/Debuganzeige fünfmal pro Sekunde; ausgewachsene Büsche stoppen Wachstumsaufrufe | Codeprüfung; kein gemessener FPS-Vergleich |
| 0.1.1 | Gelände/Kollision zuerst, danach Bäume, Gras, Blumen, Büsche und Steine in getrennten Bildern; sichtbare Versionsnummer | Weiterhin Hauptthread, kein Worker-Thread und kein hartes Zeitbudget; vollständiges Nachladen dauert mehr Bilder. Nutzer meldete subjektiv eine Verbesserung. |
| 0.1.2 | Dauerhafte Weltänderungen, Seed-Wiederaufnahme, Inventarmengen und Slotanordnung; verschiebbare/tauschbare/zusammenlegbare Stapel, Rechtsklick halbiert | Stapelgrenze 99, mindestens 24 Plätze; zusätzliche Plätze bei Bedarf. Kein festes Rucksacklimit. |
| 0.1.3 | Namenskonflikt FileAccess im Speichercode durch System.IO.FileAccess.Write behoben | Drei gemeldete Builddiagnosen gingen auf diese Zeile zurück. |
| 0.1.4 | Ausrichtung von Projekt, SDK und CI auf Godot 4.7.2 .NET | Automatischer C#-Build und Windows-Export erfolgreich |
| 0.1.5 | Fortschrittsbalken über Steinablagerungen; kurzer Zerbrecheffekt beim letzten Schlag; lose aufhebbare Steinbruchstücke; erkennbare grüne Büsche, die beim ersten Schlag einzelne Äste freigeben | Automatischer C#-Build und Windows-Export erfolgreich; Aussehen, Bedienung und Neustartverhalten noch praktisch prüfen |

## Gewünschte Spielregeln und aktueller Stand

- Weltänderungen sollen unmittelbar gespeichert werden. Beispiel: Ein gegrabenes Loch bleibt nach einem Neustart erhalten.
- Inventar als Übersicht aus einzelnen Slots und Stapeln. Stapel per Ziehen umsortieren, gleiche Gegenstände zusammenlegen, unterschiedliche tauschen und per Rechtsklick teilen. Mengen und Anordnung bleiben erhalten.
- Intakter Busch: als grüne Pflanze erkennbar. Ein Linksklick zerlegt ihn in 1–4 sichtbare, gegabelte Äste. Aufnehmen anschließend einzeln mit E; kein automatischer Inventarzuwachs beim Zerschlagen. Zerstörte Büsche bleiben als verbrauchte Weltressourcen markiert und wachsen derzeit nicht wieder nach.
- Steinablagerung: Spitzhacke auswählen, Linksklick halten. Fortschrittsbalken umfasst alle fünf Treffer. Letzter Treffer erzeugt einen kurzen visuellen Fragment-Effekt und einen großen sowie einen kleinen aufhebbaren Stein. Der Effekt ist keine physikalische Bruchsimulation.
- Bereits abgeschlossene Treffer an Steinablagerungen sowie liegen gebliebene Äste/Steine werden gespeichert. Aufheben entfernt einen Drop aus dem Spielstand und erhöht das Inventar.
- Kleine bereits lose Steine können weiterhin direkt mit E aufgenommen werden.

## Speicherung und bekannte Grenzen

- Lokale Dateien unter Godot `user://worlds`; **Spielstände werden nicht über GitHub synchronisiert**. Code auf GitHub ist keine Sicherung des lokalen Spielstands.
- Eine JSON-Datei pro Seed und eine active.txt für die zuletzt verwendete Welt. Inspector-Seed 0 lädt diese Welt; beim ersten Start wird ein zufälliger Seed gewählt. Ein expliziter anderer Seed verwendet einen eigenen Spielstand.
- Gespeichert werden Grabtiefen, Bodenbewuchsstatus, verbrauchte Ressourcen, Busch-Wachstumswerte, Inventarmengen/-werkzeuge, Slotanordnung, lose Drops und Trefferstand von Steinablagerungen.
- Spielerposition und Blickrichtung werden derzeit **nicht** gespeichert. Die Forderung „alles sofort speichern“ ist deshalb eine weiterführende Zielvorgabe, keine bereits vollständig erfüllte Zusicherung für jeden Spielzustand.
- Benutzeraktionen schreiben synchron; passive Wachstumswerte werden periodisch mitgesichert. Bei großen Spielständen können diese Schreibvorgänge selbst Bildzeitspitzen verursachen.
- Speichern erfolgt über eine temporäre Datei mit Flush und anschließendem Ersetzen. Das ist kein versioniertes Backup und keine Garantie gegen jeden Strom-/Dateisystemfehler.
- Ein nicht lesbarer Spielstand wird nicht überschrieben; Fehler erscheinen in der Ausgabe und als SPEICHERFEHLER im HUD. Eine Wiederherstellungsoberfläche fehlt noch.
- Aktuelle Inventar-/Speicherlogik ist eng mit dem Gameplay verbunden und muss für eine spätere Netzwerkarchitektur getrennt werden.

## Prüfung und nächster Wiedereinstieg

Der Laptop war am Ende wegen geringer Akkulaufzeit nur noch für einen kurzen Praxistest verfügbar. Ein Ergebnis dieses letzten Tests liegt zum Zeitpunkt dieser Übergabe nicht vor. Keine weiteren Features vorziehen, bevor dieses Ergebnis geklärt ist.

1. Auf dem Arbeitsbranch `git pull`, Projekt mit Godot 4.7.2 .NET neu bauen/starten und HUD-Version 0.1.5 prüfen.
2. Busch zerschlagen, mindestens einen Ast aufnehmen und einen liegen lassen. Inventar öffnen, Stapel bewegen/teilen.
3. Steinablagerung abbauen: Balken, Fragment-Effekt und erst nach E erhöhtes Inventar prüfen.
4. Loch graben; vollständig schließen und neu starten. Derselbe Seed, Loch, Inventar und übrige Drops müssen erhalten sein; verbrauchte Originalressourcen dürfen nicht zurückkehren.
5. Chunk verlassen/zurückkehren und Pause während Aufbau/Abbau prüfen. Vollständige Hinweise in Docs/TESTING.md.
6. Danach Architektur für spätere Multiplayer-Unterstützung besprechen und planen; keine bereits erfolgte Multiplayer-Umsetzung annehmen.

## Buildnachweise

- Version 0.1.4: https://github.com/JojoCabana/The-Norloland/actions/runs/40463473796
- Version 0.1.5: https://github.com/JojoCabana/The-Norloland/actions/runs/40463847414
- Code-Commit 0.1.5: 0124ab63cd7c1db68166fa091f6bfbc271afc893

Erfolgreicher Build/Export bestätigt Kompilierung und Paketierung, nicht die korrekte Darstellung oder das Spielgefühl. Ausführbare CI-Artefakte werden nur befristet aufbewahrt.
