﻿#if BIE
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityExplorer.Core;
using UnityExplorer.Core.Config;
using UnityExplorer.Core.Input;
using UnityExplorer.Loader.BIE;
#if CPP
using BepInEx.IL2CPP;
using UnhollowerRuntimeLib;
#endif

namespace UnityExplorer
{
    [BepInPlugin(ExplorerCore.GUID, "UnityExplorer", ExplorerCore.VERSION)]

    public class ExplorerBepInPlugin :
#if MONO
        BaseUnityPlugin
#else
        BasePlugin
#endif
        , IExplorerLoader
    {
        public static ExplorerBepInPlugin Instance;

        public ManualLogSource LogSource
#if MONO
            => Logger;
#else
            => Log;
#endif

        public ConfigHandler ConfigHandler => _configHandler;
        private BepInExConfigHandler _configHandler;

        public HarmonyLib.Harmony HarmonyInstance => s_harmony;
        private static readonly HarmonyLib.Harmony s_harmony = new HarmonyLib.Harmony(ExplorerCore.GUID);

        public string ExplorerFolder => Path.Combine(Paths.PluginPath, ExplorerCore.NAME);

        public Action<object> OnLogMessage => LogSource.LogMessage;
        public Action<object> OnLogWarning => LogSource.LogWarning;
        public Action<object> OnLogError => LogSource.LogError;

        // Init common to Mono and Il2Cpp
        internal void UniversalInit()
        {
            Instance = this;
            _configHandler = new BepInExConfigHandler();
            ExplorerCore.Init(this);
        }

#if MONO // Mono
        internal void Awake()
        {
            UniversalInit();
        }

#else   // Il2Cpp
        public override void Load()
        {
            UniversalInit();
        }
#endif

        public void SetupCursorPatches()
        {
            try
            {
                this.HarmonyInstance.PatchAll();
            }
            catch (Exception ex)
            {
                ExplorerCore.Log($"Exception setting up Harmony patches:\r\n{ex.ReflectionExToString()}");
            }
        }

        [HarmonyPatch(typeof(EventSystem), "current", MethodType.Setter)]
        public class PATCH_EventSystem_current
        {
            [HarmonyPrefix]
            public static void Prefix_EventSystem_set_current(ref EventSystem value)
            {
                CursorUnlocker.Prefix_EventSystem_set_current(ref value);
            }
        }

        [HarmonyPatch(typeof(Cursor), "lockState", MethodType.Setter)]
        public class PATCH_Cursor_lockState
        {
            [HarmonyPrefix]
            public static void Prefix_set_lockState(ref CursorLockMode value)
            {
                CursorUnlocker.Prefix_set_lockState(ref value);
            }
        }

        [HarmonyPatch(typeof(Cursor), "visible", MethodType.Setter)]
        public class PATCH_Cursor_visible
        {
            [HarmonyPrefix]
            public static void Prefix_set_visible(ref bool value)
            {
                CursorUnlocker.Prefix_set_visible(ref value);
            }
        }
    }
}
#endif