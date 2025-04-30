
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.Chat;
using ZeepSDK.Messaging;

namespace BrokenTracks
{
    public static class BTCore
    {
        public static Color32 darkRed = new Color32(137, 0, 0, 255);
        public static Color32 zeepkistOrange = new Color32(255, 146, 0, 255);
        public static string nextLevelUID;
        public static int nextLevelIndex;
        public static bool skippedRandomly = false;
        public static Dictionary<string, OnlineZeeplevel> badTracks = new ();

        public static void Initialize()
        {
        }
        /*
         * My Functions
         */
        //Local function to quickly format chat messages
        public static Dictionary<string, string> formatChatMessage(string prefix, string message, bool prefixBrackets = true, string prefixColor = "#18a81e", string messageColor = "#FFC531")
        {
            if (prefixBrackets)
                prefix = "[<color="+prefixColor+">"+prefix+"</color>]";
            else
                prefix = "<color="+prefixColor+">"+prefix+"</color>";
            return new Dictionary<string, string>
            {
                { "prefix", prefix },
                { "message", "<color="+messageColor+">"+message+"</color>" }
            };
        }

        //wraper for Custom messages
        public static void sendCustomChatMessage(bool everyone, ulong steamid = 0, string message = "", string prefix = "")
        {
            ZeepkistNetwork.SendCustomChatMessage(everyone, steamid, message, prefix);
        }
        
        //handle working with On screen Messsages
        public static void sendMessenger(string prefix, string message, float duration, bool usePrefix = false)
        {
            Dictionary<string, string> chatMessage = formatChatMessage(prefix, message);
            chatMessage["message"] = (usePrefix ? string.Join(",", chatMessage) : chatMessage["message"]);
            MessengerApi.LogError(chatMessage["message"], duration);
        }

        //Save any and all data needed for the plugin
        public static void saveData(string type = "all")
        {

            if(type == "all")
                Plugin.Instance.Log("Saving all to json.", 2);
            if (type is "brokentracks" or "all")
            {
                Plugin.storage.SaveToJson("brokentracks", badTracks);
                Plugin.Instance.Log("Saving brokentracks to json.", 2);
            }
        }
        //Load any and all data needed for the plugin
        public static void loadData(string type = "all")
        {
            if(type == "all")
                Plugin.Instance.Log("Loading all from json.", 2);
            if (type is "brokentracks" or "all")
            {
                badTracks = (Dictionary<string, OnlineZeeplevel>)Plugin.storage.LoadFromJson("brokentracks", badTracks.GetType());
                Plugin.Instance.Log("Loading brokentracks from json.", 2);
            }
        }
        public static void ClearBrokenTracks(object sender, EventArgs e)
        {
            badTracks = new ();
            saveData ("brokentracks");
            Plugin.Instance.Log("Clearing all saved broken tracks.", 2);
        }
        
        //Does a loop through the playlist to find the next track that isn't marked bad
        public static int findNextGoodTrack(List<OnlineZeeplevel> playlist, int nextIndex)
        {
           int counter = 0;
            while (counter < playlist.Count)
            {
                //found empty/invalid playlist item or to high, restart
                if (nextIndex >= playlist.Count || playlist[nextIndex] == null)
                    nextIndex = -1;
                else if (!badTracks.ContainsKey(playlist[nextIndex].UID))
                    break;
                
                nextIndex++;
                counter++;
            }
            
            return nextIndex;
        }
        
        /*
         * Checks to see if steam is down using custom service
         * if steam is down workshop maps will not load, so we need to prevent autoskip, and adding to bad list
         */
        static async Task<bool> CheckSteamStatus()
        {
            string url = "https://zeepkist.kilandor.com/steam_status.php";

            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            try
            {
                // Fetch the API response
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                // Parse JSON
                JObject data = JObject.Parse(responseBody);

                // Extract Steam status
                string steamStatus = data["status"]?.ToString();
                if (steamStatus == "normal")
                {
                    Plugin.Instance.Log("Steam is online", 2);
                    return true;
                }
                else if (steamStatus == "maintenance")
                {
                    Plugin.Instance.Log("Steam is under maintenance", 2);
                    return false;
                }
                else
                {
                    Plugin.Instance.Log("Steam status: "+steamStatus, 2);Console.WriteLine();
                    return false;
                }
            }
            catch (HttpRequestException e)
            {
                Plugin.Instance.Log("Request error: "+e.Message, 3);
                return false;
            }
            catch (Exception e)
            {
                Plugin.Instance.Log("Unexpected error: "+e.Message, 3);
                return false;
            }
        }
        
        /*
         * Harmony Patches
         */
        //Handles coloring an object in the playlist
        public static void OnDrawPlaylistItem(PlaylistListItem item, OnlineZeeplevel newZeeplevel)
        {
           if (newZeeplevel == null)
                return; 
          
           //Plugin.Instance.Log(JsonConvert.SerializeObject(newZeeplevel, Formatting.Indented), 2);
           //Level isn't found in the list color it orange, could be from not downloaded or missing
           if(!LevelManager.Instance.TryGetLevel(newZeeplevel.UID, out LevelScriptableObject _))
           {
                item.levelName.color = zeepkistOrange;
                item.levelIndex.color = zeepkistOrange;
           }

           //Level is found in the bad list, mark with skull and color it red
           if (badTracks.ContainsKey(newZeeplevel.UID))
           {
               item.levelName.text = "<sprite=\"Zeepkist\" name=\"Skull\"> " + newZeeplevel.Name.NoParse() + " - " + newZeeplevel.Author.NoParse();
               item.levelName.color = darkRed;
               item.levelIndex.color = darkRed;
           }
        }
        
        //Sets the nextlevel when skippings, changing nextmap, or changing/loading playlist
        public static void OnAcceptPlaylist(PlaylistMenu instance)
        {
            OnlineZeeplevel nextZeeplevel = instance.thePlaylist[instance.nextPlayListIndex];
            Plugin.Instance.Log(" Accepted Playlist to "+nextZeeplevel.UID, 2);
            nextLevelUID = nextZeeplevel.UID;
            nextLevelIndex = instance.nextPlayListIndex;
            
            if (badTracks.ContainsKey(nextLevelUID))
            {
                Plugin.Instance.Log("Warning next track is bad "+nextLevelUID, 2);
                sendMessenger("BrokenTracks", "<sprite=\"Zeepkist\" name=\"Skull\"> Warning next map is a known bad track.", Plugin.Instance.warningDuration.Value);
            }
        }
        
        public static async void OnDoStart()
        {
            ZeepkistLobby lobby = ZeepkistNetwork.CurrentLobby;
            
            if (!ZeepkistNetwork.IsConnectedToGame || !ZeepkistNetwork.IsMasterClient) //&& !ZeepkistNetwork.LocalPlayer.hasHostPowers))
            {
                Plugin.Instance.Log("Not connected, or not master/host, skipping logic. Master "+ZeepkistNetwork.IsMasterClient+" - HostPower "+ZeepkistNetwork.LocalPlayer.hasHostPowers, 2);
                return;
            }
            
            //command use to skip randomly, unable to detect intented map loading, prevent problems
            if (skippedRandomly)
            {
                //prepare for the next track
                nextLevelUID = lobby.Playlist[lobby.NextPlaylistIndex].UID;
                nextLevelIndex = lobby.NextPlaylistIndex;
                skippedRandomly = false;
                return;
            }
            Plugin.Instance.Log("Master "+ZeepkistNetwork.IsMasterClient+" - HostPower "+ZeepkistNetwork.LocalPlayer.hasHostPowers, 2);
            //Plugin.Instance.Log("Starting Level "+JsonConvert.SerializeObject(lobby, Formatting.Indented), 2);
            
            if(lobby.LevelUID == "ea1" && lobby.Playlist.Count == 1)
                Plugin.Instance.Log("This is a new Lobby with the default playlist.", 2);
            
            Plugin.Instance.Log("LobbyNextIndex "+lobby.NextPlaylistIndex+" UID ", 2);
            Plugin.Instance.Log("pUID "+lobby.Playlist[lobby.NextPlaylistIndex].UID, 2);
            Plugin.Instance.Log("nUID "+nextLevelUID, 2);
            Plugin.Instance.Log("nIndex "+nextLevelIndex, 2);
            
            // When the server loads to EA5 through an invalid level, the next playlist item doesn't change its kept the same
            if(lobby.LevelUID == "ea5" && lobby.Playlist.Count >= 1 && lobby.Playlist[lobby.NextPlaylistIndex].UID == nextLevelUID)
            {
                Plugin.Instance.Log("Bad Track? ea5 loaded, more than 1 item in playlist, and next item the same", 2);
                if (nextLevelUID != lobby.LevelUID)
                {
                    bool steamStatus = await CheckSteamStatus();
                    if (steamStatus)
                    {
                        if (!badTracks.ContainsKey(nextLevelUID))
                        {
                            //use the global stored index that matches UID
                            //This is due to it updating possibly when waiting for steam status
                            badTracks.Add(nextLevelUID, lobby.Playlist[nextLevelIndex]);
                            Plugin.Instance.Log("Added " + nextLevelUID + "to bad tracks.");
                            saveData("brokentracks");
                        }
                        else
                        {
                            Plugin.Instance.Log("Already in bad tracks " + nextLevelUID, 2);
                        }

                        int nextIndex = findNextGoodTrack(lobby.Playlist, lobby.NextPlaylistIndex);
                        nextLevelUID = lobby.Playlist[nextIndex].UID;
                        nextLevelIndex = nextIndex;

                        Plugin.Instance.Log("Setting next track "+nextLevelUID, 2);
                        if(Plugin.Instance.autoSkipEnabled.Value)
                        {
                            Plugin.Instance.Log("Auto-Skip enabled, skipping to next valid track");
                            Dictionary<string,string> chatMessage = formatChatMessage("BrokenTracks","Auto-Skip due to broken playlist item.", false);
                            sendCustomChatMessage(true, 0, chatMessage["message"], chatMessage["prefix"]);
                            ChatApi.SendMessage("/fs "+nextIndex);
                            return;
                        } 
                    }
                    else
                    {
                        //get the local player who is sending the command so we can send a private message to them
                        //ChatAPI.AddLocalMessage is removed when round ends, so it doesn't fit the needs 
                        ZeepkistNetworkPlayer player = ZeepkistNetwork.LocalPlayer;
                        
                        Dictionary<string,string> chatMessage = formatChatMessage("BrokenTracks","Steam may be down. Track will not be added to bad list and no autoskip.", false,  messageColor: "#f03030");
                        sendCustomChatMessage(false, player.SteamID, chatMessage["message"],chatMessage["prefix"]);
                    }
                    
                }
            }

            if (nextLevelUID == "" || nextLevelUID == lobby.LevelUID)
            {
                //Safety check incase a level was incorrectly marked bad
                if (badTracks.ContainsKey(nextLevelUID))
                {
                    badTracks.Remove(nextLevelUID);
                    Plugin.Instance.Log("Removed " + nextLevelUID + " from bad tracks.");
                    saveData("brokentracks");
                }
                
               nextLevelUID = lobby.Playlist[lobby.NextPlaylistIndex].UID;
               nextLevelIndex = lobby.NextPlaylistIndex;
               Plugin.Instance.Log("Correct Level loaded setting next track "+nextLevelUID, 2);
               if (badTracks.ContainsKey(nextLevelUID))
               {
                   Plugin.Instance.Log("Warning next track is bad "+nextLevelUID, 2);
                   sendMessenger("BrokenTracks", "<sprite=\"Zeepkist\" name=\"Skull\"> Warning next map is a known bad track.", Plugin.Instance.warningDuration.Value);
                   if (Plugin.Instance.autoNextEnabled.Value)
                   {
                       int nextIndex = findNextGoodTrack(lobby.Playlist, lobby.NextPlaylistIndex);
                       nextLevelUID = lobby.Playlist[nextIndex].UID;
                       nextLevelIndex = nextIndex;
                       ZeepSDK.Multiplayer.MultiplayerApi.SetNextLevelIndex(nextIndex);
                   }
               }
            }
        }
        
        //Updates the next level, may be from /fs
        /*
         * This packet does not trigger if the playlist didn't change
         */
        public static void OnChangeLobbyPlaylistIndex(ChangeLobbyPlaylistIndexPacket packet)
        {
            if (!ZeepkistNetwork.IsMasterClient)
                return;
            
            ZeepkistLobby lobby = ZeepkistNetwork.CurrentLobby;
            Plugin.Instance.Log("ChangedLobbyPlaylistIndexPacket  lobby " + lobby.CurrentPlaylistIndex + " CurrentIndex: " + packet.CurrentIndex + " Next: " + packet.NextIndex, 2);

            if (lobby.CurrentPlaylistIndex != packet.CurrentIndex)
            {
                if (packet.CurrentIndex >= 0 && packet.CurrentIndex < lobby.Playlist.Count)
                {
                    nextLevelUID = lobby.Playlist[packet.CurrentIndex].UID;
                    nextLevelIndex = packet.CurrentIndex;
                }

                Plugin.Instance.Log("PlaylistIndexPacket changed next level to " + nextLevelUID, 2);
            }
        }

        public static void OnChatMessageReceived(ulong playerid, string username, string message)
        {
            if (!ZeepkistNetwork.IsMasterClient)
                return;
            
            /*
             * Default message sent when a invalid map is loaded
             * <i><color=red>Workshop Level could not be loaded! <sprite="Zeepkist" name="Skull"> Skipping...</color></i>
             */
            //cleanup and remove the noparse
            message = Regex.Replace(message, @"<noparse>(.*?)<\/noparse>", "$1");
            Plugin.Instance.Log("Checking Received Message: "+message);

            if (message.Contains("Workshop Level could not be loaded!"))
            {
                Plugin.Instance.Log("Server sent message indicating failed track loading", 2);
            }
                
            
        }

        public static void OnSendChatMessage( string message)
        {
            if (!ZeepkistNetwork.IsMasterClient)
                return;
            
            //get the local player who is sending the command so we can send a private message to them
            //ChatAPI.AddLocalMessage is removed when round ends, so it doesn't fit the needs 
            ZeepkistNetworkPlayer player = ZeepkistNetwork.LocalPlayer;
            
            Plugin.Instance.Log("Checking Sent Message: "+message+" from "+player.Username+" steamid "+player.SteamID, 2);
            
            /*
             * Matches vs command and argument
             * examples
             * /fs
             * /fs 1
             * /fs random
             */
            
            string pattern = @"^\/(fs|forceskip|skiplevel)(?:\s+(\S+))?$";

   
            Match match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string command = match.Groups[1].Value;
                string argument = match.Groups[2].Success ? match.Groups[2].Value : "";
                Plugin.Instance.Log("Command: "+command+", Argument: "+argument, 2);
                
                //Unable to handle random skips, so flag and provide message
                if(argument.Equals("random", StringComparison.OrdinalIgnoreCase))
                {
                    skippedRandomly = true;
                    Dictionary<string,string> chatMessage = formatChatMessage("BrokenTracks","Unable to detect broken maps loading from skipping randomly.", false, messageColor: "#f03030");
                    sendCustomChatMessage(false, player.SteamID, chatMessage["message"],chatMessage["prefix"]);
                }
                // since an index was used we can use that to set what the intended UID is
                else if (int.TryParse(argument, out _))
                {
                    ZeepkistLobby lobby = ZeepkistNetwork.CurrentLobby;
                    int nextIndex = int.Parse(argument);
                    if (nextIndex >= 0 && nextIndex < lobby.Playlist.Count)
                    {
                        nextLevelUID = lobby.Playlist[nextIndex].UID;
                        nextLevelIndex = nextIndex;
                    }

                    Plugin.Instance.Log("Command detected setting nextLevelUID "+nextLevelUID, 2);
                }
            }
        }
    }
}