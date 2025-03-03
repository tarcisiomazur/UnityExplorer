﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
#if CPP
using UnhollowerRuntimeLib;
#endif

namespace UnityExplorer
{
    [HarmonyPatch]
    public static partial class Attach
    {
        [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.Update))]
        public static void Prefix(TextBoxTMP __instance)
        {
            if(__instance.gameObject.name == "ExplorerBehaviour") ExplorerCore.Update();
        }
    }
    // Handles all Behaviour update calls for UnityExplorer (Update, FixedUpdate, OnPostRender).
    // Basically just a wrapper which calls the corresponding methods in ExplorerCore.

    public class ExplorerBehaviour : MonoBehaviour
    {
        internal static TextBoxTMP Instance { get; private set; }

        internal static void Setup()
        {
            var obj = new GameObject("ExplorerBehaviour");
            GameObject.DontDestroyOnLoad(obj);
            obj.hideFlags |= HideFlags.HideAndDontSave;
            Instance = obj.AddComponent<TextBoxTMP>();
        }

#if CPP
        public ExplorerBehaviour(IntPtr ptr) : base(ptr) { }
#endif

        private static bool onPostRenderFailed;

        internal void Awake()
        {
            try
            {
#if CPP
                Camera.onPostRender = Camera.onPostRender == null
                   ? new Action<Camera>(OnPostRender)
                   : Il2CppSystem.Delegate.Combine(Camera.onPostRender, 
                        (Camera.CameraCallback)new Action<Camera>(OnPostRender)).Cast<Camera.CameraCallback>();

                if (Camera.onPostRender == null || Camera.onPostRender.delegates == null)
                {
                    ExplorerCore.LogWarning("Failed to add Camera.onPostRender listener, falling back to LateUpdate instead!");
                    onPostRenderFailed = true;
                }
#else
                Camera.onPostRender += OnPostRender;
#endif
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception adding onPostRender listener: {ex.ReflectionExToString()}\r\nFalling back to LateUpdate!");
                onPostRenderFailed = true;
            }
        }

        internal void Update()
        {
            ExplorerCore.Update();
        }

        internal void FixedUpdate()
        {
            ExplorerCore.FixedUpdate();
        }

        internal void LateUpdate()
        {
            if (onPostRenderFailed)
                OnPostRender(null);
        }

        internal static void OnPostRender(Camera _)
        {
            ExplorerCore.OnPostRender();
        }
    }
}
