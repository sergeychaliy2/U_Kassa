using UnityEngine;
using UnityEngine.SceneManagement;

namespace UKassaDemo
{
    public static class UKassaDemoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Debug.Log("[UKassaDemoBootstrap] Init()");
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryEnsureController(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[UKassaDemoBootstrap] Scene loaded: name={scene.name}, path={scene.path}, mode={mode}");
            TryEnsureController(scene);
        }

        private static void TryEnsureController(Scene scene)
        {
            var isTargetScene =
                scene.name == "UKassa" ||
                (!string.IsNullOrWhiteSpace(scene.path) && scene.path.EndsWith("/UKassa.unity"));

            if (!isTargetScene)
            {
                Debug.Log($"[UKassaDemoBootstrap] Skip scene '{scene.name}' (target is 'UKassa').");
                return;
            }

            if (Object.FindFirstObjectByType<UKassaShopDemoController>() != null)
            {
                Debug.Log("[UKassaDemoBootstrap] Controller already exists in scene.");
                return;
            }

            var go = new GameObject("UKassaShopDemoController");
            go.AddComponent<UKassaShopDemoController>();
            Debug.Log("[UKassaDemoBootstrap] Controller created.");
        }
    }
}
