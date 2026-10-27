# Wasserlabor – Prototyp 0.1.6

## Start direkt in der laufenden Welt

F7 erzeugt einen begrenzten 20×20-Testbereich als Kind der bestehenden Welt. Es findet kein Szenenwechsel statt; die normale Spielfigur bleibt an ihrem Ort stehen, die Kamera blickt auf den Testbereich. Der Bereich liegt erhöht neben der Spielfigur, damit bestehendes Gelände und Spielstände nicht verändert werden. F7 oder Escape entfernt das Labor und kehrt zur Spielfigur zurück. Erneutes Öffnen beginnt mit leerem Wasser.

| Taste | Funktion |
| --- | --- |
| F7 | Wasserlabor erzeugen / schließen |
| R | Regen an/aus |
| D | Damm öffnen/schließen |
| Leertaste | Wasserberechnung pausieren |
| Links/Rechts | Kamera um den Bereich drehen |
| F | Lens-Flare-Effekt an/aus |
| Rücktaste | Wasser zurücksetzen |
| Escape | Zurück zur Spielfigur |

## Enthalten

- Höhenprofil mit Talrinne, geschlossene Außenränder und beschleunigter Regen.
- Pro Zelle Bodenhöhe und Wasservolumen; Übertragung zu niedrigeren Wasserständen.
- Ein Damm zwischen den beiden Hälften hält Wasser unterhalb seiner Krone zurück und kann überlaufen. Öffnen lässt Wasser abfließen.
- Bilanzanzeige für Wasser, Regenzugabe und Durchfluss. Ohne Quellen/Senken bleibt das Volumen erhalten.
- Wasser-Shader mit kleinen Wellen, Glanz und Fresnel-Farbverlauf; begrenzte Regenpartikel und abschaltbarer stilisierter Lens Flare.
- Feste Simulationsschritte (20 Hz), maximal vier Nachholschritte pro Bild. Bei zu langsamen Bildern verlangsamt sich die Simulation, statt unbegrenzt Rechenzeit nachzuholen.

## Grenzen und Ausrichtung

Das Labor ist in die vorhandene Szene eingebettet, aber hydraulisch nicht mit der prozeduralen Welt verbunden. Es ist kein flächendeckendes Wettersystem. Graben und Dämmebauen auf dem eigentlichen Spielgelände, freie Wasserfälle, Höhlenwasser, Chunk-übergreifende Wasserzustände, Speicherung des Laborwassers, Wasserrad und Stromerzeugung sind noch nicht implementiert. Die Shader-Wellen sind rein optisch und verändern kein Volumen. Zellübergänge können sichtbar bleiben; es handelt sich nicht um ein vollständig geglättetes Flüssigkeitsmesh. Der Lens Flare ist stilisiert und hat keine geometrische Verdeckungsprüfung.

Die Wasserberechnung in Scripts/WaterGrid.cs ist von Godot, Oberfläche und Darstellung getrennt. Das erleichtert Tests und eine spätere maßgebliche Serverberechnung, stellt aber selbst noch keinen Multiplayer her.

## Prüfen

1. F7 öffnen und Regen laufen lassen: Wasser sammelt sich in der Rinne; Bilanzfehler bleibt nahe null.
2. R ausschalten: Gesamtvolumen muss trotz Umverteilung gleich bleiben.
3. D öffnen und wieder schließen; Durchfluss und Verteilung beobachten. Mit längerem Regen muss ein geschlossener Damm überlaufen können.
4. Pause, Rücksetzen, Kameradrehung und Flare-Umschaltung prüfen.
5. F7/Escape: normale Kamera, Steuerung und Inventar wieder verfügbar; Weltänderungen bleiben erhalten.

Automatische Modelltests: `dotnet run --project Tests/WaterGridChecks/WaterGridChecks.csproj`. Sie prüfen Mengenerhaltung, nichtnegative Volumen, Regenzugabe, geschlossenen/geöffneten Damm, Überlaufen und trockenes Raster. Darstellung und Eingaben müssen zusätzlich im laufenden Spiel geprüft werden.
