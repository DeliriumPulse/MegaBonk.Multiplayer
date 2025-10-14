// File: MapGenBlocker.cs
using UnityEngine;

namespace Megabonk.Multiplayer
{
    // Deprecated: Harmony patches now prevent SP systems on clients.
    public class MapGenBlocker : MonoBehaviour
    {
        private static bool _once;

        private void Start()
        {
            if (_once) return;
            _once = true;
            // Nothing to do; Patches.ApplyAll runs during plugin load.
        }
    }
}
