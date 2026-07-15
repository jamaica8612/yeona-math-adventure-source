#if UNITY_EDITOR
using Antura.Core;
using Antura.Discover;
using Antura.Discover.Audio;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace YeonaMathAdventure.Editor
{
    /// <summary>
    /// Installs Antura's real AppManager prefab into the generated Math Journey scene while
    /// disabling Discover/test-only behaviours that the standalone math APK does not use.
    /// Failure is non-fatal by design: MathAnturaBridge remains local-first.
    /// </summary>
    public static class AnturaAppManagerSceneInstaller
    {
        public const string AppManagerPrefabPath =
            "Assets/Resources/Prefabs/Managers/AppManager.prefab";

        public static bool TryInstallCoreServices(Scene scene, out string statusCode)
        {
            try
            {
                return TryInstallCoreServicesUnchecked(scene, out statusCode);
            }
            catch (Exception exception)
            {
                statusCode = "app_manager_install_unhandled_exception";
                Debug.LogWarning(
                    "[Yeona Math] Antura startup preflight failed; local-first play remains available. " +
                    exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        private static bool TryInstallCoreServicesUnchecked(Scene scene, out string statusCode)
        {
            statusCode = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                statusCode = "invalid_or_unloaded_scene";
                return false;
            }

            AppManager existing = FindSceneAppManager(scene);
            if (existing != null)
            {
                return TryConfigureCoreOnly(existing.gameObject, out statusCode);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppManagerPrefabPath);
            if (prefab == null)
            {
                statusCode = "app_manager_prefab_missing";
                Debug.LogWarning(
                    $"[Yeona Math] Antura AppManager prefab was not found at '{AppManagerPrefabPath}'. " +
                    "Continuing with the local math profile only.");
                return false;
            }

            AppManager prefabManager = prefab.GetComponent<AppManager>();
            if (prefabManager == null || prefabManager.RootConfig == null)
            {
                statusCode = "app_manager_prefab_invalid";
                Debug.LogWarning(
                    "[Yeona Math] Antura AppManager prefab is missing AppManager or RootConfig. " +
                    "Continuing with the local math profile only.");
                return false;
            }

            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    statusCode = "app_manager_prefab_instantiate_failed";
                    return false;
                }

                instance.name = "[Antura Core AppManager - Math]";
                if (!TryConfigureCoreOnly(instance, out statusCode))
                {
                    Object.DestroyImmediate(instance);
                    return false;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                statusCode = "antura_core_services_installed";
                Debug.Log(
                    "[Yeona Math] Installed Antura AppManager core services. " +
                    "Discover managers and BotTester are disabled for the standalone math scene.");
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                statusCode = "app_manager_prefab_install_exception";
                Debug.LogWarning(
                    "[Yeona Math] Could not install Antura AppManager; local-first play remains available. " +
                    exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        private static AppManager FindSceneAppManager(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                AppManager manager = roots[index].GetComponentInChildren<AppManager>(true);
                if (manager != null)
                {
                    return manager;
                }
            }

            return null;
        }

        private static bool TryConfigureCoreOnly(GameObject instance, out string statusCode)
        {
            AppManager appManager = instance.GetComponent<AppManager>();
            if (appManager == null || appManager.RootConfig == null)
            {
                statusCode = "app_manager_instance_invalid";
                return false;
            }

            appManager.enabled = true;
            DisableAll(instance.GetComponentsInChildren<BotTester>(true));
            DisableAll(instance.GetComponentsInChildren<DiscoverAppManager>(true));
            DisableAll(instance.GetComponentsInChildren<DiscoverDataManager>(true));
            DisableAll(instance.GetComponentsInChildren<DiscoverAudioManager>(true));

            PrefabUtility.RecordPrefabInstancePropertyModifications(appManager);
            EditorUtility.SetDirty(instance);
            statusCode = "antura_core_services_configured";
            return true;
        }

        private static void DisableAll<T>(T[] behaviours) where T : Behaviour
        {
            for (int index = 0; index < behaviours.Length; index++)
            {
                T behaviour = behaviours[index];
                if (behaviour == null)
                {
                    continue;
                }

                behaviour.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                EditorUtility.SetDirty(behaviour);
            }
        }
    }
}
#endif
