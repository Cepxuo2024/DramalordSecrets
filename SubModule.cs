using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DramalordSecrets;

public class SubModule : MBSubModuleBase
{
    private bool _isPatched = false;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        if (_isPatched) return;

        var harmony = new Harmony("com.bannerlord." + typeof(SubModule).Assembly.GetName().Name.ToLower());
        harmony.PatchAll(typeof(SubModule).Assembly);

        var target = AccessTools.Method(typeof(LogEntryHistory), "AddActionLog", new[] { typeof(LogEntry), typeof(bool) });
        var info = Harmony.GetPatchInfo(target);
        bool installed = info != null && info.Postfixes.Any(patch => patch.PatchMethod.DeclaringType == typeof(DramalordLogCapture));
        if (!installed)
        {
            throw new InvalidOperationException("DramalordSecretsDev не смог подключить перехват LogEntryHistory.AddActionLog.");
        }

        _isPatched = true;
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddBehavior(new DramalordSecretsCampaignBehavior());
        }
    }
}
