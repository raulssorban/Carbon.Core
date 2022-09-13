using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( ItemContainer ), "CanAcceptItem" )]
    public class CanAcceptItem
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "CanAcceptItem" );
        }
    }
}