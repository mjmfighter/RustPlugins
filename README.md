# Rust Server Plugins Collection

## Description

This repository is a collection of C# projects tailored for Rust server plugins. It serves as a centralized hub for various plugins, each enhancing the gameplay or server management in different ways. These plugins are designed to be used with the Oxide mod framework, providing server administrators with powerful tools to manage and improve the Rust server experience.

## Getting Started

To use these plugins, you must have the Oxide mod framework installed on your Rust server. Once Oxide is installed, you can add these plugins to your server to extend its functionality.

### Installation

1. Download the desired plugins.
2. Place the `.cs` files of the plugins into the `oxide/plugins` or `carbon/plugins` directory of your Rust server.
3. Restart the server or use the `oxide.reload {PluginName}` or `c.reload {PluginName}` command to load each plugin.
4. Configure each plugin as needed by editing the generated configuration files in the `oxide/config` directory.

## Plugin List

Below is the list of available plugins in this collection. Each plugin comes with its own set of features and configuration options. For detailed information about each plugin, configuration details, and usage instructions, please refer to the individual README files linked below.

### 1. MjEntityLimiter

Limits the number of specific entities a player can spawn or build, helping maintain server performance and gameplay balance.

- **Detailed README**: [docs/MjEntityLimiter.md](docs/MjEntityLimiter.md)

---

*This README.md provides a general overview of the Rust Server Plugins Collection. For detailed information about individual plugins, please refer to the linked README files.*