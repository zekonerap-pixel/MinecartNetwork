# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them as destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.2

Implemented foundation:

- per-save custom station model;
- save/load persistence through SMAPI save data;
- station manager and teleport service;
- physical minecart placement mode;
- two-tile placement footprint plus a separate arrival tile;
- placement validation against map bounds, objects, terrain features, wall/building tiles, and existing minecarts;
- world rendering for placed minecarts;
- live placement preview with valid/invalid footprint;
- optional tracks and wall-hole visuals;
- English and Spanish placement HUD;
- development console commands for end-to-end testing.

### Development commands

Load a save, then use the SMAPI console:

- `mn addhere <name> [category]` — create a non-physical test destination at the player's tile;
- `mn place <name> [category]` — enter physical minecart placement mode;
- `mn list` — list saved stations;
- `mn goto <name-or-id>` — warp to a station;
- `mn remove <name-or-id>` — delete a station.

While placing a physical minecart:

- left click / controller A: place;
- `T`: toggle tracks;
- `H`: toggle wall hole;
- right click / Escape / controller B: cancel.

The current cart artwork is deliberately procedural placeholder pixel art so placement scale and footprint can be tested before final sprites are added.

## Next milestone

- interact with a placed minecart in the world;
- open the first destination menu from a placed cart;
- collapsible destination sections grouped by map/location;
- replace placeholder cart rendering with final pixel-art assets;
- integrate vanilla minecart destinations.
