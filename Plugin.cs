// FM27ManagerPhoto: shows your own PNG as your manager's closeup portrait in Football Manager 27.
// Reads one PNG from disk, patches one game method. No networking, no processes, no registry.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using FM.UI;
using HarmonyLib;
using SI.Bindable;
using UnityEngine;

namespace FM27ManagerPhoto
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "local.fm27.managerphoto";
        public const string Name = "FM27ManagerPhoto";
        public const string Version = "1.0.2";

        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> ManagerUniqueId;
        internal static ConfigEntry<string> ManagerName;
        internal static ConfigEntry<string> PhotoFolder;
        internal static ConfigEntry<string> FileName;

        public override void Load()
        {
            Logger = Log;
            try
            {
                Setup(); // separate method: if a game update removed a type, the failure lands in this try block
            }
            catch (Exception e)
            {
                Logger.LogWarning("Plugin disabled (game update?): " + e.GetType().Name + ": " + e.Message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Setup()
        {
            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            Enabled = Config.Bind("General", "Enabled", true, "Master switch.");
            ManagerUniqueId = Config.Bind("General", "ManagerUniqueId", 0,
                "Unique id of YOUR manager. 0 = do nothing. Open your manager profile once, then look for " +
                "'closeup requested for manager id ...' in BepInEx/LogOutput.log and copy that number here.");
            ManagerName = Config.Bind("General", "ManagerName", "",
                "Experimental, optional. Only needed if the game asks for your closeup by name on some screen " +
                "(log line: 'closeup requested for manager name ...'). Leave empty otherwise.");
            PhotoFolder = Config.Bind("Photo", "Folder", pluginDir, "Folder that contains the PNG. Default: the plugin's own folder.");
            FileName = Config.Bind("Photo", "FileName", "manager.png", "PNG file name inside the folder.");

            if (!Enabled.Value) { Logger.LogInfo("Disabled in config."); return; }

            var target = typeof(ManagerCloseupShotGenerator).GetMethod(
                "SetManagerCloseupImage", BindingFlags.Public | BindingFlags.Static);
            if (target == null)
            {
                Logger.LogWarning("ManagerCloseupShotGenerator.SetManagerCloseupImage not found (game updated?). Plugin disabled.");
                return;
            }

            var harmony = new Harmony(Guid);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(CloseupPatch).GetMethod(nameof(CloseupPatch.Prefix))));

            // The by-name variant is optional: if it is missing we only lose that extra path.
            var byName = typeof(ManagerCloseupShotGenerator).GetMethod(
                "SetManagerCloseupImageByName", BindingFlags.Public | BindingFlags.Static);
            if (byName != null)
                harmony.Patch(byName, prefix: new HarmonyMethod(typeof(CloseupPatch).GetMethod(nameof(CloseupPatch.PrefixByName))));
            else
                Logger.LogWarning("SetManagerCloseupImageByName not found; the ManagerName option is unavailable.");

            string gameVersion = "?";
            try { gameVersion = Application.version; } catch { }
            Logger.LogInfo($"Ready. Plugin {Version}, game {gameVersion}, ManagerUniqueId={ManagerUniqueId.Value}");
            if (ManagerUniqueId.Value == 0 && string.IsNullOrWhiteSpace(ManagerName.Value))
                Logger.LogInfo("ManagerUniqueId is not set. Open your manager profile, find the line " +
                               "'closeup requested for manager id N' below, and put N into the config file.");
        }
    }

    internal static class CloseupPatch
    {
        private const long MaxFileBytes = 16 * 1024 * 1024;
        private const int MaxPixels = 4096;
        private const int MaxLoadAttempts = 3;   // initial load plus reloads if the game destroys the texture
        private const int MaxLoggedIds = 100;    // keeps the log and memory bounded on long saves

        private static readonly HashSet<int> SeenIds = new HashSet<int>();
        private static readonly HashSet<string> SeenNames = new HashSet<string>();
        private static Texture2D _photo;         // static reference keeps the managed wrapper alive
        private static int _loadAttempts;
        private static bool _failed;             // after any error we stop interfering with the game

        // Returns false (skips the original) only when we successfully show our own photo.
        public static bool Prefix(SIImage image, int uniqueId)
        {
            try
            {
                if (_failed || image == null) return true;

                if (SeenIds.Count < MaxLoggedIds && SeenIds.Add(uniqueId))
                    Plugin.Logger.LogInfo($"closeup requested for manager id {uniqueId}");

                int myId = Plugin.ManagerUniqueId.Value;
                if (myId == 0 || uniqueId != myId) return true;

                return !ShowPhoto(image);
            }
            catch (Exception e) { return Fail(e); }
        }

        // Same idea for the by-name variant; only active when ManagerName is set in the config.
        public static bool PrefixByName(SIImage image, string name)
        {
            try
            {
                if (_failed || image == null) return true;

                if (SeenNames.Count < MaxLoggedIds && SeenNames.Add(name ?? ""))
                    Plugin.Logger.LogInfo($"closeup requested for manager name '{name}'");

                string mine = Plugin.ManagerName.Value;
                if (string.IsNullOrWhiteSpace(mine) || !string.Equals(name, mine.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;

                return !ShowPhoto(image);
            }
            catch (Exception e) { return Fail(e); }
        }

        private static bool ShowPhoto(SIImage image)
        {
            var tex = GetPhoto();
            if (tex == null) return false;
            image.SetImage(tex);
            return true;
        }

        private static bool Fail(Exception e)
        {
            _failed = true;
            Plugin.Logger.LogWarning("Patch error, plugin stopped interfering: " + e.GetType().Name + ": " + e.Message);
            return true; // run the game's original method
        }

        private static Texture2D GetPhoto()
        {
            if (_photo != null) return _photo;            // Unity's null check also catches destroyed objects
            if (_loadAttempts >= MaxLoadAttempts) return null;
            _loadAttempts++;

            string path = Path.Combine(Plugin.PhotoFolder.Value, Plugin.FileName.Value);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                Plugin.Logger.LogWarning("Photo not found: " + path);
                return null;
            }
            if (info.Length > MaxFileBytes)
            {
                Plugin.Logger.LogWarning($"Photo is larger than {MaxFileBytes / (1024 * 1024)} MB, not loaded: {path}");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
            {
                Plugin.Logger.LogWarning("Could not decode PNG: " + path);
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            if (tex.width > MaxPixels || tex.height > MaxPixels)
            {
                Plugin.Logger.LogWarning($"Photo is {tex.width}x{tex.height}; maximum is {MaxPixels}x{MaxPixels}. Not used.");
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.hideFlags = HideFlags.HideAndDontSave; // survive scene changes and asset unloading
            _photo = tex;
            Plugin.Logger.LogInfo($"Loaded photo {tex.width}x{tex.height} from {path}");
            return _photo;
        }
    }
}
