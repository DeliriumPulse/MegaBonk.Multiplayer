using UnityEngine;

namespace Megabonk.Multiplayer
{
    /// <summary>
    /// Keeps NetDriverCore.Tick() running every frame (and waits until the driver exists).
    /// </summary>
    public class NetDriverCoreUpdater : MonoBehaviour
    {
        private float _nextCheck;

        private void Update()
        {
            // Wait until the multiplayer driver is actually ready
            var driver = MultiplayerPlugin.Driver;
            if (driver == null)
            {
                // Retry every 0.25s to avoid hammering logs
                if (Time.unscaledTime > _nextCheck)
                {
                    _nextCheck = Time.unscaledTime + 0.25f;
                    Megabonk.Multiplayer.MultiplayerPlugin.LogS.LogInfo("[NetDriverCoreUpdater] Waiting for driver...");
                }
                return;
            }

            // Once valid, tick it every frame
            driver.Tick();
        }
    }
}
