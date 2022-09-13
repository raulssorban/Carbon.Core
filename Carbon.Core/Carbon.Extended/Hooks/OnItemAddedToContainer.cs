using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( ItemContainer ), "Insert" )]
    public class OnItemAddedToContainer
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "OnItemAddedToContainer" );
        }
    }
}