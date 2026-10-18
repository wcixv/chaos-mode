// BepInEx entry point. Only compiled into the BepInEx build (build.sh defines BEPINEX).
// The mod itself is unchanged: this just starts it the same way ModLoader does.
#if BEPINEX
using BepInEx;

namespace ChaosMode
{
    [BepInPlugin("com.bobby.chaosmode", "Chaos Mode", "1.2.2")]
    public class ChaosModeBepInEx : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Chaos Mode loaded. Press F2 in game.");
            ModMain.OnGameReady();
        }
    }
}
#endif
