using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( NPCVendingMachine ), "TakeCurrencyItem" )]
    public class OnTakeCurrencyItem [NPC]
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "OnTakeCurrencyItem [NPC]" );
        }
    }
}