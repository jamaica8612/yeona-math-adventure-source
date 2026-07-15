using Antura.UI;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YeonaMathAdventure.AnturaBridge
{
    /// <summary>
    /// Keeps Antura's services alive while preventing its global overlay UI from intercepting
    /// touches in the dedicated, single-scene Math Journey APK.
    /// </summary>
    public static class MathAnturaUiIsolation
    {
        public const string ExpectedPackageIdentifier = "com.yeona.mathadventure";
        public const string MathSceneName = "math_Journey";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterFirstSceneLoad()
        {
            if (!IsDedicatedMathScene())
            {
                return;
            }

            // GlobalUI is created synchronously from AppManager.Awake. Hiding only the UI keeps
            // AppManager, DatabaseManager, TeacherAI, profile persistence and rewards running.
            GlobalUI globalUi = GlobalUI.I;
            if (globalUi != null && globalUi.gameObject.activeSelf)
            {
                globalUi.gameObject.SetActive(false);
            }

            // SceneTransitioner is instantiated as a separate DontDestroyOnLoad object.
            // Its own Awake normally hides it; this is an idempotent input/visual safeguard.
            SceneTransitioner transitioner = GlobalUI.SceneTransitioner;
            if (transitioner != null && transitioner.gameObject.activeSelf)
            {
                transitioner.gameObject.SetActive(false);
            }

            Debug.Log("[Yeona Math] Antura core services retained; Antura GlobalUI hidden for Math Journey.");
        }

        internal static bool IsDedicatedMathScene()
        {
            if (!string.Equals(Application.identifier, ExpectedPackageIdentifier, StringComparison.Ordinal))
            {
                return false;
            }

            if (SceneManager.sceneCountInBuildSettings != 1)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid()
                   && string.Equals(activeScene.name, MathSceneName, StringComparison.Ordinal);
        }
    }
}
