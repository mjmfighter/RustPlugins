using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static BaseProjectile;

namespace Oxide.Plugins
{
    [Info("MjSleeperRemover", "mjmfighter", "1.0.1")]
    [Description("Removes sleepers from the server and saves their inventory for when they return")]
    public class MjSleeperRemover : RustPlugin
    {
        #region Variables

        [PluginReference]
        private Plugin SkillTree;

        private HashSet<string> skillTreeKills = new HashSet<string>();

        private SleeperConfiguration config;
        private PluginData sleeperInventories = new PluginData();

        #endregion

        #region Hooks

        private void OnNewSave(string filename)
        {
            if (config.wipeOnUpgradeOrChange)
            {
                sleeperInventories.Clear();
                SaveData();
            }
        }

        private void Init()
        {
            LoadData();
        }

        private void OnServerInitialized()
        {
            // Register sleeperRemovalTimes permissions
            foreach (var sleeperRemoval in config.sleeperRemovalTimes)
            {
                permission.RegisterPermission(sleeperRemoval.Key, this);
            }

            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (BasePlayer.activePlayerList.Contains(player))
                {
                    continue;
                }

                if (!player.IsDestroyed)
                {
                    SerializedPlayer playerData = new SerializedPlayer(player);
                    sleeperInventories[player.UserIDString] = playerData;

                    KillPlayer(player);
                }
            }
        }

        private void OnServerSave()
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sleepersToRemove = new List<string>();
            foreach (var sleeper in sleeperInventories)
            {
                var timeDifference = currentTime - sleeper.Value.lastSeen;
                var sleeperRemovalTime = config.sleeperRemovalTime;
                foreach (var sleeperRemoval in config.sleeperRemovalTimes)
                {
                    if (permission.UserHasPermission(sleeper.Key, sleeperRemoval.Key))
                    {
                        if (sleeperRemoval.Value >= sleeperRemovalTime)
                        {
                            sleeperRemovalTime = sleeperRemoval.Value;
                        }
                    }
                }
                if (timeDifference >= sleeperRemovalTime * 3600)
                {
                    sleepersToRemove.Add(sleeper.Key);
                }
            }
            foreach (var sleeper in sleepersToRemove)
            {
                sleeperInventories.Remove(sleeper);
            }

            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!sleeperInventories.ContainsKey(player.UserIDString))
                return;
                
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            
            NextTick(() =>
            {
                if (player == null || !player.IsConnected)
                    return;
                
                if (player.IsDead())
                {
                    player.Respawn();
                }
            });
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (!sleeperInventories.ContainsKey(player.UserIDString))
                return;

            var playerData = sleeperInventories[player.UserIDString];
            playerData.RestoreItemsToPlayer(player);
            sleeperInventories.Remove(player.UserIDString);
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            if (!sleeperInventories.ContainsKey(player.UserIDString))
                return null;

            var playerData = sleeperInventories[player.UserIDString];
            
            return new BasePlayer.SpawnPoint { pos = playerData.position };
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.IsDestroyed)
                return;
            
            var playerData = new SerializedPlayer(player);
            sleeperInventories[player.UserIDString] = playerData;

            if (SkillTree != null)
                skillTreeKills.Add(player.UserIDString);

            KillPlayer(player);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            // If short prefab name is player_corpse or player_corpse_new, remove it
            if (entity.ShortPrefabName == "player_corpse" || entity.ShortPrefabName == "player_corpse_new")
                entity.Kill();
        }

        private object STOnLoseXP(BasePlayer player)
        {
            if (skillTreeKills.Contains(player.UserIDString))
            {
                skillTreeKills.Remove(player.UserIDString);
                return true;
            }
            return null;
        }

        #endregion

        #region Commands

        #endregion

        #region Methods

        protected override void LoadDefaultConfig()
        {
            config = new SleeperConfiguration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<SleeperConfiguration>();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected void KillPlayer(BasePlayer player)
        {
            if (player.IsDead())
                return;
            if (SkillTree != null)
                skillTreeKills.Add(player.UserIDString);
            
            player.inventory.Strip();
            player.Die();
        }

        protected void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("MjSleeperRemover", sleeperInventories);
        }

        protected void LoadData()
        {
            sleeperInventories = Interface.Oxide.DataFileSystem.ReadObject<PluginData>("MjSleeperRemover");
        }

        #endregion

        #region Config

        private class SleeperConfiguration {
            [JsonProperty(PropertyName = "Wipe on Upgrade or Change (true/false) (Usually a forced wipe)")]
            public bool wipeOnUpgradeOrChange = true;

            [JsonProperty(PropertyName = "Sleeper Removal Time (hours)")]
            public int sleeperRemovalTime = 300;

            [JsonProperty(PropertyName = "Sleeper Permission Override Times (hours)")]
            public Dictionary<string, int> sleeperRemovalTimes = new Dictionary<string, int>()
            {
                {"mjsleeperremover.vip", 600},
                {"mjsleeperremover.admin", -1}
            };
        }

        #endregion

        #region Data

        private class PluginData : Dictionary<string, SerializedPlayer> { }

        private class SerializedMagazine {
            public string ammoType = null;
            public int contents;

            public SerializedMagazine() { }

            public SerializedMagazine(Magazine magazine) {
                ammoType = magazine.ammoType.shortname;
                contents = magazine.contents;
            }

            public void InsertMagazine(BaseProjectile weapon) {
                if (ammoType == null)
                    return;
                var ammoTypeDef = ItemManager.FindItemDefinition(ammoType);
                weapon.primaryMagazine.ammoType = ammoTypeDef;
                weapon.primaryMagazine.contents = contents;
            }
        }

        private class SerializedItem {
            public string name;
            public int amount;
            public ulong skinId;
            public float condition;
            public SerializedMagazine magazine;

            public SerializedItemContainer contents;

            public SerializedItem() { }

            public SerializedItem(Item item) {
                name = item.info.shortname;
                amount = item.amount;
                skinId = item.skin;
                condition = item.condition;
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    magazine = new SerializedMagazine(weapon.primaryMagazine);
                }
                contents = item.contents == null ? new SerializedItemContainer() : new SerializedItemContainer(item.contents);
            }

            public Item ToItem() {
                var item = ItemManager.CreateByName(name, amount, skinId);
                item.condition = condition;
                if (magazine != null && item.GetHeldEntity() is BaseProjectile) {
                    magazine.InsertMagazine(item.GetHeldEntity() as BaseProjectile);
                }
                foreach (var content in contents) {
                    content.Value.ToItem().MoveToContainer(item.contents, content.Key);
                }
                return item;
            }
        }

        private class SerializedItemContainer : Dictionary<int, SerializedItem> {

            public SerializedItemContainer() { }

            public SerializedItemContainer(ItemContainer container) {
                foreach (var item in container.itemList) {
                    Add(item.position, new SerializedItem(item));
                }
            }
        }

        private class SerializedPlayer {
            public SerializedItemContainer main;
            public SerializedItemContainer belt;
            public SerializedItemContainer wear;

            public long lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            public Vector3 position;

            public SerializedPlayer() { }

            public SerializedPlayer(BasePlayer player) {
                // Each dictionary is slot number to item
                main = new SerializedItemContainer(player.inventory.containerMain);
                belt = new SerializedItemContainer(player.inventory.containerBelt);
                wear = new SerializedItemContainer(player.inventory.containerWear);
                position = player.transform.position;
            }

            public void RestoreItemsToPlayer(BasePlayer player) {
                // Remove everything first
                player.inventory.Strip();
                // Restore items to player including position kept in dictionary
                foreach (var item in main) {
                    item.Value.ToItem().MoveToContainer(player.inventory.containerMain, item.Key);
                }
                foreach (var item in belt) {
                    item.Value.ToItem().MoveToContainer(player.inventory.containerBelt, item.Key);
                }
                foreach (var item in wear) {
                    item.Value.ToItem().MoveToContainer(player.inventory.containerWear, item.Key);
                }

            }
        }

        #endregion
    }
}