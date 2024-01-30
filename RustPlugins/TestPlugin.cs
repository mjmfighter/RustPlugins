// Basic Oxide Rust plugin template
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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

        void OnItemDropped(Item item, BaseEntity entity)
        {
            // Find the nearest chest if it exists
            List<BoxStorage> chests = new List<BoxStorage>();
            Vis.Entities<BoxStorage>(entity.transform.position, 10f, chests);
            BoxStorage chest = chests.FirstOrDefault();
            if (chest != null)
            {
                // Check if the chest is full
                if (chest.inventory.CanAccept(item))
                {
                    // Add the item to the chest
                    chest.inventory.AddItem(item.info, item.amount);
                    item.Remove();
                    Puts($"Item {item.info.displayName.english} was moved to {chest.ShortPrefabName} at location {chest.transform.position}!");
                    return;
                }
                Puts($"Item {item.info.displayName.english} dropped by {entity.PrefabName} at {entity.transform.position}!");
                
            }
            Puts($"Item {item.info.displayName.english} dropped by {entity.PrefabName} at {entity.transform.position}!");
            var owner = entity.OwnerID;
            Puts($"Item Name: {item.info.shortname}\nItem Owner: {owner}");
            return;
        }
    }
}