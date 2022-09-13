using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [HarmonyPatch ( typeof ( BaseMountable ), "DismountPlayer" )]
    public class CanDismountEntity
    {
        public static void Prefix ()
        {
            HookExecutor.CallStaticHook ( "CanDismountEntity" );
        }
    }
}