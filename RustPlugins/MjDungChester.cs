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
    [Info("MjDungChester", "mjmfighter", "1.0.0")]
    [Description("Automatically stores dung in near by chests")]
    public class MjDungChester : RustPlugin
    {
        #region Fields

        private MjDungChesterConfig config;

        #endregion

        #region Hooks

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item.info.shortname != "horsedung" || item.GetOwnerPlayer() != null)
            {
                return;
            }
            // Find the nearest chest if it exists
            List<BoxStorage> chests = new List<BoxStorage>();
            Vis.Entities<BoxStorage>(entity.transform.position, config.maxDistance, chests);
            var chestsFiltered = chests.Where(x => x.inventory.CanAccept(item) && !x.ShortPrefabName.Contains("fridge"));
            NextTick(() => {
                // Try to move the items into the first available chest
                foreach (var chest in chestsFiltered)
                {
                    if (item.MoveToContainer(chest.inventory))
                    {
                        Puts($"Item {item.info.displayName.english} was moved to {chest.ShortPrefabName} at location {chest.transform.position}!");
                        return;
                    }
                }
                // If no chests are found, delete the dung
                if (config.deleteDung)
                {
                    Puts($"Item {item.info.displayName.english} deleted!");
                    item.Remove();
                }
            });
        }

        #endregion

        #region Configuration

        private class MjDungChesterConfig
        {
            [JsonProperty(PropertyName = "Max Distance to look for chests")]
            public int maxDistance = 2;

            [JsonProperty(PropertyName = "Delete dung if no chests are found")]
            public bool deleteDung = false;
        }

        protected override void LoadDefaultConfig()
        {
            config = new MjDungChesterConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<MjDungChesterConfig>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}
