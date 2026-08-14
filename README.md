# Minecart Network

Early development version of a Stardew Valley SMAPI mod which will let players place named minecart stations and use them as destinations in a unified minecart travel network.

## Current milestone: 0.1.0-alpha.1

Implemented foundation:

- per-save custom station model;
- save/load persistence through SMAPI save data;
- station manager;
- teleport service;
- development console commands for end-to-end testing.

### Development commands

Load a save, then use the SMAPI console:

- `mn addhere <name> [category]`
- `mn list`
- `mn goto <name-or-id>`
- `mn remove <name-or-id>`

These commands are temporary development scaffolding. The final user-facing flow will use in-game placement and menus.

## Planned next milestone

- placement mode;
- visible station rendering;
- tracks / wall-hole visual options;
- clickable station interaction;
- collapsible destination menu grouped by location/map;
- vanilla minecart integration.
