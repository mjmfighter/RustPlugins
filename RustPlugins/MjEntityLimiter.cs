﻿using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Oxide.Plugins
{
  [Info("MjEntityLimter", "mjmfighter", "0.0.1")]
  [Description("Limits specified entities for players")]
  public class MjEntityLimiter : RustPlugin
  {
    #region Variables

    private LimitsConfig config = new LimitsConfig();
    private PlayerEntityLimits playerEntityLimitsCache = new PlayerEntityLimits();
    private ExpiringCache<string, LimitPermission> permissionCache;

    private Coroutine initialLookup = null;

    #endregion

    #region Hooks

    private void Init()
    {
      foreach (var value in config.limitPermissions)
      {
        if (!string.IsNullOrEmpty(value.permission) && !permission.PermissionExists(value.permission))
        {
          permission.RegisterPermission(value.permission, this);
        }
      }
      this.permissionCache = new ExpiringCache<string, LimitPermission>(TimeSpan.FromMinutes(5));
      timer.Every(3f * 60, () => { permissionCache.CleanupExpiredItems(); });
    }

    private void OnServerInitialized()
    {
      timer.Once(3f, () =>
      {
        initialLookup = ServerMgr.Instance.StartCoroutine(InitialLookup());
      });
    }

    private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
    {
      var player = planner.GetOwnerPlayer();
      if (player == null)
      {
        return null;
      }

      return CheckBuild(player, prefab.fullName);
    }

    private void OnEntitySpawned(BaseEntity entity)
    {
      UpdateEntity(entity, false);
    }

    private void OnEntityKill(BaseEntity entity)
    {
      UpdateEntity(entity, true);
    }

    #endregion

    #region Methods

    private IEnumerator InitialLookup()
    {
      yield return new WaitForEndOfFrame();
      Puts($"Processing entities.  Server may lag while this is happening...");
      var entities = BaseNetworkable.serverEntities.OfType<BaseEntity>().ToArray();
      yield return new WaitForSecondsRealtime(1);
      var total = entities.Length;
      var processed = 0;

      foreach (var entity in entities)
      {
        if (!entity.IsValid() || !entity.OwnerID.IsSteamId())
        {
          continue;
        }

        // Fetch or create the playerData for the given OwnerID
        if (!playerEntityLimitsCache.TryGetValue(entity.OwnerID, out var playerData))
        {
          playerData = new EntityDictionary();
          playerEntityLimitsCache[entity.OwnerID] = playerData;
        }

        // Fetch or create the LimitEntityData for the given PrefabName
        if (!playerData.TryGetValue(entity.PrefabName, out var limitEntityData))
        {
          limitEntityData = new LimitEntityData();
          playerData[entity.PrefabName] = limitEntityData;
        }

        // Add the entity to the LimitEntityData
        limitEntityData.entities.Add(entity);

        if (config.debug && processed > 0 && processed % 1000 == 0)
        {
          Puts($"Entity processing: {processed}/{total}");
          yield return new WaitForEndOfFrame();
        }

        processed++;
      }

      yield return new WaitForSecondsRealtime(1);
      Puts($"Finished processing {total} entities");
    }

    private object CheckBuild(BasePlayer player, string prefabFullName)
    {
      var perm = GetPlayerLimits(player.UserIDString);
      if (perm == null)
      {
        return null;
      }

      // Get the limit for the given prefab (for both long and short names), if it doesn't exist default to -1
      var limit = perm.GetLimit(prefabFullName);
      if (limit >= 0)
      {
        var playerData = playerEntityLimitsCache.GetByPlayer(player);
        var limitEntityData = playerData.Get(prefabFullName);
        var entityCount = limitEntityData.count;
        if (entityCount >= limit)
        {
          SendMessage(player, MessageLimitType.Limit, entityCount);
          return false;
        }

        var warnLimit = limit * (100 - config.warnPercentage) / 100;
        if (entityCount >= warnLimit)
        {
          SendMessage(player, MessageLimitType.Warning, entityCount + 1, limit - entityCount - 1);
        }
      }

      return null;
    }

    // Update the entity data for the given player
    private void UpdateEntity(BaseEntity entity, bool destroyed)
    {
      if (!entity.OwnerID.IsSteamId())
      {
        return;
      }

      var owner = entity.OwnerID;
      var prefabName = entity.PrefabName;

      NextTick(() =>
      {
        if (!destroyed && !entity.IsValid())
        {
          return;
        }

        var playerData = playerEntityLimitsCache.Get(owner);
        var limitEntityData = playerData.Get(prefabName);

        if (destroyed)
        {
          limitEntityData.entities.Remove(entity);
        }
        else
        {
          limitEntityData.entities.Add(entity);
        }

      });
    }

    private static string GetShortname(string original)
    {
      var index = original.LastIndexOf("/", StringComparison.Ordinal) + 1;
      var name = original.Substring(index);
      return name.Replace(".prefab", string.Empty);
    }

    // Check to see if the original string is equal to either the short or long name
    private bool EqualShortLongName(string original, string prefabShortName, string prefabFullName)
    {
      return original.Equals(prefabShortName, StringComparison.OrdinalIgnoreCase) ||
             original.Equals(prefabFullName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Configuration

    private class LimitPermission
    {
      [JsonProperty(PropertyName = "Permission")]
      public string permission;

      [JsonProperty(PropertyName = "Priority")]
      public int priority;

      [JsonProperty(PropertyName = "Entity Limits")]
      public Dictionary<string, int> limits = new Dictionary<string, int>();

      public int GetLimit(string prefabName)
      {
        if (limits.TryGetValue(prefabName, out var limit))
        {
          return limit;
        }

        var shortName = GetShortname(prefabName);
        if (limits.TryGetValue(shortName, out limit))
        {
          return limit;
        }

        return -1;
      }
    }

    private class LimitsConfig
    {
      [JsonProperty(PropertyName = "Enable more debugging messages")]
      public bool debug = false;

      [JsonProperty(PropertyName = "Chat Prefix")]
      public string chatPrefix = "<color=red>[MjEntityLimiter]</color> ";

      [JsonProperty(PropertyName = "Warn about limits below x percent")]
      public int warnPercentage = 10;

      [JsonProperty(PropertyName = "Limit Permissions")]
      public LimitPermission[] limitPermissions = new LimitPermission[]
      {
                new LimitPermission
                {
                    permission = nameof(MjEntityLimiter) + ".default",
                    priority = 0,
                    limits = new Dictionary<string, int>
                    {
                        {"electric.windmill.small", 0}
                    }
                }
      };
    }

    protected override void LoadConfig()
    {
      base.LoadConfig();

      try
      {
        config = Config.ReadObject<LimitsConfig>();
        if (config == null)
        {
          LoadDefaultConfig();
        }
      }
      catch
      {
        PrintError("Configuration file is corrupt.  Please check your config file's syntax!");

        LoadDefaultConfig();
        return;
      }
      SaveConfig();
    }

    protected override void LoadDefaultConfig()
    {
      config = new LimitsConfig();
    }

    protected override void SaveConfig()
    {
      Config.WriteObject(config);
    }

    #endregion

    #region Messages

    private enum MessageLimitType
    {
      Warning,
      Limit,
    }

    protected override void LoadDefaultMessages()
    {
      lang.RegisterMessages(new Dictionary<string, string>
      {
        {MessageLimitType.Warning.ToString(), "You used {0} of that entity type, you have {1} remaining..." },
        {MessageLimitType.Limit.ToString(), "You have reached your limit of that entity type!  You have used: {0}" }
      }, this, "en");
    }

    private void SendMessage(object receiver, MessageLimitType type, params object[] args)
    {
      var userId = (receiver as BasePlayer)?.UserIDString;
      var message = String.Format(lang.GetMessage(type.ToString(), this, null), args);
      SendMessage(receiver, message);
    }

    private void SendMessage(object receiver, string message)
    {
      if (receiver == null)
      {
        Puts(message);
        return;
      }

      var console = receiver as ConsoleSystem.Arg;
      if (console != null)
      {
        SendReply(console, message);
        return;
      }

      var player = receiver as BasePlayer;
      if (player != null)
      {
        Player.Message(player, message, config.chatPrefix);
        return;
      }
    }

    #endregion

    #region Plugin Data

    private class LimitEntityData
    {
      public int count => entities.Count;
      public HashSet<BaseEntity> entities = new HashSet<BaseEntity>();
    }

    private class EntityDictionary : Dictionary<string, LimitEntityData>
    {
      public LimitEntityData Get(string prefabName)
      {
        if (!ContainsKey(prefabName))
        {
          this[prefabName] = new LimitEntityData();
        }
        return this[prefabName];
      }
    }

    private class PlayerEntityLimits : Dictionary<ulong, EntityDictionary>
    {
      public EntityDictionary GetByPlayer(BasePlayer player)
      {
        return Get(player.userID);
      }

      public EntityDictionary Get(ulong id)
      {
        if (!ContainsKey(id))
        {
          this[id] = new EntityDictionary();
        }
        return this[id];
      }
    }

    #endregion

    #region Permissions

    private LimitPermission GetPlayerLimits(string playerID)
    {
      if (permissionCache.TryGetValue(playerID, out var lastPermission))
      {
        return lastPermission;
      }

      var lastPriority = -1;
      foreach (var perm in config.limitPermissions)
      {
        if (perm.priority > lastPriority && permission.UserHasPermission(playerID, perm.permission))
        {
          lastPriority = perm.priority;
          lastPermission = perm;
        }
      }

      if (lastPermission != null)
      {
        permissionCache.Set(playerID, lastPermission);
      }

      return lastPermission;
    }

    public class ExpiringCache<TKey, TValue>
    {
      private class CacheItem
      {
        public TValue Value { get; set; }
        public DateTime Expiry { get; set; }
      }

      private Dictionary<TKey, CacheItem> _cache = new Dictionary<TKey, CacheItem>();
      private TimeSpan _expiryDuration;

      public ExpiringCache(TimeSpan expiryDuration)
      {
        _expiryDuration = expiryDuration;
        //_cleanupTimer = new Timer(CleanupExpiredItems, null, cleanupInterval, cleanupInterval);
      }

      public void Set(TKey key, TValue value)
      {
        lock (_cache)
        {
          _cache[key] = new CacheItem
          {
            Value = value,
            Expiry = DateTime.UtcNow.Add(_expiryDuration)
          };
        }
      }

      public bool TryGetValue(TKey key, out TValue value)
      {
        lock (_cache)
        {
          if (_cache.TryGetValue(key, out CacheItem item) && item.Expiry > DateTime.UtcNow)
          {
            value = item.Value;
            return true;
          }
        }

        value = default;
        return false;
      }

      public void CleanupExpiredItems()
      {
        lock (_cache)
        {
          var keysToRemove = new List<TKey>();
          foreach (var pair in _cache)
          {
            if (pair.Value.Expiry <= DateTime.UtcNow)
            {
              keysToRemove.Add(pair.Key);
            }
          }

          foreach (var key in keysToRemove)
          {
            _cache.Remove(key);
          }
        }
      }
    }

    #endregion
  }
}