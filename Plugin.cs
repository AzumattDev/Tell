using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace Tell
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class TellPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Tell";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource TellLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            LocalizationManager.Localizer.Load();

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            tellChatColor = config("2 - Display", "Color of the tell chat", new Color(1f, 0.1f, 0.5f), new ConfigDescription("The color for messages in your group."), false);
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                TellLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                TellLogger.LogError($"There was an issue loading your {ConfigFileName}");
                TellLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Color> tellChatColor = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Start))]
    public class AddRPCs
    {
        private static void Postfix()
        {
            ZRoutedRpc.instance.Register<string>("Tell AddMessage", (_, message) =>
            {
                Chat.instance.AddString(message);
                Chat.instance.m_hideTimer = 0f;
            });
            ZRoutedRpc.instance.Register<UserInfo, string>("Tell PrivateMessage", onChatMessageReceived);
        }

        private static void onChatMessageReceived(long senderId, UserInfo name, string message)
        {
            Chat.instance.AddString("<color=orange>" + name.Name + "</color>: <color=#" + ColorUtility.ToHtmlStringRGBA(TellPlugin.tellChatColor.Value) + ">" + message + "</color>");
            Chat.instance.m_hideTimer = 0f;
            ZDOID playerZDO = ZNet.instance.m_players.FirstOrDefault(p => p.m_characterID.UserID == senderId).m_characterID;
            if (playerZDO != ZDOID.None && ZNetScene.instance.FindInstance(playerZDO) is { } playerObject && playerObject.GetComponent<Player>() is { } player)
            {
                if (Minimap.instance && Player.m_localPlayer && Minimap.instance.m_mode == Minimap.MapMode.None && Vector3.Distance(Player.m_localPlayer.transform.position, player.GetHeadPoint()) > Minimap.instance.m_nomapPingDistance)
                {
                    return;
                }

                Chat.instance.AddInworldText(playerObject, senderId, player.GetHeadPoint(), Talker.Type.Normal, name, "<color=#" + ColorUtility.ToHtmlStringRGBA(TellPlugin.tellChatColor.Value) + ">" + message + "</color>");
            }
        }
    }
}