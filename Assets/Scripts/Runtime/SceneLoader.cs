using System.Collections;
using JRPG.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JRPG
{
    public static class SceneLoader
    {
        private const string LoadingPrefabResourcePath = "LoadingScreen";
        private static LoadingScreenController activeLoadingScreen;
        private static bool isLoading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterMenuHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void LoadAsync(string sceneName)
        {
            if (isLoading)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("SceneLoader requires a non-empty scene name.");
                return;
            }

            if (activeLoadingScreen == null)
            {
                GameObject prefab = Resources.Load<GameObject>(LoadingPrefabResourcePath);
                if (prefab == null)
                {
                    Debug.LogError(
                        "LoadingScreen prefab is missing. Run Tools > Lentera > Build Loading Screen Prefab.");
                    return;
                }

                GameObject instance = Object.Instantiate(prefab);
                activeLoadingScreen = instance.GetComponent<LoadingScreenController>();
                if (activeLoadingScreen == null)
                {
                    Object.Destroy(instance);
                    Debug.LogError("LoadingScreen prefab does not contain LoadingScreenController.");
                    return;
                }

                activeLoadingScreen.LoadingFinished += OnLoadingFinished;
            }

            isLoading = true;
            activeLoadingScreen.BeginLoading(sceneName);
            if (activeLoadingScreen == null || !activeLoadingScreen.gameObject.activeInHierarchy)
            {
                isLoading = false;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                MainMenuLoadingBridge.Install();
            }
        }

        private static void OnLoadingFinished()
        {
            isLoading = false;
        }

        private sealed class MainMenuLoadingBridge : MonoBehaviour
        {
            private MainMenuController menuController;
            private Button startButton;
            private Coroutine installRoutine;

            public static void Install()
            {
                MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>();
                if (controller == null || controller.startButton == null)
                {
                    return;
                }

                MainMenuLoadingBridge bridge = controller.GetComponent<MainMenuLoadingBridge>();
                if (bridge == null)
                {
                    bridge = controller.gameObject.AddComponent<MainMenuLoadingBridge>();
                }

                bridge.menuController = controller;
                bridge.startButton = controller.startButton;
                if (bridge.installRoutine != null)
                {
                    bridge.StopCoroutine(bridge.installRoutine);
                }

                bridge.installRoutine = bridge.StartCoroutine(bridge.InstallAfterMenuStart());
            }

            private IEnumerator InstallAfterMenuStart()
            {
                yield return null;
                if (menuController == null || startButton == null)
                {
                    yield break;
                }

                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(BeginGameLoading);
                installRoutine = null;
            }

            private void BeginGameLoading()
            {
                if (menuController.menuAudio != null)
                {
                    menuController.menuAudio.PlayClick();
                }

                string destination = string.IsNullOrWhiteSpace(menuController.firstGameScene)
                    ? "ForestPembuka"
                    : menuController.firstGameScene;
                SceneLoader.LoadAsync(destination);
            }
        }
    }
}
