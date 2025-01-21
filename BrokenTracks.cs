
using System;
using System.Collections.Generic;
using Lidgren.Network;
using Newtonsoft.Json;
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
        public static string defaultLevelUID = "ea1";
        public static string nextLevelUID;
        public static Dictionary<string, OnlineZeeplevel> badTracks = new ();

        public static void Initialize()
        {
        }
        //Local function to quickly format chat messages
        public static Dictionary<string, string> formatChatMessage(string prefix, string message, bool prefixBrackets = true, string prefixColor = "#18a81e", string messageColor = "#FFC531")
        {
            if (prefixBrackets)
                prefix = $"[<color={prefixColor}>{prefix}</color>]";
            else
                prefix = $"<color={prefixColor}>{prefix}</color>";
            return new Dictionary<string, string>
            {
                { "prefix", prefix },
                { "message", $"<color={messageColor}>{message}</color>" }
            };
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
                nextIndex++;
                //found empty/invalid playlist item or to high, restart
                if (nextIndex >= playlist.Count || playlist[nextIndex] == null)
                    nextIndex = -1;
                else if (!badTracks.ContainsKey(playlist[nextIndex].UID))
                    break;

                counter++;
            }

            return nextIndex;
        }
        
        //Handles coloring an object in the playlist
        public static void OnDrawPlaylistItem(PlaylistListItem item, OnlineZeeplevel newZeeplevel)
        {
            if (newZeeplevel == null)
                return; 
           Plugin.Instance.Log(JsonConvert.SerializeObject(newZeeplevel, Formatting.Indented), 2);
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
            
            if (badTracks.ContainsKey(nextLevelUID))
            {
                Plugin.Instance.Log("Warning next track is bad "+nextLevelUID, 2);
                sendMessenger("BrokenTracks", "<sprite=\"Zeepkist\" name=\"Skull\"> Warning next map is a known bad track.", Plugin.Instance.warningDuration.Value);
            }
        }
        
        public static void OnDoStart()
        {
            ZeepkistLobby lobby = ZeepkistNetwork.CurrentLobby;
            
            if (!ZeepkistNetwork.IsConnectedToGame || !ZeepkistNetwork.IsMasterClient) //&& !ZeepkistNetwork.LocalPlayer.hasHostPowers))
            {
                Plugin.Instance.Log("Not connected, or not master/host, skipping logic. Master "+ZeepkistNetwork.IsMasterClient+" - HostPower "+ZeepkistNetwork.LocalPlayer.hasHostPowers, 2);
                return;
            }
            Plugin.Instance.Log("Master "+ZeepkistNetwork.IsMasterClient+" - HostPower "+ZeepkistNetwork.LocalPlayer.hasHostPowers, 2);
            Plugin.Instance.Log("Starting Level "+JsonConvert.SerializeObject(lobby, Formatting.Indented), 2);
            
            if(lobby.LevelUID == "ea1" && lobby.Playlist.Count == 1)
                Plugin.Instance.Log("This is a new Lobby with the default playlist.", 2);
            if(lobby.LevelUID == "ea5" && lobby.Playlist.Count >= 1 && lobby.Playlist[lobby.NextPlaylistIndex].UID == nextLevelUID)
            {
                Plugin.Instance.Log("Bad Track? ea5 loaded, more than 1 item in playlist, and next item the same", 2);
                if (nextLevelUID != lobby.LevelUID)
                {
                    if (!badTracks.ContainsKey(nextLevelUID))
                    {
                        badTracks.Add(nextLevelUID, lobby.Playlist[lobby.NextPlaylistIndex]);
                        Plugin.Instance.Log("Added " + nextLevelUID + "to bad tracks.");
                        saveData("brokentracks");
                    }
                    else
                    {
                        Plugin.Instance.Log("Already in bad tracks " + nextLevelUID, 2);
                    }

                    int nextIndex = findNextGoodTrack(lobby.Playlist, lobby.NextPlaylistIndex);
                    nextLevelUID = lobby.Playlist[nextIndex].UID;

                    Plugin.Instance.Log("Setting next track "+nextLevelUID, 2);
                    if(Plugin.Instance.autoSkipEnabled.Value)
                    {
                        Plugin.Instance.Log("Auto-Skip enabled, skipping to next valid track");
                        Dictionary<string,string> chatMessage = formatChatMessage("BrokenTracks","Auto-Skip due to broken playlist item.", false);
                        ZeepkistNetwork.SendCustomChatMessage(true, 0, chatMessage["message"], chatMessage["prefix"]);
                        ChatApi.SendMessage("/fs");
                        return;
                    }
                    
                }
            }
            if (nextLevelUID == "" || nextLevelUID == lobby.LevelUID)
            {
               nextLevelUID = lobby.Playlist[lobby.NextPlaylistIndex].UID;
               Plugin.Instance.Log("Correct Level loaded setting next track "+nextLevelUID, 2);
               if (badTracks.ContainsKey(nextLevelUID))
               {
                   Plugin.Instance.Log("Warning next track is bad "+nextLevelUID, 2);
                   sendMessenger("BrokenTracks", "<sprite=\"Zeepkist\" name=\"Skull\"> Warning next map is a known bad track.", Plugin.Instance.warningDuration.Value);
               }
            }
        }
    }
}