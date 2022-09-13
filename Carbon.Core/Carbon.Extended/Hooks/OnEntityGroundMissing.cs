using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( DestroyOnGroundMissing ), "OnGroundMissing" )]
    public class OnEntityGroundMissing
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "OnEntityGroundMissing" );
        }
    }
}