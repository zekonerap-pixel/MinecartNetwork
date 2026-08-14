# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them together with the game's minecart destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.6

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
- vanilla Default minecarts are routed into the unified menu through Harmony;
- vanilla destinations are read from `Data/Minecarts` instead of being hardcoded;
- vanilla unlock and destination conditions are respected through game-state queries;
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
- modded destinations injected into the vanilla `Default` `Data/Minecarts` network use the same region classifier;
- unknown minecart destination IDs are converted into friendlier display text when no specific translation is available;
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

To use a placed minecart, stand within interaction range and either click anywhere over its visible two-tile surface or use the game's configured action button. The unified menu mixes custom stations with currently available vanilla Default-network destinations and groups them by region/category.

When the network menu is opened from a custom physical minecart, use **Edit station** at the bottom-left to rename it, set a manual category, enable/disable automatic categorization, move it, or delete it.

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

Priced `Data/Minecarts` destinations are intentionally skipped by the unified menu for now so the mod doesn't bypass ticket/payment mechanics from other mods.

The current cart artwork is deliberately procedural placeholder pixel art so placement scale and interaction can be tested before final sprites are added.

## Next milestone

- add controller/snappy-menu navigation to the unified and station editor menus;
- preserve compatible custom minecart networks beyond the vanilla `Default` network;
- replace placeholder cart rendering with final pixel-art assets;
- add travel animation, sounds, and effects;
- later expand multiplayer synchronization and configuration support.
