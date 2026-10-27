# Geländeausbau 0.2.0

Feste Anforderungen: chunkübergreifende Höhlen, begehbare Grabungen mit Decken,
Schaufel statt Plasmaschneider, sichtbare Erd-/Sand-/Fels-/Erzschichten und die
Vegetation aus maguh1488/3dspiel. Spätere Aufbereitung bleibt Erweiterung.

VolumeTerrain.cs überträgt Dichtefeld, Schnitt zweier Rauschfelder für Tunnel,
Kammern, Kugelabzug und Marching Tetrahedra aus terrain_field.gd / terrain_mesher.gd.
Es verwendet globales, deterministisches Value Noise statt FastNoiseLite. Das
Höhlennetz ist deshalb neu generiert, folgt aber demselben Prinzip. Raster 0,5 m.
Nachbar-Chunks verwenden identische globale Stützstellen und Dichten. Geometrie
wird im Hintergrund erzeugt; GPU und Kollisionsform nur im Hauptthread angewendet.
Die Tiefengrenze wird bei Bedarf in 16-m-Schritten erweitert.

Alte gespeicherte Oberflächenvertiefungen bleiben als Ausgangsoberfläche erhalten.
Neue Grabungen speichern Kugelschnitte in Weltkoordinaten. Material wird in
0,25-m-Zellen ausschließlich für neu entfernte Festsubstanz gezählt. Herkunft,
Zusammensetzung und Volumen jeder Charge werden gespeichert. Das ist eine
Raster-Näherung, keine exakte analytische Volumenmessung. Bestehende Inventarzähler
zeigen zusätzlich Grabungsvorgänge; die Materialvolumen sind maßgeblich für
spätere Verarbeitung. Goldwaschen/Raffination ist noch nicht implementiert.

Oben Erde (0,45 m), dann Sand (bis 2,2 m), dann Fels. Eisenadern ab 7 m Tiefe.
Schichten folgen der ursprünglichen Oberfläche. Fels benötigt die vorhandene
Spitzhacke; Erde/Sand lassen sich mit Hand oder Schaufel abbauen.

Modelle, Texturen und Wind-/Bodenshader aus 3dspiel, Credits in Assets/Nature.
MultiMesh-Batches, nahe/ferne Laubbäume und begrenzte Grassichtweite. Kein
Performanceversprechen für die RX 5600 XT ohne Messung am Zielrechner.

Das F7-Wasserlabor behält seine gesonderte Höhenfeld-Testfläche; unterirdischer
Wasserfluss ist nicht Teil dieses Ausbaus. Die Kopplung des Oberflächenwassers
an ein dreidimensionales Höhlensystem ist gesondert zu entwickeln.

CI prüft Materialschichten, Höhlen, Erhalt der Decke, Grenzgeometrie,
Speicher-Rundlauf und überlappenden Abbau ohne Materialduplikation. Dazu Godot-
Build, Wasserprüfungen und Windows-Export. --landscape-demo erzeugt 150 echte
Spielbilder für ein zehnsekündiges 15-fps-MP4 (640 × 360 auf dem Software-Renderer); keine KI-generierte Vorschau.
