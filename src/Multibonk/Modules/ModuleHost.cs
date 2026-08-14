using System;
using System.Collections.Generic;
using Multibonk.Modules.Session;
using NetFacade = Multibonk.Net.Net;

namespace Multibonk.Modules
{
    /// <summary>
    /// Composition root for feature modules. Owns the single list of installed
    /// <see cref="IModule"/>s, installs them once at mod load, and fans
    /// session-start/session-end out to every module whenever the <see cref="NetFacade"/>
    /// reports one (see Net.OnSessionStarted / Net.OnSessionEnded).
    /// </summary>
    public static class ModuleHost
    {
        /// <summary>Direct handle to the session module for the dev-trigger keybinds in Mod.cs.</summary>
        public static SessionModule Session { get; } = new SessionModule();

        private static readonly List<IModule> Modules = new List<IModule> { Session };

        private static bool _installed;

        /// <summary>Idempotent. Call once from Mod.OnInitializeMelon.</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            foreach (var module in Modules)
            {
                try { module.Install(NetFacade.Handlers); }
                catch (Exception e) { Log.Error($"ModuleHost: {module.GetType().Name}.Install threw: {e}"); }
            }

            NetFacade.OnSessionStarted += isHost =>
            {
                foreach (var module in Modules)
                {
                    try { module.OnSessionStart(isHost); }
                    catch (Exception e) { Log.Error($"ModuleHost: {module.GetType().Name}.OnSessionStart threw: {e}"); }
                }
            };

            NetFacade.OnSessionEnded += () =>
            {
                foreach (var module in Modules)
                {
                    try { module.OnSessionEnd(); }
                    catch (Exception e) { Log.Error($"ModuleHost: {module.GetType().Name}.OnSessionEnd threw: {e}"); }
                }
            };

            Log.Info($"ModuleHost: installed {Modules.Count} module(s).");
        }
    }
}
