# Minecart Network visual assets

Minecart Network loads three transparent PNG atlases for custom stations:

- `minecart.png` — the cart itself;
- `tracks.png` — repeatable vertical/horizontal rail sections;
- `mine_entrance.png` — the mine/tunnel entrance.

## Geometry contract

The station follows this logical layout:

`tunnel -> N track tiles -> minecart -> clear arrival tile`

where:

- the minecart occupies exactly **1 × 1 logical tile**;
- each track section occupies exactly **1 × 1 logical tile**;
- the tunnel/entrance occupies exactly **1 × 1 logical tile**, although its artwork may overhang above or sideways;
- the arrival tile in front of the minecart is not part of the station artwork and is the only tile which must remain genuinely clear and walkable;
- the whole station can face up, right, down, or left;
- track length is configurable from 0 to 8 sections.

## Current PolyCarts-derived art

The current PNGs use pixels taken directly from the PolyCarts sprite sheet and arranged into Minecart Network's directional atlases:

- `minecart.png`: **128 × 32 px** — four 32 × 32 frames: up, right, down, left;
- `tracks.png`: **32 × 16 px** — 16 × 16 vertical frame followed by 16 × 16 horizontal frame;
- `mine_entrance.png`: **192 × 48 px** — four 48 × 48 frames: up, right, down, left.

The renderer uses integer scaling for crisp pixel art:

- minecart: 64 × 64 world pixels;
- tracks: 64 × 64 world pixels per segment;
- entrance: 96 × 96 world pixels.

The minecart visually sits on the rails. Rails continue through the minecart tile and disappear into the tunnel opening.

Missing files remain supported. Minecart Network falls back independently to procedural rendering, so missing artwork cannot break placement, interaction, travel, or saved stations.
