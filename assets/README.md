# Minecart Network visual assets

Minecart Network can load optional transparent PNG layers for the custom station:

- `minecart.png` — the cart itself;
- `tracks.png` — one repeatable track section;
- `wall_hole.png` — the mine/tunnel entrance.

## Geometry contract (alpha.10)

The station now follows this logical layout:

`tunnel -> N track tiles -> minecart -> clear arrival tile`

where:

- the minecart occupies exactly **1 × 1 logical tile**;
- each track section occupies exactly **1 × 1 logical tile**;
- the tunnel/entrance occupies exactly **1 × 1 logical tile**, although its artwork may overhang above or sideways;
- the arrival tile in front of the minecart is not part of the station artwork and is the only tile which must remain genuinely clear and walkable;
- the whole station can face up, right, down, or left;
- track length is configurable from 0 to 8 sections.

## Source sizes

Stardew renders one 16 px source tile as 64 px in world space, so the intended final assets are:

- `minecart.png`: **16 × 16 px** source frame per direction;
- `tracks.png`: **16 × 16 px** source frame per direction;
- `wall_hole.png`: approximately **20 × 22 px** per direction, centered on its one-tile logical anchor with visual overhang allowed.

Directional sprites are not bundled yet. The current alpha uses procedural rendering for unsupported directions and missing files. This is deliberate so geometry can be tested independently from final art.

The minecart should visually sit on the rails. Rails continue through the minecart tile and disappear into the tunnel opening, matching the intended in-game composition.

Missing files are always supported. Minecart Network falls back independently to procedural rendering, so incomplete art cannot break placement, interaction, travel, or saved stations.
