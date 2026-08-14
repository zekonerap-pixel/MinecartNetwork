# Sprite layout

- `mine_entrance.png`: 4 horizontal frames in order up, right, down, left; each frame is 28x28 source pixels.
- `minecart.png`: 4 horizontal frames in order up, right, down, left; each frame is 24x24 source pixels (96x24 atlas total).
- `tracks.png`: vertical then horizontal; each frame is 16x16 source pixels (32x16 atlas total).

Rendering scale:
- minecart: exact 3x scale (72x72 world pixels), centered on a 64x64 logical cart tile with small directional offsets;
- tracks: 4x scale (64x64 world pixels);
- entrance: 4x scale with visual overhang allowed.

The minecart's 1x1 logical footprint, interaction area, placement geometry, travel data, and save format are unchanged. Procedural drawing remains fallback-only.