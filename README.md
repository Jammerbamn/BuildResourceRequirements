# Build Resource Requirements

**Build Resources Requirements** is a mod that allows control over whether resources are required for specific build categories, tools, and pieces. This includes support for vanilla and modded categories, and configuration syncing. I wrote this mod because I didn't really like how the world modifier for disabling resources turned off everything, including the cultivator.

## Features
- **Category-Based Resource Requirements:** Configure resource requirements for specific build categories (e.g., Crafting, Furniture, Misc.).
- **Tool-Specific Configurations:** Enable or disable resource requirements for tools such as the Cultivator and Hoe.
- **Piece Exceptions:** Specify individual pieces that should never require resources, regardless of their category.
- **Modded Category Support:** Automatically detects and configures newly added modded categories.
- **Skill Based Resource Requirements:** Configurable option to reduce resource requirements depending on the players crafting skill level.
- **Multiplayer Synchronization:** Ensures all players share the same configuration when connected to a multiplayer server.

## Installation
1. Install **BepInEx**.
2. Extract the `BuildResourcesMod.dll` into the `BepInEx/plugins` folder.
3. Launch Valheim to generate the configuration file.

## Configuration
The configuration file is generated in `BepInEx/config/Jammerbam.buildresourcesmod.cfg`.

### Categories
These are the available categories and their defaults:
```ini
[Categories]
MiscRequiresResources = true
CraftingRequiresResources = true
FurnitureRequiresResources = false
BuildingWorkbenchRequiresResources = false   (Build)
BuildingStonecutterRequiresResources = false (Heavy Building)
CultivatorRequiresResources = true
HoeRequiresResources = true
```

### Exceptions
Individual pieces can be added to the exceptions list:
```ini
[Exceptions]
## Comma-separated list of pieces that are always buildable.
# Setting type: String
# Default value: 
PieceExceptions = darkwood_wolf,darkwood_raven,wood_dragon1
```
Add piece names as a comma-separated list. Exceptions override category-based settings and never require resources.

### Skill Based Resource Reduction
The mod has an option to enable a feature to reduce resource requirements depending on the players crafting sill level.<br>
This works as a percentage, so if the player has a crafting skill of 50, the required resources will be 50% less.<br>
There is also another option to completly disable resource requirements if the player has a crafting skill of 100. If this option is disabled, all pieces will require at least 1 of each required item.

### Modded Categories
The mod will detect modded categories and add them to the config file. These categories are usually added when you load a world. If you want to configure a modded category, load a world, then close the game.<br>
Currently, the way that modded categories are displayed in the config file is by a numerical value, which is assigned when it is loaded in the game.

```ini
## Require resources for modded category: $category_9
# Setting type: Boolean
# Default value: true
9RequiresResources = true
```

If you need to find the category, enable the debugging option, and select a piece from the category. In the log it should show something like this:
```plaintext
[Info   : Unity Log] [BuildResourcesMod] Piece 'Armory_TW' in category '9' requires resources: True
```
You can also use this same method to find the name of a piece to add it to exceptions.

## Disclaimer
This is my very first mod, and my first time coding in C#. There are things that will inevitably be broken as I haven't been able to test for all scenarios. Please report if anything goes wrong so I can fix it.

## Planned Changes
- Fix the naming scheme in the config to make it more user-friendly.
- Continue updating and support for game updates.

## Credits
Almost all of the code was written by me however the config syncing is powered by:
https://github.com/blaxxun-boop/ServerSync

## Test text