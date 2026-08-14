# Minecart Network visual assets

Minecart Network can load optional transparent PNG layers for the custom station:

- `minecart.png` — the cart itself;
- `tracks.png` — one track section;
- `wall_hole.png` — the tunnel/wall opening.

## Geometry note (alpha.9)

Station geometry is now directional and variable-length. A station can face up, right, down, or left, and can have 0–8 full track sections between the wall opening and the cart.

The old fixed 32 × 24 px composite is therefore no longer the final asset contract. During alpha.9:

- the existing PNG fallback path remains supported for the legacy/down-facing presentation;
- rotated/new geometry uses procedural rendering when no suitable directional art exists;
- tracks are repeated per configured section instead of being treated as one fixed station-wide layer;
- the placement and save-data model are now independent of final sprite dimensions.

This is intentional: geometry is being stabilized before final pixel art is produced. The next visual milestone will define directional source frames for cart, track section, and tunnel opening based on the validated in-game footprint.

Missing files are always supported. Minecart Network falls back independently to procedural rendering, so incomplete art cannot break placement, interaction, travel, or saved stations.
