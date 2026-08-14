# Sprite layout

- `mine_entrance.png`: 4 horizontal frames in order up, right, down, left; each frame is 28x28 source pixels.
- `minecart.png`: 4 horizontal frames in order up, right, down, left; each frame is 24x24 source pixels (96x24 atlas total).
- `tracks.png`: vertical then horizontal; each frame is 16x16 source pixels (32x16 atlas total).

World rendering uses fixed target sizes instead of per-asset x2/x3/x4 multipliers:
- minecart: 64x64 world pixels;
- tracks: 64x64 world pixels;
- entrance: 80x80 world pixels, allowing visual overhang while the logical footprint remains unchanged.

The source atlas size no longer determines the in-game size. This avoids mixed scale factors and keeps all station geometry, interaction, travel data, and save format unchanged. Procedural drawing remains fallback-only.