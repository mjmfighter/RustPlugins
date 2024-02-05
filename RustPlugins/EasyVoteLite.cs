using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info(_PluginName, _PluginAuthor, _PluginVersion)]
    [Description(_PluginDescription)]
    public class EasyVoteLite : RustPlugin
    {

        // Plugin Metadata
        private const string _PluginName = "EasyVoteLite";
        private const string _PluginAuthor = "BippyMiester";
        private const string _PluginVersion = "3.0.13";
        private const string _PluginDescription = "#1 Rust Server Voting System";

        #region ChangeLog
        /*
         * 3.0.5
         * Removed unused references
         * Added more debug stuff
         *
         * 3.0.6
         * Fixed compliation error due to HookMethod Attribute not including a directive
         * 
         * 3.0.7
         * Removed Top-Serveurs.net from config as this website is not supported in the lite version
         * 
         * 3.0.8
         * Fixed Log Files not outputing to the right log file
         * 
         * 3.0.9
         * Added option to get rid of the please wait message when checking voting status
         * Completely rewrote how the claim and check voting status is handled. Removed not needed checks and loops.
         * Removed duplicate "Enable Debug" entry in the config
         * 
         * 3.0.10
         * Removed BasePlayerExtension
         * 
         * 3.0.11
         * Added check to claim webhook for a 2 response code to display an already voted message
         * 
         * 3.0.12
         * [+] Changed the Vote Sites API section to include a warning not to change the values within that section.
         *
         * 3.0.13
         * [-] Removed the required checked for specific sites
         */
        #endregion


        // Misc Variables
        private IEnumerator coroutine;

        private void Init()
        {
            ConsoleLog($"{_PluginName} has been initialized...");
            _config = Config.ReadObject<PluginConfig>();
            LoadMessages();
        }

        private void OnServerInitialized()
        {
            
        }

        private void Loaded()
        {
            
        }

        private void Unload()
        {
            if (coroutine != null) ServerMgr.Instance.StopCoroutine(coroutine);
        }

        #region HelperFunctions

        private void HandleClaimWebRequestCallback(int code, string response, BasePlayer player, string url, string serverName, string site)
        {
            if (code != 200)
            {
                ConsoleError($"An error occurred while trying to check the claim status of the player {player.displayName}:{player.UserIDString}");
                ConsoleWarn($"URL: {url}");
                ConsoleWarn($"HTTP Code: {code} | Response: {response} | Server Name: {serverName}");
                ConsoleWarn("This error could be due to a malformed or incorrect server token, id, or player id / username issue. Most likely its due to your server key being incorrect. Check that you server key is correct.");
                return;
            }
            
            _Debug("------------------------------");
            _Debug("Method: HandleClaimWebRequestCallback");
            _Debug($"Site: {site}");
            _Debug($"Code: {code}");
            _Debug($"Response: {response}");
            _Debug($"URL: {url}");
            _Debug($"ServerName: {serverName}");
            _Debug($"Player Name: {player.displayName}");
            _Debug($"Player SteamID: {player.UserIDString}");
            _Debug("Web Request Type: Claim");
            // Handle Verbose Debugging
            if (_config.DebugSettings[ConfigDefaultKeys.VerboseDebugEnabled].ToBool())
            {
                _Debug($"Verbose Debug Enabled, Setting Response Code to: {_config.DebugSettings[ConfigDefaultKeys.ClaimAPIRepsonseCode]}");
                response = _config.DebugSettings[ConfigDefaultKeys.ClaimAPIRepsonseCode];
            }

            // Handle Every Reward
            if (response == "1")
            {
                HandleVoteCount(player);
                player.ChatMessage(_lang("ThankYou", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], DataFile[player.UserIDString].ToString(), site));
                // Handle Discord Announcements
                if (_config.Discord[ConfigDefaultKeys.DiscordEnabled].ToBool())
                {
                    coroutine = DiscordSendMessage(_lang("DiscordWebhookMessage", player.UserIDString, player.displayName, serverName, site));
                    ServerMgr.Instance.StartCoroutine(coroutine);
                }
                // Handle Global Annonucements
                if (_config.NotificationSettings[ConfigDefaultKeys.GlobalChatAnnouncements] == "true")
                {
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        p.ChatMessage(_lang("GlobalChatAnnouncements", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix],player.displayName, DataFile[player.UserIDString].ToString()));
                    }
                }
            }
            else if (response == "2")
            {
                player.ChatMessage(_lang("AlreadyVoted", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], site));
            }
            else
            {
                player.ChatMessage(_lang("ClaimStatus", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], serverName, site, "<color=#aa0000>Not Voted</color>"));
            }
        }
        
        private void HandleStatusWebRequestCallback(int code, string response, BasePlayer player, string url, string serverName, string site)
        {
            // If the error code isn't a successful code
            if (code != 200)
            {
                ConsoleError($"An error occurred while trying to check the claim status of the player {player.displayName}:{player.UserIDString}");
                ConsoleWarn($"URL: {url}");
                ConsoleWarn($"HTTP Code: {code} | Response: {response} | Server Name: {serverName}");
                ConsoleWarn("This error could be due to a malformed or incorrect server token, id, or player id / username issue. Most likely its due to your server key being incorrect. Check that you server key is correct.");
                return;
            }
            
            _Debug("------------------------------");
            _Debug("Method: HandleClaimWebRequestCallback");
            _Debug($"Site: {site}");
            _Debug($"Code: {code}");
            _Debug($"Response: {response}");
            _Debug($"URL: {url}");
            _Debug($"ServerName: {serverName}");
            _Debug($"Player Name: {player.displayName}");
            _Debug($"Player SteamID: {player.UserIDString}");
            _Debug($"Web Request Type: Status/Check");
            // Handle Verbose Debugging
            if (_config.DebugSettings[ConfigDefaultKeys.VerboseDebugEnabled].ToBool())
            {
                _Debug($"Verbose Debug Enabled, Setting Response Code to: {_config.DebugSettings[ConfigDefaultKeys.CheckAPIResponseCode]}");
                response = _config.DebugSettings[ConfigDefaultKeys.CheckAPIResponseCode];
            }

            // Handle all other sites
            if (response == "0")
            {
                player.ChatMessage(_lang("NoRewards", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], serverName, site));
            }
            // Handle a player needs to claim a reward
            else if (response == "1")
            {
                player.ChatMessage(_lang("RememberClaim", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], site));
            }
            // Handle a player has already voted
            else if (response == "2")
            {
                player.ChatMessage(_lang("AlreadyVoted", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix], site));
            }
        }

        private void HandleVoteCount(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: HandleVoteCount");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // Grab the current vote count of the player
            int playerVoteCount = (int) DataFile[player.UserIDString];
            _Debug($"Current VoteCount: {playerVoteCount}");
            // Increase the players vote count by 1
            playerVoteCount += 1;
            DataFile[player.UserIDString] = playerVoteCount;
            SaveDataFile(DataFile);
            _Debug($"Updated Vote Count: {playerVoteCount}");
            // Handle giving rewards to the player based on cumulative boolean value
            if (_config.PluginSettings[ConfigDefaultKeys.RewardIsCumulative] == "true")
            {
                GiveCumulativeRewards(player, (int) DataFile[player.UserIDString]);
            }
            else
            {
                GiveNormalRewards(player, (int) DataFile[player.UserIDString]);
            }
        }

        private void GiveCumulativeRewards(BasePlayer player, int playerVoteCount)
        {
            _Debug("------------------------------");
            _Debug("Method: GiveCumulativeRewards");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // Handle the every reward
            GiveEveryReward(player);
            // Handle the first reward
            GiveFirstReward(player);
            // Handle all subsequent rewards
            foreach (KeyValuePair<string, List<string>> rewards in _config.Rewards)
            {
                if (rewards.Key.ToInt() <= playerVoteCount)
                {
                    GiveSubsequentReward(player, rewards.Value);
                }
            }
        }

        private void GiveNormalRewards(BasePlayer player, int playerVoteCount)
        {
            _Debug("------------------------------");
            _Debug("Method: GiveNormalRewards");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // Handle the every reward
            GiveEveryReward(player);
            // Handle the first reward
            if (playerVoteCount == 1)
            {
                GiveFirstReward(player);
            }
            // Handle all subsequent rewards
            foreach (KeyValuePair<string, List<string>> rewards in _config.Rewards)
            {
                if (rewards.Key.ToInt() == playerVoteCount)
                {
                    GiveSubsequentReward(player, rewards.Value);
                }
            }
        }

        private void GiveEveryReward(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: GiveEveryReward");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            foreach (string rewardCommand in _config.Rewards["@"])
            {
                string command = ParseRewardCommand(player, rewardCommand);
                _Debug($"Reward Command: {command}");
                rust.RunServerCommand(command);
            }
        }

        private void GiveFirstReward(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: GiveFirstReward");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            foreach (string rewardCommand in _config.Rewards["first"])
            {
                string command = ParseRewardCommand(player, rewardCommand);
                _Debug($"Reward Command: {command}");
                rust.RunServerCommand(command);
            }
        }

        private void GiveSubsequentReward(BasePlayer player, List<string> rewardsList)
        {
            _Debug("------------------------------");
            _Debug("Method: GiveSubsequentReward");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            _Debug($"Vote Count: {DataFile[player.UserIDString]}");
            foreach (string rewardCommand in rewardsList)
            {
                string command = ParseRewardCommand(player, rewardCommand);
                _Debug($"Reward Command: {command}");
                rust.RunServerCommand(command);
            }
        }

        private string ParseRewardCommand(BasePlayer player, string command)
        {
            return command
                .Replace("{playerid}", player.UserIDString)
                .Replace("{playername}", player.displayName);
        }

        private void CheckIfPlayerDataExists(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: CheckIfPlayerDataExists");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // If the player data entry is null then we need to create a new entry
            if (DataFile[player.UserIDString] == null)
            {
                _Debug($"{player.displayName} data does not exist. Creating new entry now.");
                DataFile[player.UserIDString] = 0;
                SaveDataFile(DataFile);
                _Debug($"{player.displayName} Data has been created.");
            }
        }

        private void ResetAllVoteData()
        {
            _Debug("------------------------------");
            _Debug("Method: ResetAllVoteData");
            foreach (KeyValuePair<string, object> player in DataFile.ToList())
            {
                DataFile[player.Key] = 0;
                _Debug($"Player {player.Key} vote count reset...");
            }
            SaveDataFile(DataFile);
        }

        private void CheckVotingStatus(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: CheckVotingStatus");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            var timeout = 5000;
            if (_config.NotificationSettings[ConfigDefaultKeys.PleaseWaitMessage].ToBool())
            {
                player.ChatMessage(_lang("PleaseWait", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix]));
            }
            // Loop through all the Servers
            foreach (KeyValuePair<string, Dictionary<string, string>> ServersKVP in _config.Servers)
            {
                _Debug($"ServersKVP.Key: {ServersKVP.Key.ToString()}");
                // Loop through all the ID's and Keys for each Voting Site
                foreach (KeyValuePair<string, string> IDKeys in ServersKVP.Value)
                {
                    _Debug($"IDKeys.Key: {IDKeys.Key.ToString().ToLower()}");
                    _Debug($"IDKeys.Value: {IDKeys.Value.ToString()}");

                    // Check if the API key is present
                    if (!_config.VoteSitesAPI.ContainsKey(IDKeys.Key))
                    {
                        ConsoleWarn($"The voting website {IDKeys.Key} does not exist in the API section of the config!");
                        continue;
                    }
                    var APILink = _config.VoteSitesAPI[IDKeys.Key.ToString()][ConfigDefaultKeys.apiStatus];
                    _Debug($"Check Status API Link: {APILink.ToString()}");
                    var usernameAPIEnabled = _config.VoteSitesAPI[IDKeys.Key.ToString()][ConfigDefaultKeys.apiUsername];
                    _Debug($"API Username Enabled: {usernameAPIEnabled}");
                    string[] IDKey = IDKeys.Value.Split(':');
                    _Debug($"ID: {IDKey[0]}");
                    _Debug($"Key/Token: {IDKey[1]}");

                    string formattedURL = "";
                    if (usernameAPIEnabled.ToBool())
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.displayName);
                    }
                    else
                    if (IDKeys.Key.ToString().ToLower() == "rustservers.gg")
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.UserIDString, IDKey[0]);
                    }
                    else
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.UserIDString);
                    }
                    _Debug($"Formatted URL: {formattedURL}");

                    webrequest.Enqueue(formattedURL, null,
                        (code, response) => HandleStatusWebRequestCallback(code, response, player, formattedURL, ServersKVP.Key.ToString(), IDKeys.Key.ToString()), this,
                        RequestMethod.GET, null, timeout);

                    _Debug("------------------------------");
                }
            }
        }

        #endregion

        #region Hooks

        private void OnPlayerConnected(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: OnPlayerConnected");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // Check if the player data is present in the data file
            CheckIfPlayerDataExists(player);
            // Check voting status
            if (!_config.NotificationSettings[ConfigDefaultKeys.OnPlayerSleepEnded].ToBool() &&
                _config.NotificationSettings[ConfigDefaultKeys.OnPlayerConnected].ToBool())
            {
                CheckVotingStatus(player);
            }
        }

        private void OnNewSave(string filename)
        {
            _Debug("------------------------------");
            _Debug("Method: OnNewSave");
            ConsoleLog("New map data detected!");
            if (_config.PluginSettings[ConfigDefaultKeys.ClearRewardsOnWipe] == "true")
            {
                _Debug("Wiping all votes from data file");
                ResetAllVoteData();
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            _Debug("------------------------------");
            _Debug("Method: OnPlayerSleepEnded");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            // Check the voting status when the player wakes up
            if (_config.NotificationSettings[ConfigDefaultKeys.OnPlayerSleepEnded].ToBool())
            {
                CheckVotingStatus(player);
            }
        }

        #endregion
        
        #region ChatCommands

        [ChatCommand("rewardlist")]
        private void RewardListChatCommand(BasePlayer player, string command, string[] args)
        {
            player.ChatMessage(_config.PluginSettings[ConfigDefaultKeys.Prefix] + "The following rewards are given for voting!");
            foreach (KeyValuePair<string, string> kvp in _config.RewardDescriptions)
            {
                player.ChatMessage(kvp.Value);
            }
        }

        [ChatCommand("vote")]
        private void VoteChatCommand(BasePlayer player, string command, string[] args)
        {
            player.ChatMessage(_lang("VoteList", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix]));
            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _config.VoteSitesAPI)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> serverskvp in _config.Servers)
                {
                    foreach (KeyValuePair<string, string> serveridkeys in serverskvp.Value)
                    {
                        if (Equals(kvp.Key, serveridkeys.Key))
                        {
                            string[] parts = serveridkeys.Value.Split(':');
                            player.ChatMessage(serverskvp.Key + ": " + string.Format(kvp.Value[ConfigDefaultKeys.apiLink], parts[0]));
                        }
                    }
                }
            }
            player.ChatMessage(_lang("EarnReward"));
        }

        [ChatCommand("claim")]
        private void ClaimChatCommand(BasePlayer player, string command, string[] args)
        {
            // Check if the player data is present in the data file
            CheckIfPlayerDataExists(player);

            _Debug("------------------------------");
            _Debug("Method: ClaimChatCommand");
            _Debug($"Player: {player.displayName}/{player.UserIDString}");
            var timeout = 5000;
            if (_config.NotificationSettings[ConfigDefaultKeys.PleaseWaitMessage].ToBool())
            {
                player.ChatMessage(_lang("PleaseWait", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix]));
            }
            // Loop through all the Servers
            foreach (KeyValuePair<string, Dictionary<string, string>> ServersKVP in _config.Servers)
            {
                _Debug($"ServersKVP.Key: {ServersKVP.Key.ToString()}");
                // Loop through all the ID's and Keys for each Voting Site
                foreach (KeyValuePair<string, string> IDKeys in ServersKVP.Value)
                {
                    _Debug($"IDKeys.Key: {IDKeys.Key.ToString().ToLower()}");
                    _Debug($"IDKeys.Value: {IDKeys.Value.ToString()}");

                    // Check if the API key is present
                    if (!_config.VoteSitesAPI.ContainsKey(IDKeys.Key))
                    {
                        ConsoleWarn($"The voting website {IDKeys.Key} does not exist in the API section of the config!");
                        continue;
                    }
                    var APILink = _config.VoteSitesAPI[IDKeys.Key.ToString()][ConfigDefaultKeys.apiClaim];
                    _Debug($"Check Status API Link: {APILink.ToString()}");
                    var usernameAPIEnabled = _config.VoteSitesAPI[IDKeys.Key.ToString()][ConfigDefaultKeys.apiUsername];
                    _Debug($"API Username Enabled: {usernameAPIEnabled}");
                    string[] IDKey = IDKeys.Value.Split(':');
                    _Debug($"ID: {IDKey[0]}");
                    _Debug($"Key/Token: {IDKey[1]}");

                    string formattedURL = "";
                    if (usernameAPIEnabled.ToBool())
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.displayName);
                    }
                    else
                    if (IDKeys.Key.ToString().ToLower() == "rustservers.gg")
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.UserIDString, IDKey[0]);
                    }
                    else
                    {
                        formattedURL = string.Format(APILink, IDKey[1], player.UserIDString);
                    }
                    _Debug($"Formatted URL: {formattedURL}");

                    webrequest.Enqueue(formattedURL, null,
                        (code, response) => HandleClaimWebRequestCallback(code, response, player, formattedURL, ServersKVP.Key.ToString(), IDKeys.Key.ToString()), this,
                        RequestMethod.GET, null, timeout);

                    _Debug("------------------------------");
                }
            }

            // Wait until all web requests are done and then send a message
            timer.Once(5f, () =>
            {
                player.ChatMessage(_lang("ClaimReward", player.UserIDString, _config.PluginSettings[ConfigDefaultKeys.Prefix]));
            });
        }
        
        
        #endregion
        
        #region ConsoleHelpers

        protected void ConsoleLog(object message)
        {
            Puts(message?.ToString());
        }

        protected void ConsoleError(string message)
        {
            if (Convert.ToBoolean(_config.PluginSettings[ConfigDefaultKeys.LogEnabled]))
                LogToFile(_PluginName, $"ERROR: {message}", this);
            
            Debug.LogError($"ERROR: " + message);
        }

        protected void ConsoleWarn(string message)
        {
            if (Convert.ToBoolean(_config.PluginSettings[ConfigDefaultKeys.LogEnabled]))
                LogToFile(_PluginName, $"WARNING: {message}", this);
            
            Debug.LogWarning($"WARNING: " + message);
        }

        protected void _Debug(string message, string arg = null)
        {
            if (_config.DebugSettings[ConfigDefaultKeys.DebugEnabled] == "true")
            {
                if (Convert.ToBoolean(_config.PluginSettings[ConfigDefaultKeys.LogEnabled]))
                    LogToFile(_PluginName, $"DEBUG: {message}", this);
                
                Puts($"DEBUG: {message}");
                if (arg != null)
                {
                    Puts($"DEBUG ARG: {arg}");
                }
            }
        }
        
        #endregion

        #region ConsoleCommands

        [ConsoleCommand("clearvote")]
        private void ClearPlayerVoteCountConsoleCommand(ConsoleSystem.Arg arg)
        {
            // Check if an argument is even passed
            if (!arg.HasArgs(1))
            {
                ConsoleError("Command clearvote usage: clearvote steamid|username");
                return;
            }
            
            // Get the player based off the argument passed
            BasePlayer player = arg.GetPlayer(0);
            if (player == null)
            {
                ConsoleError($"Failed to find player with ID/Username/IP of: {arg.GetString(0)}");
                return;
            }
            
            // Update the player vote count in the data file
            DataFile[player.UserIDString] = 0;
            SaveDataFile(DataFile);
            ConsoleLog($"{player.displayName}/{player.UserIDString} vote count has been reset to 0");
        }

        [ConsoleCommand("checkvote")]
        private void CheckPlayerVoteCountConsoleCommand(ConsoleSystem.Arg arg)
        {
            // Check if an argument is even passed
            if (!arg.HasArgs(1))
            {
                ConsoleError("Command checkvote usage: checkvote steamid|username");
                return;
            }
            
            // Get the player based off the argument passed
            BasePlayer player = arg.GetPlayer(0);
            if (player == null)
            {
                ConsoleError($"Failed to find player with ID/Username/IP of: {arg.GetString(0)}");
                return;
            }
            
            ConsoleLog($"Player {player.displayName}/{player.UserIDString} has {getPlayerVotes(player.UserIDString)} votes total");
        }

        [ConsoleCommand("setvote")]
        private void SetPlayerVoteCountConsoleCommand(ConsoleSystem.Arg arg)
        {
            // Check if an argument is even passed
            if (!arg.HasArgs(2))
            {
                ConsoleError("Command setvote usage: setvote steamid|username numberOfVotes");
                return;
            }
            
            // Get the player based off the argument passed
            BasePlayer player = arg.GetPlayer(0);
            if (player == null)
            {
                ConsoleError($"Failed to find player with ID/Username/IP of: {arg.GetString(0)}");
                return;
            }

            DataFile[player.UserIDString] = arg.GetInt(1);
            SaveDataFile(DataFile);
            ConsoleLog($"Player {player.displayName}/{player.UserIDString} vote count has been updated to {arg.GetString(1)}");
        }

        [ConsoleCommand("resetvotedata")]
        private void ResetAllVoteDataConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.HasArgs(1))
            {
                ConsoleError("Command resetvotedata usage: This command has no arguments. Run the command again with no arguments");
                return;
            }

            ResetAllVoteData();
        }
        
        #endregion

        #region APIHooks

        [HookMethod("getPlayerVotes")]
        public int getPlayerVotes(string steamID)
        {
            if (DataFile[steamID] == null)
            {
                _Debug("getPlayerVotes(): Player data doesn't exist");
                return 0;
            }
            
            return (int) DataFile[steamID];
        }

        #endregion
        
        #region Localization
        string _lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use this command!",
                ["ClaimStatus"] = "{0} <color=#e67e22>{1}</color>\nChecked {2}, Status: {3}",
                ["ClaimReward"] = "{0} If you voted, and the votes went through, then you just received your vote reward(s). Enjoy!",
                ["PleaseWait"] = "{0} Checking all the VoteSites API's... Please be patient as this can take some time...",
                ["VoteList"] = "{0} You can vote for our server at the following links:",
                ["EarnReward"] = "When you have voted, type <color=#e67e22>/claim</color> to claim your reward(s)!",
                ["ThankYou"] = "{0} Thank you for voting! You have voted <color=#e67e22>{1}</color> time(s) Here is your reward for: <color=#e67e22>{2}</color>",
                ["NoRewards"] = "{0} You haven't voted for <color=#e67e22>{1}</color> on <color=#e67e22>{2}</color> yet! Type <color=#e67e22>/vote</color> to get started!",
                ["RememberClaim"] = "{0} <color=#e67e22>{1}</color> is reporting that you have an unclaimed reward! Use <color=#e67e22>/claim</color> to claim your reward!\n You have to claim your reward within <color=#e67e22>24h</color>! Otherwise it will be gone!",
                ["GlobalChatAnnouncements"] = "{0} <color=#e67e22>{1}</color> has voted <color=#e67e22>{2}</color> time(s) and just received their rewards. Find out where you can vote by typing <color=#e67e22>/vote</color>\nTo see a list of available rewards type <color=#e67e22>/rewardlist</color>",
                ["AlreadyVoted"] = "{0} <color=#e67e22>{1}</color> reports you have already voted! Vote again later.",
                ["DiscordWebhookMessage"] = "{0} has voted for {1} on {2} and got some rewards! Type /rewardlist in game to find out what you can get when you vote for us!"
            }, this);
        }
        #endregion
        
        #region Config
        
        private PluginConfig _config;
        
        protected override void LoadDefaultConfig()
        {
            
            _config = new PluginConfig();
            _config.DebugSettings = new Dictionary<string, string>
            {
                {ConfigDefaultKeys.DebugEnabled, "false"},
                {ConfigDefaultKeys.VerboseDebugEnabled, "false"},
                {ConfigDefaultKeys.CheckAPIResponseCode, "0"},
                {ConfigDefaultKeys.ClaimAPIRepsonseCode, "0"}
            };
            _config.PluginSettings = new Dictionary<string, string>
            {
                {ConfigDefaultKeys.LogEnabled, "true"},
                {ConfigDefaultKeys.ClearRewardsOnWipe, "true"},
                {ConfigDefaultKeys.RewardIsCumulative, "false"},
                {ConfigDefaultKeys.Prefix, "<color=#e67e22>[EasyVote]</color> "},
            };
            _config.NotificationSettings = new Dictionary<string, string>
            {
                {ConfigDefaultKeys.GlobalChatAnnouncements, "true"},
                {ConfigDefaultKeys.PleaseWaitMessage, "true"},
                {ConfigDefaultKeys.OnPlayerSleepEnded, "false"},
                {ConfigDefaultKeys.OnPlayerConnected, "true"}
            };
            _config.Discord = new Dictionary<string, string>
            {
                {ConfigDefaultKeys.discordWebhookURL, "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks"},
                {ConfigDefaultKeys.DiscordEnabled, "false"},
                {ConfigDefaultKeys.discordTitle, "A player has just voted for us!"} 
            };
            _config.Rewards = new Dictionary<string, List<string>>
            {
                { "@", new List<string>() { "giveto {playerid} supply.signal 1" } },
                { "first", new List<string>() { "giveto {playerid} stones 10000", "sr add {playerid} 10000" } },
                { "3", new List<string>() { "addgroup {playerid} vip 7d" } },
                { "6", new List<string>() { "grantperm {playerid} plugin.test 1d" } },
                { "10", new List<string>() { "zl.lvl {playerid} * 2" } }
            };
            _config.RewardDescriptions = new Dictionary<string, string>
            {
                { "@", "Every Vote: 1 Supply Signal" },
                { "first", "First Vote: 10000 Stones, 10000 RP" },
                { "3", "3rd Vote: 7 days of VIP rank" },
                { "6", "6th Vote: 1 day of plugin.test permission" },
                { "10", "10th Vote: 2 zLevels in Every Category" }
            };
            _config.Servers = new Dictionary<string, Dictionary<string, string>>
            {
                { "ServerName1", new Dictionary<string, string>() { { "Rust-Servers.net", "ID:KEY" },{ "Rustservers.gg", "ID:KEY" }, { "BestServers.com", "ID:KEY" } } },
                { "ServerName2", new Dictionary<string, string>() { { "Rust-Servers.net", "ID:KEY" },{ "Rustservers.gg", "ID:KEY" }, { "BestServers.com", "ID:KEY" } } }
            };
            _config.VoteSitesAPI = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "Rust-Servers.net",
                    new Dictionary<string, string>()
                    {
                        {
                            ConfigDefaultKeys.apiClaim,
                            "http://rust-servers.net/api/?action=custom&object=plugin&element=reward&key={0}&steamid={1}"
                        },
                        {
                            ConfigDefaultKeys.apiStatus,
                            "http://rust-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}"
                        },
                        { ConfigDefaultKeys.apiLink, "http://rust-servers.net/server/{0}" },
                        { ConfigDefaultKeys.apiUsername, "false"}
                    }
                },
                {
                    "Rustservers.gg",
                    new Dictionary<string, string>()
                    {
                        {
                            ConfigDefaultKeys.apiClaim,
                            "https://rustservers.gg/vote-api.php?action=claim&key={0}&server={2}&steamid={1}"
                        },
                        {
                            ConfigDefaultKeys.apiStatus,
                            "https://rustservers.gg/vote-api.php?action=status&key={0}&server={2}&steamid={1}"
                        },
                        { ConfigDefaultKeys.apiLink, "https://rustservers.gg/server/{0}" },
                        { ConfigDefaultKeys.apiUsername, "false"}
                    }
                },
                {
                    "BestServers.com",
                    new Dictionary<string, string>()
                    {
                        {
                            ConfigDefaultKeys.apiClaim,
                            "https://bestservers.com/api/vote.php?action=claim&key={0}&steamid={1}"
                        },
                        {
                            ConfigDefaultKeys.apiStatus,
                            "https://bestservers.com/api/vote.php?action=status&key={0}&steamid={1}"
                        },
                        { ConfigDefaultKeys.apiLink, "https://bestservers.com/server/{0}" },
                        { ConfigDefaultKeys.apiUsername, "false"}
                    }
                },
                {
                    "7DaysToDie-Servers.com",
                    new Dictionary<string, string>()
                    {
                        {
                            ConfigDefaultKeys.apiClaim,
                            "https://7daystodie-servers.com/api/?action=post&object=votes&element=claim&key={0}&steamid={1}"
                        },
                        {
                            ConfigDefaultKeys.apiStatus,
                            "https://7daystodie-servers.com/api/?object=votes&element=claim&key={0}&steamid={1}"
                        },
                        { ConfigDefaultKeys.apiLink, "https://7daystodie-servers.com/server/{0}" },
                        { ConfigDefaultKeys.apiUsername, "false"}
                    }
                },
                {
                    "Ark-Servers.net",
                    new Dictionary<string, string>()
                    {
                        {
                            ConfigDefaultKeys.apiClaim,
                            "https://ark-servers.net/api/?action=post&object=votes&element=claim&key={0}&steamid={1}"
                        },
                        {
                            ConfigDefaultKeys.apiStatus,
                            "https://ark-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}"
                        },
                        { ConfigDefaultKeys.apiLink, "https://ark-servers.net/server/{0}" },
                        { ConfigDefaultKeys.apiUsername, "false"}
                    }
                }
            };
            SaveConfig();
            ConsoleWarn("A new configuration file has been generated!");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                    //SaveConfig();
                }
            }
            catch
            {
                ConsoleError("The configuration file is corrupted. Please delete the config file and reload the plugin.");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        class ConfigDefaultKeys
        {
            // API Stuff
            public const string apiClaim = "API Claim Reward (GET URL)";
            public const string apiStatus = "API Vote status (GET URL)";
            public const string apiLink = "Vote link (URL)";
            public const string apiUsername = "Site Uses Username Instead of Player Steam ID?";
            // Discord Webhook
            public const string discordTitle = "Discord Title";
            public const string discordWebhookURL = "Discord webhook (URL)";
            public const string DiscordEnabled = "DiscordMessage Enabled (true / false)";
            // Plugin Settings
            public const string Prefix = "Chat Prefix";
            public const string LogEnabled = "Enable logging => logs/EasyVote (true / false)";
            public const string RewardIsCumulative = "Vote rewards cumulative (true / false)";
            public const string ClearRewardsOnWipe = "Wipe Rewards Count on Map Wipe?";
            // Notification Settings
            public const string GlobalChatAnnouncements = "Globally announcment in chat when player voted (true / false)";
            public const string PleaseWaitMessage = "Enable the 'Please Wait' message when checking voting status?";
            public const string OnPlayerSleepEnded = "Notify player of rewards when they stop sleeping?";
            public const string OnPlayerConnected = "Notify player of rewards when they connect to the server?";
            // Debug Settings
            public const string DebugEnabled = "Debug Enabled?";
            public const string VerboseDebugEnabled = "Enable Verbose Debugging? (READ DOCUMENTATION FIRST!)";
            public const string CheckAPIResponseCode = "Set Check API Response Code (0 = Not found, 1 = Has voted and not claimed, 2 = Has voted and claimed)";
            public const string ClaimAPIRepsonseCode = "Set Claim API Response Code (0 = Not found, 1 = Has voted and not claimed. The vote will now be set as claimed., 2 = Has voted and claimed";
        }
        
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Debug Settings")]
            public Dictionary<string, string> DebugSettings;
            
            [JsonProperty(PropertyName = "Plugin Settings")]
            public Dictionary<string, string> PluginSettings;
            
            [JsonProperty(PropertyName = "Notification Settings")]
            public Dictionary<string, string> NotificationSettings;

            [JsonProperty(PropertyName = "Discord")]
            public Dictionary<string, string> Discord;

            [JsonProperty(PropertyName = "Rewards")]
            public Dictionary<string, List<string>> Rewards;

            [JsonProperty(PropertyName = "Reward Descriptions")]
            public Dictionary<string, string> RewardDescriptions;

            [JsonProperty(PropertyName = "Server Voting IDs and Keys")]
            public Dictionary<string, Dictionary<string, string>> Servers;
            
            [JsonProperty(PropertyName = "Voting Sites API Information (DO NOT CHANGE OR DELETE ANYTHING HERE!!!!)")]
            public Dictionary<string, Dictionary<string, string>> VoteSitesAPI;
            
        }

        #endregion

        #region Data

        protected internal static DynamicConfigFile DataFile = Interface.Oxide.DataFileSystem.GetDatafile(_PluginName);

        private void SaveDataFile(DynamicConfigFile data)
        {
            data.Save();
            _Debug("Data file has been updated.");
        }

        #endregion
        
        #region DiscordMessages

        private IEnumerator DiscordSendMessage(string msg)
        {
            // Check if the discord webhook is default or null/empty
            if (_config.Discord[ConfigDefaultKeys.discordWebhookURL] != "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks" || !string.IsNullOrEmpty(_config.Discord[ConfigDefaultKeys.discordWebhookURL]))
            {
                // Grab the form data
                WWWForm formData = new WWWForm();
                string content = $"{msg}\n";
                formData.AddField("content", content);

                // Define the request
                using (var request = UnityWebRequest.Post(_config.Discord[ConfigDefaultKeys.discordWebhookURL], formData))
                {
                    // Execute the request
                    yield return request.SendWebRequest();
                    if ((request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) && request.error.Contains("Too Many Requests"))
                    {
                        Puts("Discord Webhook Rate Limit Exceeded... Waiting 30 seconds...");
                        yield return new WaitForSeconds(30f);
                    }
                }
            }

            ServerMgr.Instance.StopCoroutine(coroutine);
        }
        #endregion
    }

}