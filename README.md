# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them as destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.3

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
- movement remains available while placement controls are isolated;
- physical minecart interaction through the player's configured action button;
- first in-game destination menu;
- collapsible destination categories;
- scroll support for larger networks;
- travel directly from menu selections;
- English and Spanish interface text;
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

To use a placed minecart, stand next to either of its two occupied tiles, face the cart, and press the game's configured action button. The destination menu groups all other enabled stations by category. Click a category header to collapse/expand it, or click a station to travel.

The current cart artwork is deliberately procedural placeholder pixel art so placement scale and footprint can be tested before final sprites are added.

## Next milestone

- improve menu visuals, hover states, and controller navigation;
- automatically map vanilla locations into friendly broad regions;
- integrate vanilla minecart destinations into the same menu;
- add station edit / rename / move / delete flow;
- replace placeholder cart rendering with final pixel-art assets.
