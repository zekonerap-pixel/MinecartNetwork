# Minecart Network visual assets

Minecart Network can render the custom station from three optional transparent PNG layers:

- `minecart.png` — the cart itself;
- `tracks.png` — optional rails/sleepers;
- `wall_hole.png` — optional wall/tunnel opening.

## Canvas

Use a transparent **32 × 24 px** canvas for each layer. The game renders the canvas at 4× scale (**128 × 96 px**).

The bottom 16 source pixels align with the physical 2 × 1-tile minecart footprint. The upper 8 source pixels may extend above the cart footprint, primarily for the wall/tunnel opening and taller cart details.

All three PNG files must use the same canvas and alignment so they can be composited directly in this order:

1. wall hole;
2. tracks;
3. minecart.

Missing files are supported independently. If a layer doesn't exist, Minecart Network falls back to its current procedural rendering for that layer. This allows visual assets to be developed and replaced without changing placement, interaction, save data, or travel logic.
