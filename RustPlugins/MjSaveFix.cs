using System.Diagnostics;
using Harmony;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MjSaveFix", "mjmfighter", "1.0.0")]
    public class MjSaveFix : RustPlugin
    {
        private void OnServerInitialized()
        {
            BaseEntity.saveList.RemoveWhere(p => !p);
            BaseEntity.saveList.RemoveWhere(p => p == null);
            Puts("Attempted to remove nulls in save list if any were present");
        }
    }
}