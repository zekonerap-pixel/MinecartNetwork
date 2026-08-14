# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them together with the game's minecart destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.8

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
- hover feedback for categories, destinations, and management controls;
- custom and vanilla minecart destinations share the same menu;
- vanilla and compatible modded minecart menus are routed into the unified UI through Harmony;
- minecart destinations are read from `Data/Minecarts` instead of being hardcoded;
- network unlock and destination conditions are respected through game-state queries;
- the `Default` network includes Minecart Network custom stations;
- compatible third-party networks are opened as their own isolated network instead of being merged into `Default`;
- third-party networks with available priced destinations fall back to their original menu so ticket/payment mechanics aren't bypassed;
- custom physical stations can be renamed, recategorized, moved, or deleted from an in-game editor;
- moving a station reuses placement preview/validation and keeps the original position until the new position is confirmed;
- deleting a station requires a second confirmation click;
- new custom stations use automatic region classification when no category is supplied;
- automatic/manual category mode is stored per station, while older saves remain manual by default;
- automatic stations are grouped dynamically from their current map, so their effective region follows their location;
- the station editor can switch between automatic grouping and a user-defined manual category;
- automatic regions currently include Town, Mines, Farm, Mountain, Forest, Beach, Desert, Ginger Island, and Other;
- location names and location types are inspected heuristically so many modded maps can be classified without explicit compatibility patches;
- unknown maps safely fall back to Other instead of guessing an unrelated region;
- modded destinations use the same region classifier;
- unknown minecart destination IDs are converted into friendlier display text when no specific translation is available;
- full controller navigation in the unified network menu;
- D-pad/left stick moves through categories and destinations, A activates, and B closes;
- left/right collapses or expands the selected category, while LB/RB move through long lists faster;
- controller focus automatically keeps the selected row inside the visible scroll area;
- the station editor is fully operable with D-pad/left stick, A, and B;
- controller-selected rows/buttons use the same visual highlight language as mouse hover;
- optional layered sprite pipeline for `assets/minecart.png`, `assets/tracks.png`, and `assets/wall_hole.png`;
- each missing visual layer falls back independently to the existing procedural art, allowing sprites to be introduced without breaking stations or save data;
- automatic GitHub Actions build validation using the SMAPI mod build environment;
- English and Spanish interface text.

### Development commands

Load a save, then use the SMAPI console:

- `mn addhere <name>` — create a non-physical test destination using automatic region classification;
- `mn addhere <name> <category>` — create a non-physical test destination with a manual category override;
- `mn place <name>` — enter physical placement mode using automatic region classification;
- `mn place <name> <category>` — enter physical placement mode with a manual category override;
- `mn list` — list saved stations grouped by their effective category;
- `mn goto <name-or-id>` — warp to a station;
- `mn remove <name-or-id>` — delete a station.

While placing or moving a physical minecart:

- left click / controller A: place or confirm the new position;
- `T`: toggle tracks;
- `H`: toggle wall hole;
- right click / Escape / controller B: cancel.

To use a placed minecart, stand within interaction range and either click anywhere over its visible two-tile surface or use the game's configured action button. Custom stations join the game's `Default` minecart network and are grouped with available destinations by region/category.

Controller controls in the unified network menu:

- D-pad / left stick up-down: move selection;
- A: activate the selected category, destination, or Edit station button;
- B: close the menu;
- D-pad / left stick left-right on a category: collapse or expand it;
- LB / RB: move several rows at once through long destination lists.

When the network menu is opened from a custom physical minecart, use **Edit station** at the bottom-left to rename it, set a manual category, enable/disable automatic categorization, move it, or delete it. The station editor can also be used entirely with a controller.

Automatic grouping currently recognizes common map signals for:

- Town;
- Mines;
- Farm;
- Mountain;
- Forest;
- Beach;
- Desert;
- Ginger Island;
- Other (fallback).

This classifier also examines modded map identifiers and runtime location types. For example, identifiers containing recognizable terms such as `forest`, `desert`, `island`, or `mine` can be grouped without a dedicated compatibility patch. Manual categories always remain available when the automatic result isn't appropriate.

### Third-party minecart networks

Minecart Network now recognizes named networks in `Data/Minecarts`, not only the vanilla `Default` network. If another mod opens an unlocked network with no currently available paid destinations, Minecart Network replaces that menu with the same collapsible/controller-friendly UI while keeping the destinations isolated to that network.

If a third-party network exposes an available destination with a price, Minecart Network deliberately does **not** replace its menu. This preserves the other mod's payment/ticket behavior instead of providing a free warp.

### Visual asset pipeline

The renderer supports three optional transparent PNG layers:

- `assets/minecart.png`;
- `assets/tracks.png`;
- `assets/wall_hole.png`.

The standard source canvas is **32 × 24 px**, rendered at 4× scale. All layers share the same alignment. Detailed alignment notes are in `assets/README.md`.

No final PNG artwork is bundled yet. Until a layer exists, the current procedural placeholder is used for that layer, so the visual migration can happen incrementally without touching placement, interaction, save data, or network logic.

## Next milestone

- create and integrate the final pixel-art minecart, tracks, and wall-hole layers;
- add travel animation, sounds, and effects;
- expand compatibility rules for unusual third-party network metadata/payment systems;
- later expand multiplayer synchronization and GMCM/configuration support.
