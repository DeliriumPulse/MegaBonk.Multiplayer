using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer
{
    public sealed class SceneDelayHelper : MonoBehaviour
    {
        public static void Run(string scene)
        {
            var go = new GameObject("SceneDelayHelper");
            DontDestroyOnLoad(go);
            var helper = go.AddComponent<SceneDelayHelper>();
            helper.sceneToLoad = scene;
            helper.Invoke(nameof(helper.LoadNow), 0.1f);
        }

        private string sceneToLoad;

        private void LoadNow()
        {
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
            MultiplayerPlugin.LogS?.LogInfo($"[NetDriver] Shim missing; directly loading scene {sceneToLoad}");
            Destroy(gameObject);
        }
    }
}
