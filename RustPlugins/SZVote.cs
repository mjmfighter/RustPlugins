using Carbon.Extensions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using static Oxide.Core.Libraries.WebRequests;

namespace Oxide.Plugins
{
    [Info("SZVote", "mjmfighter", "1.0.0")]
    [Description("Get rewards for voting for your favorite server")]

    public class SZVote : RustPlugin
    {

        #region Variables

        private VoteConfiguration config;

        private PluginData pluginData;
        #endregion

        private void Init()
        {
            pluginData = PluginData.Load();
        }

        private void Unload()
        {
            
        }

        #region Commands

        [ChatCommand("rewardlist")]
        private void RewardListCommand(BasePlayer player, string command, string[] args)
        {
            var rewards = string.Join("\n", config.Rewards.ConvertAll(x => x.Item3));
            Player.Message(player, GetMessage(MessageTypes.RewardList, rewards), config.Prefix);
        }

        [ChatCommand("vote")]
        private void VoteCommand(BasePlayer player, string command, string[] args)
        {
            var servers = "";
            foreach (var server in config.Servers)
            {
                var serverName = server.Key;
                foreach (var voteServer in server.Value)
                {
                    var voteServerName = voteServer.Key;
                    var voteServerConfig = config.VoteServers[voteServer.Key];
                    var url = string.Format(voteServerConfig.ServerURL, voteServer.Value.Item1);
                    servers += $"{serverName}: {url}\n";
                }
            }
            Player.Message(player, GetMessage(MessageTypes.VoteList, servers), config.Prefix);
        }

        [ChatCommand("claim")]
        private void ClaimCommand(BasePlayer player, string command, string[] args)
        {
            Player.Message(player, GetMessage(MessageTypes.PleaseWait), config.Prefix);
            var queue = new WebRequestQueue(this, webrequest, (responses) =>
            {
                var votesBefore = pluginData[player.UserIDString].TotalVotes;
                var claimResponses = new Dictionary<string, ClaimResponse>();
                // Phrase all the responses.
                foreach (var response in responses)
                {
                    var server = response.Key.Split(':')[0];
                    var site = response.Key.Split(':')[1];
                    var voteServerConfig = config.VoteServers[site];
                    var claim = PhraseClaimVoteResponse(response.Value.Item1, response.Value.Item2, player, server, site);
                    claimResponses.Add(response.Key, claim);
                }
                var votesAfter = pluginData[player.UserIDString].TotalVotes;
                // Give the player all the rewards they would have earned
                var newRewards = new List<string>();
                for(var i = votesBefore; i < votesAfter; i++)
                {
                    newRewards.AddRange(GetRewards(i));
                }
                GiveRewards(player, newRewards.ToArray());

                // TODO: Print the appropriate message.  If all claimed, print everything is claimed.  If some are not claimed, print that some are not claimed.  If there was an error in any of them, also include that at the end
                if (claimResponses.Values.All(r => r == ClaimResponse.Claimed))
                {
                    Player.Message(player, GetMessage(MessageTypes.ClaimReward), config.Prefix);
                }
                else if (claimResponses.Values.Any(r => r == ClaimResponse.Error))
                {
                    Player.Message(player, "There was an error claiming your rewards, please try again later", config.Prefix);
                }
                else
                {
                    Player.Message(player, "Some of your rewards were not claimed, please try again later", config.Prefix);
                }
            });

            foreach (var server in config.Servers)
            {
                foreach (var voteServer in server.Value)
                {
                    var voteServerConfig = config.VoteServers[voteServer.Key];
                    var url = FormatURL(voteServerConfig.StatusURL, player, voteServer.Value.Item2);
                    var request = new WebRequest(url, (int statusCode, string response) => { }, this);
                    queue.Add($"{server.Key}:${voteServer.Key}", request);
                }
            }
            queue.Start();
        }

        private void GiveRewards(BasePlayer player, string[] newRewards)
        {
            // Check to see if player has any unclaimed rewards and try to give them
            var unclaimedRewards = pluginData[player.UserIDString].UnclaimedRewards;
            for (var i = unclaimedRewards.Count - 1; i >= 0; i--)
            {
                var reward = unclaimedRewards[i];
                if (HasFreeSlots(player))
                {
                    player.SendConsoleCommand(reward.Replace("{playerid}", player.UserIDString));
                    unclaimedRewards.RemoveAt(i);
                }
            }

            // Now try to give the new rewards
            foreach (var reward in newRewards)
            {
                if (HasFreeSlots(player))
                {
                    player.SendConsoleCommand(reward.Replace("{playerid}", player.UserIDString));
                }
                else
                {
                    pluginData[player.UserIDString].UnclaimedRewards.Add(reward);
                }
            }
        }

        #endregion

        #region Data

        protected class PluginData : Dictionary<string, PlayerVoteData>
        {
            public static PluginData Load()
            {
                return Interface.Oxide.DataFileSystem.ReadObject<PluginData>("SZVote");
            }

            public void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject("SZVote", this);
            }
        }

        protected class PlayerVoteData
        {
            // Store number of votes per server
            public Dictionary<string, int> Votes = new Dictionary<string, int>();

            // Store any unclaimed rewards that are not able to be given to the player
            public List<string> UnclaimedRewards = new List<string>();

            public int TotalVotes
            {
                get
                {
                    return Votes.Values.Sum();
                }
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
            public string ClaimURL = "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={apikey}&steamid={steamid}";

            [JsonProperty(PropertyName = "API Status URL")]
            public string StatusURL = "http://rust-servers.net/api/?object=votes&element=claim&key={apikey}&steamid={steamid}";

            [JsonProperty(PropertyName = "Server Vote Url")]
            public string ServerURL = "http://rust-servers.net/server/{0}/";
        }

        private class VoteConfiguration
        {
            [JsonProperty(PropertyName = "Enable Debug Logging")]
            public bool Debug = false;

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "<color=#e67e22>[Vote]</color> ";

            [JsonProperty(PropertyName = "Required Free Slots To Give Items")]
            public int RequiredFreeSlots = 1;

            [JsonProperty(PropertyName = "Discord")]
            public DiscordConfiguration Discord = new DiscordConfiguration();

            [JsonProperty(PropertyName = "Rewards")]
            public List<(string, string, string)> Rewards = new List<(string, string, string)>
            {
                ("@", "giveto {playerid} supply.signal 1", "Every Vote: One Supply Signal"),
                ("@3", "giveto {playerid} supply.signal 1", "Every 3 Votes: One Supply Signal"),
                ("5", "giveto {playerid} testgen 1", "Fith Vote: One Test Generator")
            };

            public Dictionary<string, Dictionary<string, (string, string)>> Servers = new Dictionary<string, Dictionary<string, (string, string)>>
            {
                ["Server1"] = 
                {
                    ["rust-servers.net"] = ("Server ID", "API Key")
                }
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
            RewardList,
            VoteList,
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MessageTypes.ClaimStatus.ToString()] = "Checked {0}, Status: {1}",
                [MessageTypes.ClaimReward.ToString()] = "You have claimed your reward",
                [MessageTypes.PleaseWait.ToString()] = "Checking vote sites for your rewards, please wait...",
                [MessageTypes.RewardList.ToString()] = "The following rewards are given for voting:\n{0}",
                [MessageTypes.VoteList.ToString()] = "The following vote sites are available:\n{0}"
            }, this);
        }

        private string GetMessage(MessageTypes type, params object[] args)
        {
            return string.Format(lang.GetMessage(type.ToString(), this), args);
        }

        #endregion

        #region Helpers

        public enum ClaimResponse
        {
            Claimed,
            NotClaimed,
            AlreadyVoted,
            Error,
        }

        private bool HasFreeSlots(BasePlayer player)
        {
            return player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count >= config.RequiredFreeSlots;
        }

        private ClaimResponse PhraseClaimVoteResponse(int code, string response, BasePlayer player, string server, string site)
        {
            if (code == 200)
            {
                switch (response)
                {
                    case "1":
                        ConsoleDebug($"Player {player.displayName} has claimed vote for {site} on {server}");
                        UpdatePlayerData(player.UserIDString, server, 1);
                        return ClaimResponse.Claimed;
                    case "2":
                        ConsoleDebug($"Player {player.displayName} has already claimed vote for {site} on {server}");
                        return ClaimResponse.AlreadyVoted;
                    default:
                        ConsoleDebug($"Player {player.displayName} has not voted for {site} on {server}");
                        return ClaimResponse.NotClaimed;
                }
            }
            else
            {
                // Log error as a warning to the console
                ConsoleError($"Failed to check vote status for {player.displayName} on {site} with code {code}");
                ConsoleDebug($"Response: {response}");
                return ClaimResponse.Error;
            }
        }

        private string[] GetRewards(int votes)
        {
            var rewards = new List<string>();
            foreach (var reward in config.Rewards)
            {
                var when = reward.Item1;
                int number;
                if (when.StartsWith("@") && int.TryParse(when.Substring(1), out number))
                {
                    if (votes % number == 0)
                    {
                        rewards.Add(reward.Item2);
                    }
                }
                else if (when.StartsWith("@"))
                {
                    rewards.Add(reward.Item2);
                }
                else if (when.StartsWith(">=") && int.TryParse(when.Substring(2), out number))
                {
                    if (votes >= number)
                    {
                        rewards.Add(reward.Item2);
                    }
                }
                else if (when.StartsWith(">") && int.TryParse(when.Substring(1), out number))
                {
                    if (votes > number)
                    {
                        rewards.Add(reward.Item2);
                    }
                }
                else if (when.StartsWith("<=") && int.TryParse(when.Substring(2), out number))
                {
                    if (votes <= number)
                    {
                        rewards.Add(reward.Item2);
                    }
                }
                else if (when.StartsWith("<") && int.TryParse(when.Substring(1), out number))
                {
                    if (votes < number)
                    {
                        rewards.Add(reward.Item2);
                    }
                }
                else if (when.ToInt() == votes)
                {
                    rewards.Add(reward.Item2);
                }
            }
            return rewards.ToArray();
        }

        private void UpdatePlayerData(string playerid, string server, int votes)
        {
            if (!pluginData.ContainsKey(playerid))
            {
                pluginData[playerid] = new PlayerVoteData();
            }
            if (!pluginData[playerid].Votes.ContainsKey(server))
            {
                pluginData[playerid].Votes[server] = 0;
            }
            pluginData[playerid].Votes[server] += votes;
            pluginData.Save();
        }

        private string FormatURL(string url, BasePlayer player, string apikey)
        {
            return url.Replace("{apikey}", apikey).Replace("{steamid}", player.UserIDString).Replace("{playerusername}", player.displayName);
        }

        // WebRequestQueue that will allow you to queue multiple web requests, and receive a callback when all requests are completed
        private class WebRequestQueue
        {
            private Plugin _plugin;
            private WebRequests _webrequest;

            private List<WebRequest> _requests = new List<WebRequest>();
            private Dictionary<string, (int, string)> _responses = new Dictionary<string, (int, string)>();
            private int _completed;
            private Action<Dictionary<string, (int, string)>> _finishedcallback;

            public WebRequestQueue(Plugin plugin, WebRequests webRequests, Action<Dictionary<string, (int, string)>> callback)
            {
                _plugin = plugin;
                _webrequest = webRequests;
                _finishedcallback = callback;
            }

            public void Add(string id, WebRequest request)
            {
                // Adjust the request so that it will call our callback first, then the original callback
                var originalcallback = request.SuccessCallback;
                request.SuccessCallback = (code, data) =>
                {
                    originalcallback.Invoke(code, data);
                    _responses[id] = (code, data);
                    OnRequestComplete();
                };
                _requests.Add(request);
            }

            private void OnRequestComplete()
            {
                _completed++;
                if (_completed == _requests.Count)
                {
                    _finishedcallback.Invoke(_responses);
                }
            }

            public void Start()
            {
                _completed = 0;
                foreach (var request in _requests)
                {
                    request.Start();
                }
            }
        }

        #endregion

        #region Debug

        protected void ConsoleLog(object message)
        {
            Puts(message.ToString());
        }

        protected void ConsoleError(object message)
        {
            PrintError(message.ToString());
        }

        protected void ConsoleDebug(object message)
        {
            if (config.Debug)
                Puts(message.ToString());
        }
        
        #endregion

    }
}