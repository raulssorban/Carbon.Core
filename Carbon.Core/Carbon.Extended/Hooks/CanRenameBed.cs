using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( SleepingBag ), "Rename" )]
    public class CanRenameBed
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "CanRenameBed" );
        }
    }
}