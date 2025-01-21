using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ZeepkistNetworking;
using ZeepSDK.Storage;

namespace BrokenTracks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    public static Plugin Instance;
    public static IModStorage storage;
    
    public ConfigEntry<bool> modEnabled;
    public ConfigEntry<bool> autoSkipEnabled;
    public ConfigEntry<float> warningDuration;
    
    public static ConfigEntry<bool> clearBrokenTracks;
    public ConfigEntry<bool> debugEnabled;

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Instance = this;
        ConfigSetup();

        storage = StorageApi.CreateModStorage(this);
        BTCore.loadData(); // load all saved data

    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }
    
    private void ConfigSetup()
    {
        modEnabled = Config.Bind("1. General", "Plugin Enabled", true, "Is the plugin currently enabled?");
        autoSkipEnabled = Config.Bind("1. General", "Auto-Skip after broken", true, "Auto-Skips to the next intened track if A05 is loaded due to a broken track");
        warningDuration = Config.Bind("1. General", "Broken Track warning duration", 5f, "How long the warning message shows up for");
        
        clearBrokenTracks = Config.Bind("9. Dev / Debug", "Delete saved broken tracks", false, "[Button] Deletes all broken track information");
        clearBrokenTracks.SettingChanged += BTCore.ClearBrokenTracks;
        debugEnabled = Config.Bind("9. Dev / Debug", "Debug Logs", false, "Provides extra output in logs for troubleshooting");
        
    }
    
    /*
     * Level 0 - info/normal
     * Level 1 - Warning
     * Level 2 - Debug
     * Level 3 - Error
     */
    public void Log(string message, int level = 0)
    {
        switch (level)
        {
            default:
            case 0:
                Logger.LogInfo(message);
                break;
            case 1:
                    Logger.LogWarning(message);
                break;
            case 2:
                if (Plugin.Instance.debugEnabled.Value)
                    Logger.LogDebug(message);
                break;
            case 3:
                Logger.LogError(message);
                break;
        }
    }
    
    [HarmonyPatch(typeof(PlaylistListItem), "DrawListItem")]
    public class SetupPlaylistDrawItem
    {
        public static void Postfix(PlaylistListItem __instance, OnlineZeeplevel newZeeplevel)
        {
            if (Plugin.Instance.modEnabled.Value)
            {
                BTCore.OnDrawPlaylistItem(__instance, newZeeplevel);
            }
        }
    }
    
    [HarmonyPatch(typeof(PlaylistMenu), "AcceptPlaylist")]
    public class SetupAcceptPlaylist
    {
        public static void Prefix(PlaylistMenu __instance)
        {
            if (Plugin.Instance.modEnabled.Value)
            {
                BTCore.OnAcceptPlaylist(__instance);
            }
        }
    }
    
    [HarmonyPatch(typeof(SetupGame), "DoStart")]
    public class SetupDoStart
    {
        public static void Postfix()
        {
            if (Plugin.Instance.modEnabled.Value)
            {
                BTCore.OnDoStart();
            }
        }
    }
}
