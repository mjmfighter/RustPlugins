//Reference: 0Harmony

using System.Diagnostics;
using Harmony;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MjEconomicsMonitor", "mjmfighter", "1.0.0")]
    public class MjEconomicsMonitor : RustPlugin
    {
        private HarmonyInstance harmony;

        private void Init()
        {
            harmony = HarmonyInstance.Create(Name + "PATCH");
            var originalMethod = typeof(Economics).GetMethod("OnEconomicsWithdraw", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            var prefixMethod = typeof(MjEconomicsMonitor).GetMethod(nameof(WithdrawPrefix),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            harmony.Patch(originalMethod, new HarmonyMethod(prefixMethod));
        }

        private void Unload()
        {
            harmony.UnpatchAll("com.yourname.economicsmonitor");
        }

        static void WithdrawPrefix(string playerId, double amount)
        {
            // Create a new StackTrace that captures filename, line number, and column information.
            StackTrace stackTrace = new StackTrace(true);

            Interface.Oxide.LogInfo($"Economics wthdrawal detected: Player {playerId} withdrew {amount}.");

            // Iterate through the stack frames and log them
            foreach (StackFrame frame in stackTrace.GetFrames())
            {
                // Get the method that called the current method
                var method = frame.GetMethod();

                // Log the method name and other information
                Interface.Oxide.LogInfo($"Called from: {method.DeclaringType.FullName}.{method.Name}");
            }
        }
    }
}
