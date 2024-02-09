using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using static Oxide.Core.Libraries.WebRequests;

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
            if (args.Length == 0)
            {
                Player.Message(player, GetMessage(MessageTypes.PleaseWait), config.Prefix);
                var queue = new WebRequestQueue(this, webrequest, (responses) =>
                {
                    
                });

                foreach (var server in config.Servers)
                {
                    foreach (var voteServer in server.Value)
                    {
                        var voteServerConfig = config.VoteServers[voteServer.Key];
                        var url = FormatURL(voteServerConfig.StatusURL, player, voteServer.Value.Item2);
                        var request = new WebRequest(url, (int statusCode, string response) => { }, this);
                        queue.Add(voteServer.Key, request);
                    }
                }
                queue.Start();
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
            private Dictionary<string, string> _responses = new Dictionary<string, string>();
            private int _completed;
            private Action<Dictionary<string, string>> _finishedcallback;

            public WebRequestQueue(Plugin plugin, WebRequests webRequests, Action<Dictionary<string, string>> callback)
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
                    _responses[id] = data;
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

    }
}