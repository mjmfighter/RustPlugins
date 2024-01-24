# MjEntityLimiter Plugin for Rust

## Description

The `MjEntityLimiter` is a Rust server plugin designed to limit the number of specific entities a player can spawn or build. It's particularly useful for server administrators who want to maintain server performance and gameplay balance by preventing players from over-spawning entities that might cause lag or disrupt the gameplay experience. The plugin is highly configurable, allowing administrators to set specific limits for different entities and provide warnings to players as they approach these limits.

## Features

- **Entity Limit Enforcement**: Limits the number of instances a player can spawn or build of specific entities.
- **Permission-Based Limits**: Allows different limits for different groups of players based on permission settings.
- **Warning System**: Notifies players as they approach the entity limit.
- **Configurable**: All aspects of the plugin can be configured, including the entities to limit, the limits themselves, and the warning threshold.

## Configuration Options

### Main Configuration

- `Enable more debugging messages`: If set to `true`, the plugin will output more detailed debug messages to the server console.
- `Chat Prefix`: The prefix used for chat messages sent by this plugin.
- `Warn about limits below x percent`: The percentage of the limit at which players start receiving warnings. For example, if set to `10`, players will be warned when they reach 90% of their limit.
- `Limit Permissions`: An array of permission settings that define the entity limits for different groups of players.

### Limit Permission Configuration

Each object in the `Limit Permissions` array has the following properties:

- `Permission`: The permission string used to apply this limit setting. Players with this permission will be subject to the defined limits.
- `Priority`: An integer defining the priority of this limit setting. Higher numbers take precedence over lower numbers.
- `Entity Limits`: A dictionary where each key represents the name of an entity, and the value is the maximum number of instances a player with this permission can have. You can specify either the short or long name of the prefab for each entity. For example, for a windmill, the short name would be `electric.windmill.small`, and the long name would be `assets/prefabs/deployable/windmill/windmillsmall/electric.windmill.small.prefab`. The plugin recognizes both naming conventions, allowing you to use whichever is more convenient or consistent with your server's setup.

## Installation

1. Place the `MjEntityLimiter.cs` file in the `oxide/plugins` directory of your Rust server.
2. Restart the server or use the `oxide.reload` command to load the plugin.
3. Configure the plugin by editing the generated configuration file in `oxide/config/MjEntityLimiter.json` or `carbon/config/MjEntityLimter.json`.

## Usage

Once installed and configured, the plugin will automatically start enforcing the defined entity limits. Players will be prevented from building or spawning entities beyond their limits and will receive chat messages warning them as they approach their limits.

---

*This README.md is a general guide. For specific details or troubleshooting, refer to the plugin's source code or contact the plugin's author.*