# Oberflächenwasser in der Spielwelt – 0.2.2

Der separate F7-Testmodus samt temporärer Mulde, Damm, eigener Kamera und Lens-Flare-Effekt ist entfernt. Die normale Landschaft wird für den Regen nicht verändert.

## Bedienung

Auf erreichbaren Gelände-Boden zielen: Der gelbe Rahmen zeigt den Zielchunk, unabhängig vom Standort der Figur. **R** schaltet Regen genau in diesem Chunk ein oder aus. Mehrere Chunks können gleichzeitig regnen. Die Beschriftung nennt Koordinaten und Regenzustand. Regen bleibt an der gewählten Weltposition, auch wenn die Figur weiterläuft. **Esc** pausiert das Spiel samt Wasser.

**V** wählt ein vorhandenes Startgefäß (Schüssel oder Krug), **B** füllt es aus der anvisierten Wasserzelle bis zum Fassungsvermögen, **P** gießt seinen Inhalt dort aus. Es wird nur tatsächlich vorhandenes Wasser übertragen. Bei verdecktem Boden muss eine andere Stelle anvisiert werden; Pflanzen werden nicht als Regenziel akzeptiert.

## Fluss und Speicherung

Die konservative Berechnung verwendet globale Meterzellen. Chunkgrenzen und Render-Sichtweite sind keine Wassergrenzen. Erreicht Wasser eine Nachbarzelle, wird deren prozedurale Bodenhöhe bei Bedarf berechnet, auch ohne geladenes Gelände-Mesh. Sichtbare Oberflächen verwenden gemeinsame weltweite Eckpegel und dieselben Ufer-Schnittpunkte an Chunkrändern. Regenquellen und Wasserstände bleiben beim Entladen sichtbarer Chunks erhalten und werden mit der Welt gespeichert. Beim Neuladen wird ab dem gespeicherten Zustand fortgesetzt; während das Spiel geschlossen ist, läuft keine Simulation.

Regen ist zum Ausprobieren bewusst auf **10 mm/s** beschleunigt. Nur gewählte Chunks liefern Wasser. Ausschalten stoppt den Zufluss. Vorhandenes Wasser fließt weiter und verdunstet mit der eingestellten Rate. Standard-Basisrate: **0,5 mm je Spielstunde**, einstellbar von 0 bis 5 unter **Esc → Einstellungen** (0 = aus). Temperatur und Sonneneinstrahlung multiplizieren die Basisrate. Im beregneten Chunk reduziert ein vereinfachter Bewölkungsfaktor die Verdunstung zusätzlich auf 35 Prozent. Die aktuelle Rate steht im HUD. Ein Spieltag dauert 20 reale Minuten; Verdunstung rechnet entsprechend in Spielzeit. Die Rate wird gespeichert. Verdunstung läuft auch bei Regen; der beschleunigte Zufluss bleibt unabhängig davon. Graben invalidiert die Bodenhöhen; offene Rinnen und Senken beeinflussen anschließend den Abfluss. Keine feste äußere Beckenwand, kein künstlicher Damm und keine Testmulde werden erzeugt.

## Vier Jahreszeiten

Frühling, Sommer, Herbst und Winter wechseln automatisch nach jeweils 28 Spieltagen. Ein Spieljahr hat 112 Tage. Tageszeit und Jahreszeit werden gespeichert. Neue bzw. bisherige Welten beginnen im Herbst am Mittag; es gibt keine Offline-Fortschreibung. **Esc → Einstellungen → Jahreszeit** erlaubt den direkten Wechsel bei erhaltener Tageszeit.

Jede Jahreszeit hat eine andere Tageslänge, Sonnenhöhe und vereinfachte Temperaturkurve. Tageszeit und Jahreszeit verändern Sonnenlicht und Umgebungslicht sowie die Verdunstung. Sommer ist wärmer und länger hell, Winter kälter und kürzer hell. In der Nacht fehlt direkte Sonneneinstrahlung, die Verdunstung sinkt entsprechend. Das HUD nennt Jahreszeit, Tag, Uhrzeit und Temperatur. Baum-Assets und Baumgeometrie werden in diesem Schritt nicht ersetzt. Schnee, Eis, Gefrieren, Laubfall und saisonale Vegetationsmodelle sind noch nicht enthalten; auch im Winter bleibt das vorhandene Wasser flüssig.

## Grenzen dieses Schritts

Dies ist eine Oberflächenberechnung mit einer Wasserhöhe pro Quadratmeter, keine dreidimensionale Flüssigkeitssimulation. Offene Ausgrabungen werden als oberste feste Oberfläche abgetastet. Gestapelte Höhlenbecken, Druckwasser, Untertunnelung unter noch geschlossenem Boden, freie Fallstrahlen, dynamische Bauwerksdämme, Versickerung ist noch nicht modelliert. Das Klima ist eine vereinfachte Spielparametrisierung, keine meteorologische oder hydrologische Fachsimulation. Eine volle Höhle unter einer trockenen Oberfläche lässt sich damit nicht abbilden. Die kleinen Shader-Wellen sind nur optisch.

Es werden nur benetzte Zellen und deren Nachbarn simuliert. Sehr kleine Restmengen bleiben gespeichert, ohne endlos weiter zu diffundieren. Lange Regenläufe über große Flächen erhöhen dennoch Rechen- und Speicherbedarf; eine konstante Bildrate für beliebig große Überschwemmungen wird nicht behauptet. Regenpartikel sind ein visueller Effekt ohne Tropfenkollision; die Zuflussmenge kommt aus dem Solver.

## Prüfungen

`Tests/WorldWaterChecks` prüft alle vier Chunkkanten einschließlich negativer Koordinaten, lokale Regenmenge, grenzüberschreitenden Abfluss ohne geladene Renderchunks, Mengenbilanz, Nichtnegativität, natürliche Aufstauung und Überlauf, geänderten Boden nach Graben, Speicher-Rundlauf, Gefäßbilanz sowie Verdunstungsrate, Trockenfallen und atmosphärische Mengenbilanz. Der Windows-Workflow führt diese Prüfungen zusätzlich zu den bestehenden Wasser- und Volumentests aus.

Manueller Spieltest: auf einen fremden Chunk zielen, R einschalten, Abfluss über dessen niedrigsten Rand beobachten, R ausschalten, eine Rinne öffnen, danach weggehen und zurückkehren und die Welt neu laden. Wasser und Regenzustand müssen erhalten bleiben. F7 darf keinen Modus oder Geländeumbau mehr auslösen.
