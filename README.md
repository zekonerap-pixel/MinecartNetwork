# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them together with the game's minecart destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.4

Implemented foundation:

- per-save custom station model and persistence;
- physical minecart placement with a two-tile footprint and separate arrival tile;
- placement validation and live valid/invalid preview;
- optional tracks and wall-hole visuals;
- movement remains available while placement controls are isolated;
- full 2x1 minecart surface is interactive;
- action cursor appears while hovering an in-range custom minecart;
- left click anywhere on the cart surface or use the configured action button to open the network;
- collapsible destination categories and scroll support;
- custom and vanilla minecart destinations share the same menu;
- vanilla Default minecarts are routed into the unified menu through Harmony;
- vanilla destinations are read from `Data/Minecarts` instead of being hardcoded;
- vanilla unlock and destination conditions are respected through game-state queries;
- known vanilla destinations are grouped into friendly regions such as Town, Mines, Farm, and Mountain;
- automatic GitHub Actions build validation using the SMAPI mod build environment;
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

To use a placed minecart, stand within interaction range and either click anywhere over its visible two-tile surface or use the game's configured action button. The unified menu mixes custom stations with currently available vanilla Default-network destinations and groups them by region/category.

Priced `Data/Minecarts` destinations are intentionally skipped by the unified menu for now so the mod doesn't bypass ticket/payment mechanics from other mods.

The current cart artwork is deliberately procedural placeholder pixel art so placement scale and interaction can be tested before final sprites are added.

## Next milestone

- refine automatic region mapping for custom and modded locations;
- improve menu hover states and controller navigation;
- preserve compatible custom minecart networks beyond the vanilla `Default` network;
- add station edit / rename / move / delete flow;
- replace placeholder cart rendering with final pixel-art assets;
- add travel animation, sounds, and effects.
