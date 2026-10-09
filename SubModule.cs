using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DramalordSecrets;

public class SubModule : MBSubModuleBase
{
    private bool _isPatched = false;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        try
        {
            if (!_isPatched)
            {
                var harmony = new Harmony("com.bannerlord." + typeof(SubModule).Assembly.GetName().Name.ToLower());
                harmony.PatchAll();
                _isPatched = true;
            }
        }
        catch (Exception)
        {
            // Fail-safe
        }
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
