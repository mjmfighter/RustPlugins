using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MjVote", "mjmfighter", "1.0.0")]
    [Description("Get rewards for voting for your favorite server")]

    public class MjVote : RustPlugin
    {

        #region Variables

        private VoteConfiguration config;
        #endregion

        private void Init()
        {
            
        }

        private void Unload()
        {
            
        }

        #region Commands

        [ChatCommand("rewardlist")]
        private void RewardListCommand(BasePlayer player, string command, string[] args)
        {
            foreach (var reward in config.Rewards)
            {
                PrintToChat(player, $"{config.Prefix} {reward.Item1} - {reward.Item3}");
            }
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            config = new VoteConfiguration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<VoteConfiguration>();
            }
            catch
            {
                PrintError("Failed to load configuration file, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class DiscordConfiguration
        {
            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string WebhookUrl = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty(PropertyName = "Enable Discord Logging")]
            public bool Enabled = false;

        }

        private class VoteServerConfiguration
        {
            [JsonProperty(PropertyName = "API Claim URL")]
            public string ClaimURL = "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}";

            [JsonProperty(PropertyName = "API Status URL")]
            public string StatusURL = "http://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}";

            [JsonProperty(PropertyName = "Server Vote Url")]
            public string ServerURL = "http://rust-servers.net/server/{0}/";
        }

        private class VoteConfiguration
        {
            [JsonProperty(PropertyName = "Enable Debug Logging")]
            public bool Debug = false;

            public string Prefix = "<color=#e67e22>[Vote]</color> ";

            [JsonProperty(PropertyName = "Discord")]
            public DiscordConfiguration Discord = new DiscordConfiguration();

            [JsonProperty(PropertyName = "Rewards")]
            public List<(string, string, string)> Rewards = new List<(string, string, string)>
            {
                ("@", "giveto {playerid} supply.signal 1", "Every Vote: One Supply Signal"),
                ("@3", "giveto {playerid} supply.signal 1", "Every 3 Votes: One Supply Signal"),
                ("5", "giveto {playerid} testgen 1", "Fith Vote: One Test Generator")
            };

            public Dictionary<string, (string, string)> Servers = new Dictionary<string, (string, string)>
            {
                ["rust-servers.net"] = ("Server ID", "API Key")
            };

            [JsonProperty(PropertyName = "Vote Servers Configuration")]
            public Dictionary<string, VoteServerConfiguration> VoteServers = new Dictionary<string, VoteServerConfiguration>
            {
                ["rust-servers.net"] = new VoteServerConfiguration()
            };
        }

        #endregion

        #region Language

        private enum MessageTypes
        {
            ClaimStatus,
            ClaimReward,
            PleaseWait,

        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MessageTypes.ClaimStatus.ToString()] = "You have {0} votes and {1} rewards available",
                [MessageTypes.ClaimReward.ToString()] = "You have claimed your reward",
                [MessageTypes.PleaseWait.ToString()] = "Please wait a moment before claiming your reward"
            }, this);
        }

        #endregion

    }
}