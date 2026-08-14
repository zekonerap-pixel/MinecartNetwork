# Development status

## 0.1.0-alpha.1 — core travel prototype

### Implemented
- SMAPI C# project targeting .NET 6.
- Per-save station model and save data container.
- Host-side persistence using SMAPI save data.
- Station creation/removal/search service.
- Teleport service.
- Temporary SMAPI console test commands.
- English and Spanish translation scaffolding.

### Test flow
1. Load a save through SMAPI.
2. Stand on a destination tile.
3. Run `mn addhere Plaza Pueblo`.
4. Move elsewhere.
5. Run `mn list`.
6. Run `mn goto Plaza`.
7. Save, reload, and confirm `mn list` still contains the station.

### Known alpha limitations
- No in-world minecart object yet.
- No placement UI yet.
- No collapsible destination menu yet.
- Vanilla minecart integration is not patched yet.
- Multiplayer farmhand synchronization is not implemented yet; save data is host-owned.
- Visual track/wall-hole options are stored in the model but not rendered yet.

### Next implementation target
Placement and rendering layer:
- placement mode;
- tile validity checking;
- composite minecart rendering (cart + optional tracks + optional wall hole);
- interaction hitbox;
- station editor prompt.
