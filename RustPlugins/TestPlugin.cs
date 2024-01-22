// Basic Oxide Rust plugin template
using Oxide.Core.Plugins;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("TestPlugin", "YourName", "1.0.0")]
    public class TestPlugin : RustPlugin
    {
        // Called when the plugin is loading
        void Loaded()
        {
            Puts("TestPlugin loaded!");
        }

        // Called when the plugin is unloaded
        void Unload()
        {
            Puts("TestPlugin unloaded!");
        }

        // Called when the server is initialized
        void OnServerInitialized()
        {
            Puts("Server initialized!");
        }
    }
}