# Wasserlabor – Prototyp 0.1.7

## Start direkt in der laufenden Welt

F7 formt eine temporäre Senke im bestehenden Gelände neben der Spielfigur und zeigt sie mit einer Übersichtskamera. Wasser liegt auf diesem Gelände: Es gibt keinen schwebenden Boden oder Beckenboxen mehr. Die Senke blendet an ihrem Rand in das ursprüngliche Höhenprofil über. Betroffene Gelände-Meshes und Kollisionen werden aktualisiert; Vegetation in betroffenen Chunks wird während des Tests ausgeblendet. F7/Escape beendet den Test und stellt das ursprüngliche Gelände wieder her. Die Spielfigur bleibt am Ausgangsort.

Schüssel (2 L) und Krug (5 L) stehen für den Prototyp als Startgefäße im Slotinventar bereit. Im Labor wählt V das Gefäß, Linksklick eine gelb markierte Wasserzelle, B füllt und P gießt aus. Es wird nur die verfügbare Menge bis zum Fassungsvermögen übertragen (1 m³ = 1000 L). Der Zellenwert zeigt auch kleine Mengenänderungen. Gefäßfüllstände werden gespeichert, das Laborwasser und die temporäre Geländeform noch nicht. Noch kein freies Befüllen aus der Egoansicht oder Gefäß-Crafting.

| Taste | Funktion |
| --- | --- |
| F7 | Wasserlabor erzeugen / schließen |
| V / B / P | Gefäß wählen / füllen / ausgießen |
| Linksklick | Entnahme-/Ausgießzelle wählen |
| R | Regen an/aus |
| G | Damm öffnen/schließen |
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

Das Labor ist in die vorhandene Szene eingebettet, aber hydraulisch nicht mit der prozeduralen Welt verbunden. Es ist kein flächendeckendes Wettersystem. Graben und Dämmebauen auf dem eigentlichen Spielgelände, freie Wasserfälle, Höhlenwasser, Chunk-übergreifende Wasserzustände, Speicherung des Laborwassers, Wasserrad und Stromerzeugung sind noch nicht implementiert. Die Wasseroberfläche verwendet nun gemeinsame Eckhöhen statt überlappender Einzelkacheln. Die Shader-Wellen sind rein optisch und verändern kein Volumen. Zellübergänge können sichtbar bleiben; es handelt sich nicht um ein vollständig geglättetes Flüssigkeitsmesh. Der Lens Flare ist stilisiert und hat keine geometrische Verdeckungsprüfung.

Die Wasserberechnung in Scripts/WaterGrid.cs ist von Godot, Oberfläche und Darstellung getrennt. Das erleichtert Tests und eine spätere maßgebliche Serverberechnung, stellt aber selbst noch keinen Multiplayer her.

## Prüfen

1. F7 öffnen und Regen laufen lassen: Wasser sammelt sich in der Rinne; Bilanzfehler bleibt nahe null.
2. R ausschalten: Gesamtvolumen muss trotz Umverteilung gleich bleiben.
3. G öffnen und wieder schließen; Durchfluss und Verteilung beobachten. Mit längerem Regen muss ein geschlossener Damm überlaufen können.
4. Pause, Rücksetzen, Kameradrehung und Flare-Umschaltung prüfen.
5. F7/Escape: normale Kamera, Steuerung und Inventar wieder verfügbar; Weltänderungen bleiben erhalten.

Automatische Modelltests: `dotnet run --project Tests/WaterGridChecks/WaterGridChecks.csproj`. Sie prüfen Mengenerhaltung, nichtnegative Volumen, Regenzugabe, geschlossenen/geöffneten Damm, Überlaufen und trockenes Raster. Darstellung und Eingaben müssen zusätzlich im laufenden Spiel geprüft werden.

## 0.1.9 – Wasser wächst aus der Mitte

Der Testbereich startet trocken. Regen wird als gesammelter Zufluss in die mittlere Zelle der Senke geleitet, statt jede Zelle zu benetzen. Das Gelände steigt in beiden Achsen an. Der Pegel steigt zuerst im Zentrum und erreicht umliegende Zellen erst über deren Bodenhöhe. Höhere Kanten halten Wasser zurück, bis sie überlaufen. Das Netz bleibt ein begrenztes, am Außenrand geschlossenes Labor; chunkübergreifende Hydrologie ist noch nicht umgesetzt. Die Oberfläche wird an der Geländeform abgeschnitten und am flachen Ufer transparent. Rücktaste leert das Becken und erhält den mittigen Zufluss.

## 0.1.10 – Damm und Wasseroberfläche

Geschlossene Dämme trennen auch die Glättung der Wasseroberfläche: Ober- und Unterwasser behalten ihren eigenen Pegel. Unterhalb der Krone endet das Wasser an den sichtbaren Wandflächen. Das HUD nennt die Höhe der Dammkrone und unterscheidet Überlauf (geschlossen) von Durchfluss (offen). Über der Krone darf Wasser weiterhin auf die andere Seite fließen. Prüfungen decken getrennte Oberflächenpegel sowie Dichtigkeit bei anhaltendem Zufluss aus beiden Richtungen ab.

Freie Flugkamera im Wasserbereich: WASD bewegt in Blickrichtung bzw. seitwärts, Q/E senkt/hebt die Kamera, Umschalt beschleunigt. Rechte Maustaste halten und Maus bewegen zum Umschauen; Loslassen gibt den Mauszeiger für die Wasserauswahl frei. Pfeiltasten links/rechts kehren zur Übersicht zurück. Die Kamera funktioniert auch bei pausiertem Wasser. Damm öffnen/schließen liegt jetzt auf **G**, damit D zum Fliegen frei ist.
