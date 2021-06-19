﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityExplorer.Core;
using UnityExplorer.Core.Runtime;

namespace UnityExplorer.UI.ObjectExplorer
{
    public enum SearchContext
    {
        UnityObject,
        // GameObject,
        Singleton,
        StaticClass
    }

    public enum ChildFilter
    {
        Any,
        RootObject,
        HasParent
    }

    public enum SceneFilter
    {
        Any,
        ActivelyLoaded,
        DontDestroyOnLoad,
        HideAndDontSave,
    }

    public static class SearchProvider
    {

        private static bool Filter(Scene scene, SceneFilter filter)
        {
            switch (filter)
            {
                case SceneFilter.Any:
                    return true;
                case SceneFilter.DontDestroyOnLoad:
                    return scene == SceneHandler.DontDestroyScene;
                case SceneFilter.HideAndDontSave:
                    return scene == default;
                case SceneFilter.ActivelyLoaded:
                    return scene != SceneHandler.DontDestroyScene && scene != default;
                default:
                    return false;
            }
        }

        internal static List<object> UnityObjectSearch(string input, string customTypeInput, SearchContext context,
            ChildFilter childFilter, SceneFilter sceneFilter)
        {
            var results = new List<object>();

            Type searchType = null;
            if (!string.IsNullOrEmpty(customTypeInput))
            {
                if (ReflectionUtility.GetTypeByName(customTypeInput) is Type customType)
                {
                    if (typeof(UnityEngine.Object).IsAssignableFrom(customType))
                        searchType = customType;
                    else
                        ExplorerCore.LogWarning($"Custom type '{customType.FullName}' is not assignable from UnityEngine.Object!");
                }
                else
                    ExplorerCore.LogWarning($"Could not find any type by name '{customTypeInput}'!");
            }

            if (searchType == null)
                searchType = typeof(UnityEngine.Object);

            var allObjects = RuntimeProvider.Instance.FindObjectsOfTypeAll(searchType);

            // perform filter comparers

            string nameFilter = null;
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            bool shouldFilterGOs = searchType == typeof(GameObject) || typeof(Component).IsAssignableFrom(searchType);

            foreach (var obj in allObjects)
            {
                // name check
                if (!string.IsNullOrEmpty(nameFilter) && !obj.name.ContainsIgnoreCase(nameFilter))
                    continue;

                GameObject go = null;
                var type = obj.GetActualType();

                if (type == typeof(GameObject))
                    go = obj.TryCast<GameObject>();
                else if (typeof(Component).IsAssignableFrom(type))
                    go = obj.TryCast<Component>()?.gameObject;

                if (go)
                {
                    // hide unityexplorer objects
                    if (go.transform.root.name == "ExplorerCanvas")
                        continue;

                    if (shouldFilterGOs)
                    {
                        // scene check
                        if (sceneFilter != SceneFilter.Any)
                        {
                            if (!Filter(go.scene, sceneFilter))
                                continue;
                        }

                        if (childFilter != ChildFilter.Any)
                        {
                            if (!go)
                                continue;

                            // root object check (no parent)
                            if (childFilter == ChildFilter.HasParent && !go.transform.parent)
                                continue;
                            else if (childFilter == ChildFilter.RootObject && go.transform.parent)
                                continue;
                        }
                    }
                }

                results.Add(obj);
            }

            return results;
        }

        internal static List<object> StaticClassSearch(string input)
        {
            var list = new List<object>();

            var nameFilter = "";
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in asm.TryGetTypes().Where(it => it.IsSealed && it.IsAbstract))
                {
                    if (!string.IsNullOrEmpty(nameFilter) && !type.FullName.ContainsIgnoreCase(nameFilter))
                        continue;

                    list.Add(type);
                }
            }

            return list;
        }

        internal static string[] instanceNames = new string[]
        {
            "m_instance",
            "m_Instance",
            "s_instance",
            "s_Instance",
            "_instance",
            "_Instance",
            "instance",
            "Instance",
            "<Instance>k__BackingField",
            "<instance>k__BackingField",
        };

        internal static List<object> SingletonSearch(string input)
        {
            var instances = new List<object>();

            var nameFilter = "";
            if (!string.IsNullOrEmpty(input))
                nameFilter = input;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Search all non-static, non-enum classes.
                foreach (var type in asm.TryGetTypes().Where(it => !(it.IsSealed && it.IsAbstract) && !it.IsEnum))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(nameFilter) && !type.FullName.ContainsIgnoreCase(nameFilter))
                            continue;

                        ReflectionUtility.FindSingleton(instanceNames, type, flags, instances);
                    }
                    catch { }
                }
            }

            return instances;
        }

    }
}
