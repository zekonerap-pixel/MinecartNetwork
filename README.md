# Minecart Network

Early development version of a Stardew Valley SMAPI mod which lets players create named minecart stations and use them together with the game's minecart destinations in an expandable travel network.

## Current milestone: 0.1.0-alpha.9

Implemented foundation:

- per-save custom station model and persistence;
- physical minecart placement with a separate arrival tile;
- stations can face up, right, down, or left;
- configurable 0–8 track sections between the tunnel opening and the minecart;
- new stations start with two intermediate track sections;
- station rotation and track length are stored per station and preserved when moving it;
- the cart footprint rotates with the station and remains fully interactive;
- the arrival tile is always placed immediately beyond the minecart, opposite the tunnel;
- only the arrival tile must be clear and walkable; the tunnel/track/cart construction corridor may cross structural map tiles and existing world geometry;
- station-to-station overlap is still prevented;
- live preview outlines the entire construction corridor and highlights the required free arrival tile separately;
- simple generated world clutter inside the construction corridor is cleared automatically when the station is confirmed;
- reversible clutter currently includes spawned forage and litter/debris objects identified by the game;
- cleared object ID, stack, quality, position, and spawned state are stored with the station;
- moving or deleting a station restores those cleared objects before changing/removing the station;
- restoration refuses to overwrite a newly occupied tile, preventing silent item loss;
- complex/player-owned objects such as machines and chests are never destructively removed by the cleanup system;
- optional tracks and wall-hole visuals;
- movement remains available while placement controls are isolated;
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
- moving a station keeps the original position until the new position is confirmed;
- deleting a station requires a second confirmation click;
- new custom stations use automatic region classification when no category is supplied;
- automatic/manual category mode is stored per station, while older saves remain manual by default;
- automatic stations are grouped dynamically from their current map, so their effective region follows their location;
- automatic regions currently include Town, Mines, Farm, Mountain, Forest, Beach, Desert, Ginger Island, and Other;
- location names and location types are inspected heuristically so many modded maps can be classified without explicit compatibility patches;
- full controller navigation in the unified network and station editor menus;
- optional layered sprite loading with procedural fallbacks while directional pixel art is being developed;
- automatic GitHub Actions build validation using the SMAPI mod build environment;
- English and Spanish interface text.

### Development commands

Load a save, then use the SMAPI console:

- `mn addhere <name>` — create a non-physical test destination using automatic region classification;
- `mn addhere <name> <category>` — create a non-physical test destination with a manual category override;
- `mn place <name>` — enter physical placement mode using automatic region classification;
- `mn place <name> <category>` — enter physical placement mode with a manual category override;
- `mn list` — list saved stations including direction, track length, and reversible cleared-object count;
- `mn goto <name-or-id>` — warp to a station;
- `mn remove <name-or-id>` — delete a station and restore its reversible cleared environment.

### Station placement controls

While placing or moving a physical station:

- left click / controller A: place or confirm;
- `R` / controller X: rotate clockwise;
- `Q` / controller LB: reduce the number of intermediate track sections;
- `E` / controller RB: increase the number of intermediate track sections;
- `T`: toggle tracks;
- `H`: toggle wall/tunnel opening;
- right click / Escape / controller B: cancel.

Track length ranges from **0 to 8**. It represents the number of complete rail sections between the wall opening and the minecart.

The blue preview outlines the station construction corridor. This area may overlap ordinary map structure and world geometry. The green tile immediately beyond the cart is the arrival/exit tile and is the only environmental tile which must remain genuinely clear and walkable.

The cleanup pass deliberately avoids destructive handling of complex objects. It automatically removes and records only simple objects the game identifies as spawned forage or litter/debris. Those objects are restored when the station is moved or removed. If their original tile has since become occupied, restoration is blocked instead of overwriting the new object.

Stations created before alpha.9 remain compatible: they load with the legacy down-facing geometry and zero intermediate track sections.

### Using the network

To use a placed minecart, stand within interaction range and either click anywhere over its visible cart surface or use the game's configured action button. Custom stations join the game's `Default` minecart network and are grouped with available destinations by region/category.

Controller controls in the unified network menu:

- D-pad / left stick up-down: move selection;
- A: activate the selected category, destination, or Edit station button;
- B: close the menu;
- D-pad / left stick left-right on a category: collapse or expand it;
- LB / RB: move several rows at once through long destination lists.

When the network menu is opened from a custom physical minecart, use **Edit station** to rename it, set a manual category, enable/disable automatic categorization, move it, or delete it. Moving a station also lets you change its orientation and track length.

### Third-party minecart networks

Minecart Network recognizes named networks in `Data/Minecarts`, not only the vanilla `Default` network. If another mod opens an unlocked network with no currently available paid destinations, Minecart Network replaces that menu with the same collapsible/controller-friendly UI while keeping the destinations isolated to that network.

If a third-party network exposes an available destination with a price, Minecart Network deliberately does **not** replace its menu. This preserves the other mod's payment/ticket behavior instead of providing a free warp.

### Visual asset pipeline

Station geometry is now directional and variable-length, so the old fixed 32 × 24 px three-layer composite is no longer the final art contract. See `assets/README.md` for the current transition notes.

The current procedural art remains the fallback while the final directional cart, repeated track section, and tunnel opening sprites are designed against the alpha.9 geometry.

## Next milestone

- validate the new four-direction placement geometry and reversible cleanup in-game;
- define and integrate final directional pixel-art frames for cart, track section, and tunnel opening;
- add travel animation, sounds, and effects;
- expand compatibility rules for unusual third-party network metadata/payment systems;
- later expand multiplayer synchronization and GMCM/configuration support.
