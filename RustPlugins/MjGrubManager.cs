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
    [Info("MjGrubManager", "mjmfighter", "1.0.0")]
    [Description("Manages grubbing features")]
    public class MjGrubManager : RustPlugin
    {
        private Dictionary<NetworkableId, BasePlayer> lootLocks = new Dictionary<NetworkableId, BasePlayer>();
        private const float lockDuration = 60f; // Lock duration in seconds

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is NPCPlayer)
            {
                var npc = entity as NPCPlayer;
                BasePlayer killer = null;

                if (info != null && info.InitiatorPlayer != null)
                {
                    killer = info.InitiatorPlayer;
                    LockLoot(npc, killer);
                }
            }
        }

        void LockLoot(NPCPlayer npc, BasePlayer killer)
        {
            var lootContainer = npc.inventory.loot;
            if (lootContainer != null)
            {
                lootLocks.Add(lootContainer.entitySource.net.ID, killer);
                timer.Once(lockDuration, () => UnlockLoot(lootContainer.entitySource.net.ID));
            }
        }

        void UnlockLoot(NetworkableId containerId)
        {
            if (lootLocks.ContainsKey(containerId))
            {
                lootLocks.Remove(containerId);
            }
        }

        object CanLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (lootLocks.TryGetValue(entity.net.ID, out var locker) && locker != player)
            {
                return false; // Prevent looting if the player is not the killer
            }
            return null;
        }
    }
}