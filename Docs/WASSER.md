# Oberflächenwasser in der Spielwelt – 0.2.3

Der separate F7-Testmodus samt temporärer Mulde, Damm, eigener Kamera und Lens-Flare-Effekt ist entfernt. Die normale Landschaft wird für den Regen nicht verändert.

## Bedienung

Auf erreichbaren Gelände-Boden zielen: Der gelbe Rahmen zeigt den Zielchunk, unabhängig vom Standort der Figur. **R** schaltet Regen genau in diesem Chunk ein oder aus. Mehrere Chunks können gleichzeitig regnen. Die Beschriftung nennt Koordinaten und Regenzustand. Regen bleibt an der gewählten Weltposition, auch wenn die Figur weiterläuft. **Esc** pausiert das Spiel samt Wasser.

**V** wählt ein vorhandenes Startgefäß (Schüssel oder Krug), **B** füllt es aus der anvisierten Wasserzelle bis zum Fassungsvermögen, **P** gießt seinen Inhalt dort aus. Es wird nur tatsächlich vorhandenes Wasser übertragen. Bei verdecktem Boden muss eine andere Stelle anvisiert werden; Pflanzen werden nicht als Regenziel akzeptiert.

## Fluss und Speicherung

Die konservative Berechnung verwendet globale Meterzellen. Chunkgrenzen und Render-Sichtweite sind keine Wassergrenzen. Erreicht Wasser eine Nachbarzelle, wird deren prozedurale Bodenhöhe bei Bedarf berechnet, auch ohne geladenes Gelände-Mesh. Sichtbare Oberflächen verwenden gemeinsame weltweite Eckpegel und dieselben Ufer-Schnittpunkte an Chunkrändern. Regenquellen und Wasserstände bleiben beim Entladen sichtbarer Chunks erhalten und werden mit der Welt gespeichert. Beim Neuladen wird ab dem gespeicherten Zustand fortgesetzt; während das Spiel geschlossen ist, läuft keine Simulation.

Niederschlag ist zum Ausprobieren bewusst auf **10 mm/s** beschleunigt. Nur gewählte Chunks liefern Wasser. Ausschalten stoppt den Zufluss. Vorhandenes Wasser fließt weiter und verdunstet mit der eingestellten Rate. Standard-Basisrate: **0,5 mm je Spielstunde**, einstellbar von 0 bis 5 unter **Esc → Einstellungen** (0 = aus). Temperatur und Sonneneinstrahlung multiplizieren die Basisrate. Im beregneten Chunk reduziert ein vereinfachter Bewölkungsfaktor die Verdunstung zusätzlich auf 35 Prozent. Die aktuelle Rate steht im HUD. Ein Spieltag dauert 20 reale Minuten; Verdunstung rechnet entsprechend in Spielzeit. Die Rate wird gespeichert. Verdunstung läuft auch bei Regen; der beschleunigte Zufluss bleibt unabhängig davon. Graben invalidiert die Bodenhöhen; offene Rinnen und Senken beeinflussen anschließend den Abfluss. Keine feste äußere Beckenwand, kein künstlicher Damm und keine Testmulde werden erzeugt.

## Vier Jahreszeiten

Frühling, Sommer, Herbst und Winter wechseln automatisch nach jeweils 28 Spieltagen. Ein Spieljahr hat 112 Tage. Tageszeit und Jahreszeit werden gespeichert. Neue bzw. bisherige Welten beginnen im Herbst am Mittag; es gibt keine Offline-Fortschreibung. **Esc → Einstellungen → Jahreszeit** erlaubt den direkten Wechsel bei erhaltener Tageszeit.

Jede Jahreszeit hat eine andere Tageslänge, Sonnenhöhe und vereinfachte Temperaturkurve. Tageszeit und Jahreszeit verändern Sonnenlicht und Umgebungslicht sowie die Verdunstung. Sommer ist wärmer und länger hell, Winter kälter und kürzer hell. In der Nacht fehlt direkte Sonneneinstrahlung, die Verdunstung sinkt entsprechend. Das HUD nennt Jahreszeit, Tag, Uhrzeit und Temperatur. Baum-Assets und Baumgeometrie werden in diesem Schritt nicht ersetzt. Schnee ist temperaturabhängig enthalten. Eis, Gefrieren, Laubfall und saisonale Vegetationsmodelle sind noch nicht enthalten; vorhandenes flüssiges Wasser friert im Winter noch nicht ein.

## Schnee und Schmelzwasser

Unter bzw. bei 0 °C fällt Schnee, ab 2 °C Regen. Dazwischen werden beide Anteile linear gemischt. **R** schaltet weiterhin Niederschlag im Zielchunk ein und aus; die Temperatur wählt die Form. Die vereinfachte mitteleuropäische Temperaturkurve umfasst Tagesgang und eine langsame Wetterabweichung, sodass kalte und milde Wintertage möglich sind. Ein kalter Wintertag zum Nachstellen liegt an Tag 5 der Wintersaison um Mittag; die Zeit läuft regulär weiter.

Schneefall wird in globalen Meterzellen als Wasseräquivalent gespeichert, nicht gleichzeitig als flüssiges Wasser erzeugt. Für die Darstellung wird frischer Schnee mit einem Verhältnis von 10:1 Schnee zu Wasser angenommen. Die erste Schneedecke ist eine raue weiße, aufwärts gerichtete Gelände- und Felsoberfläche; sie ändert noch keine Kollisionshöhe. Keine weißen senkrechten Höhlenwände, kein eigener Testboden. Die Schneemenge bleibt beim Entladen eines Chunks und im Spielstand erhalten.

Bei Temperaturen bis 0 °C bleibt Schnee liegen. Bei Plusgraden schmilzt er nach einer vereinfachten Gradtagregel; Wärme und Sonne beschleunigen, schattige/nächtliche Bedingungen reduzieren die Rate. Jeder geschmolzene Kubikmeter Wasseräquivalent geht exakt in dieselbe Wasserzelle und kann anschließend über Chunkgrenzen abfließen. Verdunstung betrifft in diesem Schritt flüssiges Wasser, nicht direkt den Schnee (keine Sublimation).

Die Schneeflocken verwenden ein transparentes sechszackiges Kristall-Sprite auf einer kamerazugewandten Fläche. Sie fallen langsam und drehen bzw. treiben leicht. Es sind keine Kugel- oder Boxpartikel. Die Kristallzeichnung wird einmal erzeugt und gemeinsam genutzt. Regen und Schnee können bei Übergangstemperaturen gleichzeitig sichtbar sein. Schnee auf Baumästen, detailgetreue Schneehaufen, Flockenkollision, Schneeverwehungen und gefrierende Gewässer folgen später.

## Grenzen dieses Schritts

Dies ist eine Oberflächenberechnung mit einer Wasserhöhe pro Quadratmeter, keine dreidimensionale Flüssigkeitssimulation. Offene Ausgrabungen werden als oberste feste Oberfläche abgetastet. Gestapelte Höhlenbecken, Druckwasser, Untertunnelung unter noch geschlossenem Boden, freie Fallstrahlen, dynamische Bauwerksdämme, Versickerung ist noch nicht modelliert. Das Klima ist eine vereinfachte Spielparametrisierung, keine meteorologische oder hydrologische Fachsimulation. Eine volle Höhle unter einer trockenen Oberfläche lässt sich damit nicht abbilden. Die kleinen Shader-Wellen sind nur optisch.

Es werden nur benetzte Zellen und deren Nachbarn simuliert. Sehr kleine Restmengen bleiben gespeichert, ohne endlos weiter zu diffundieren. Lange Regenläufe über große Flächen erhöhen dennoch Rechen- und Speicherbedarf; eine konstante Bildrate für beliebig große Überschwemmungen wird nicht behauptet. Niederschlagspartikel sind ein visueller Effekt ohne Einzelpartikelkollision; die Zuflussmenge kommt aus dem Solver.

## Prüfungen

`Tests/WorldWaterChecks` prüft alle vier Chunkkanten einschließlich negativer Koordinaten, lokale Regenmenge, grenzüberschreitenden Abfluss ohne geladene Renderchunks, Mengenbilanz, Nichtnegativität, natürliche Aufstauung und Überlauf, geänderten Boden nach Graben, Speicher-Rundlauf, Gefäßbilanz, Verdunstung und Jahreszeiten sowie Niederschlagsphasen, Schneespeicherung, temperatur-/sonnenabhängiges Tauen, vollständige Schneeschmelze und deren Wasserbilanz. Der Windows-Workflow führt diese Prüfungen zusätzlich zu den bestehenden Wasser- und Volumentests aus.

Die CI-Aufnahme `Snow-World-Preview` zeigt tatsächlich gerenderten Schneefall im normalen Gelände. Das frühere KI-Konzeptbild ist weiterhin kein Screenshot. Die Aufnahme ist nur ein Kommandozeilen-Capture für CI; F7 erzeugt keinen Spielmodus.

Manueller Spieltest: auf einen fremden Chunk zielen, R einschalten, Abfluss über dessen niedrigsten Rand beobachten, R ausschalten, eine Rinne öffnen, danach weggehen und zurückkehren und die Welt neu laden. Wasser und Regenzustand müssen erhalten bleiben. F7 darf keinen Modus oder Geländeumbau mehr auslösen.
