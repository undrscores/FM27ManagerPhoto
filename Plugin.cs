// FM27ManagerPhoto: shows your own PNG as your manager's closeup portrait in Football Manager 27.
// Reads one PNG from disk, patches one game method. No networking, no processes, no registry.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        public const string Version = "1.0.0";

        internal static ManualLogSource Logger;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> ManagerUniqueId;
        internal static ConfigEntry<string> PhotoFolder;
        internal static ConfigEntry<string> FileName;

        public override void Load()
        {
            Logger = Log;
            try
            {
                string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                Enabled = Config.Bind("General", "Enabled", true, "Master switch.");
                ManagerUniqueId = Config.Bind("General", "ManagerUniqueId", 0,
                    "Unique id of YOUR manager. 0 = do nothing. Open your manager profile once, then look for " +
                    "'closeup requested for manager id ...' in BepInEx/LogOutput.log and copy that number here.");
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

                var prefix = new HarmonyMethod(typeof(CloseupPatch).GetMethod(nameof(CloseupPatch.Prefix)));
                new Harmony(Guid).Patch(target, prefix: prefix);
                Logger.LogInfo($"Ready. ManagerUniqueId={ManagerUniqueId.Value}");
            }
            catch (Exception e)
            {
                Logger.LogWarning("Plugin disabled, setup failed: " + e.Message);
            }
        }
    }

    internal static class CloseupPatch
    {
        private static readonly HashSet<int> SeenIds = new HashSet<int>();
        private static Texture2D _photo;       // static reference keeps the managed wrapper alive
        private static bool _loadAttempted;
        private static bool _failed;           // after any error we stop interfering with the game

        // Returns false (skips the original) only when we successfully show our own photo.
        public static bool Prefix(SIImage image, int uniqueId)
        {
            try
            {
                if (_failed || image == null) return true;

                if (SeenIds.Add(uniqueId))
                    Plugin.Logger.LogInfo($"closeup requested for manager id {uniqueId}");

                int myId = Plugin.ManagerUniqueId.Value;
                if (myId == 0 || uniqueId != myId) return true;

                var tex = GetPhoto();
                if (tex == null) return true;

                image.SetImage(tex);
                return false;
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Logger.LogWarning("Patch error, plugin stopped interfering: " + e.Message);
                return true;
            }
        }

        private static Texture2D GetPhoto()
        {
            if (_photo != null) return _photo;
            if (_loadAttempted) return null;
            _loadAttempted = true;

            string path = Path.Combine(Plugin.PhotoFolder.Value, Plugin.FileName.Value);
            if (!File.Exists(path))
            {
                Plugin.Logger.LogWarning("Photo not found: " + path);
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
            {
                Plugin.Logger.LogWarning("Could not decode PNG: " + path);
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
