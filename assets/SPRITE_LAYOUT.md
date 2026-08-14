# Sprite layout

Current station art uses the PolyCarts-derived PNG assets supplied in `assets/`:

- `minecart.png`: 4 horizontal frames in order up, right, down, left; each frame is 32x32 source pixels (128x32 atlas total).
- `tracks.png`: vertical then horizontal; each frame is 16x16 source pixels (32x16 atlas total).
- `mine_entrance.png`: 4 horizontal frames in order up, right, down, left; each frame is 48x48 source pixels (192x48 atlas total).

World rendering remains independent from the logical station footprint and uses integer pixel-art scaling:

- minecart: 64x64 world pixels (32 -> 64, x2);
- tracks: 64x64 world pixels per segment (16 -> 64, x4);
- entrance: 96x96 world pixels (48 -> 96, x2), with visual overhang allowed.

The minecart still occupies exactly one logical tile, and the interaction area, placement geometry, travel data and save format are unchanged. Procedural drawing remains fallback-only if an asset is missing.
