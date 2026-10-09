﻿using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace DramalordSecrets;

public class DramalordSecretsCampaignBehavior : CampaignBehaviorBase
{
    public static DramalordSecretsCampaignBehavior? Instance { get; private set; }

    private List<string> _discoveredMothers = new List<string>();

    public DramalordSecretsCampaignBehavior()
    {
        Instance = this;
    }

    public bool HasPlayerDiscoveredSecret(Hero mother)
    {
        if (mother == null) return false;
        return _discoveredMothers.Contains(mother.StringId);
    }

    public void MarkSecretDiscovered(Hero mother)
    {
        if (mother == null) return;
        if (!_discoveredMothers.Contains(mother.StringId))
        {
            _discoveredMothers.Add(mother.StringId);
        }
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.OnDailyTick));
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        PersistentHistoryTracker.Load();
        AddDialogs(starter);
    }

    private void OnDailyTick()
    {
        try
        {
            if (_discoveredMothers.Count > 0)
            {
                _discoveredMothers.RemoveAll(id =>
                {
                    Hero h = Hero.Find(id);
                    return h == null || !h.IsAlive || !h.IsPregnant;
                });
            }

            PersistentHistoryTracker.SyncFromGame();
            PersistentHistoryTracker.Save();
        }
        catch
        {
            // Ignore
        }
    }

    public override void SyncData(IDataStore dataStore)
    {
        try
        {
            dataStore.SyncData("DLS_DiscoveredMothers", ref _discoveredMothers);
        }
        catch
        {
            // Ignore
        }

        if (_discoveredMothers == null)
        {
            _discoveredMothers = new List<string>();
        }
    }

    private bool IsPregnantHero(Hero? h)
    {
        return h != null && h.IsFemale && !h.IsChild && h.IsPregnant && h.IsAlive && h != Hero.MainHero;
    }

    private bool IsPlayerWife(Hero mother)
    {
        if (mother == null) return false;
        return mother.Spouse == Hero.MainHero || DramalordHelper.IsSpouseOf(mother, Hero.MainHero);
    }

    private bool IsPlayerWifeWithPlayerChild(Hero mother)
    {
        if (!IsPregnantHero(mother)) return false;
        Hero? father = DramalordHelper.GetPregnancyFather(mother);
        return IsPlayerWife(mother) && father == Hero.MainHero;
    }

    private bool IsHusbandChild(Hero mother)
    {
        if (!IsPregnantHero(mother)) return false;
        Hero? father = DramalordHelper.GetPregnancyFather(mother);
        if (father == null || father == Hero.MainHero) return false;

        bool isHusband = (mother.Spouse != null && mother.Spouse == father) ||
                         DramalordHelper.IsSpouseOf(mother, father);
        return isHusband;
    }

    private bool IsScandalousPregnancy(Hero mother)
    {
        if (!IsPregnantHero(mother)) return false;
        if (IsPlayerWifeWithPlayerChild(mother)) return false;
        if (IsHusbandChild(mother)) return false;
        return true;
    }

    private void AddDialogs(CampaignGameStarter starter)
    {
        // =========================================================================
        // 1. Ветка выяснения тайны: "Кто отец будущего ребёнка?"
        // =========================================================================
        starter.AddPlayerLine(
            "dls_ask_father",
            "hero_main_options",
            "dls_father_response",
            "{=dls_ask_father}Я вижу твоё положение... Могу я спросить: кто отец будущего ребёнка?",
            AskFatherCondition,
            null,
            120
        );

        // Ответ: отец — сам Главный Герой
        starter.AddDialogLine(
            "dls_father_answer_player",
            "dls_father_response",
            "hero_main_options",
            "{=dls_ans_player}Как ты можешь спрашивать об этом, {PLAYER.NAME}? Это дитя от тебя! Неужели ты забыл наши ночи страсти?",
            FatherIsPlayerCondition,
            FatherIsPlayerConsequence,
            135
        );

        // Ответ: признание жены ГГ в измене
        starter.AddDialogLine(
            "dls_father_answer_confess_wife",
            "dls_father_response",
            "hero_main_options",
            "{=dls_ans_confess_wife}(Она со слезами опускает взгляд) Прости меня, {PLAYER.NAME}... Я согрешила. Настоящий отец ребёнка — {FATHER_NAME}. Молю, пощади меня и сохрани это в тайне!",
            FatherConfessWifeCondition,
            FatherConfessConsequence,
            130
        );

        // Ответ: признание замужней дамы в измене мужу
        starter.AddDialogLine(
            "dls_father_answer_confess_married",
            "dls_father_response",
            "hero_main_options",
            "{=dls_ans_confess_married}(Она тревожно оглядывается и шепчет) Никому ни слова, {PLAYER.NAME}! Мой муж думает, что ребёнок от него, но настоящий отец — {FATHER_NAME}... Надеюсь на твоё благородство и молчание.",
            FatherConfessMarriedCondition,
            FatherConfessConsequence,
            125
        );

        // Ответ: признание незамужней дамы
        starter.AddDialogLine(
            "dls_father_answer_confess_unmarried",
            "dls_father_response",
            "hero_main_options",
            "{=dls_ans_confess_unmarried}(Она смущённо оглядывается и понижает голос) Прошу, никому ни слова, {PLAYER.NAME}... Это дитя от {FATHER_NAME}. Между нами была тайная связь... Надеюсь, я могу рассчитывать на твоё благородство и молчание.",
            FatherConfessUnmarriedCondition,
            FatherConfessConsequence,
            120
        );

        // Ответ: отказ и возмущение (низкое доверие)
        starter.AddDialogLine(
            "dls_father_answer_reject",
            "dls_father_response",
            "hero_main_options",
            "{=dls_ans_reject}Какая неслыханная дерзость! Моя личная жизнь — не предмет для праздных сплетен. Не смейте совать нос не в своё дело, {PLAYER.NAME}!",
            null,
            FatherRejectConsequence,
            100
        );

        // =========================================================================
        // 2. Ветка заботы: "Как протекает твоя беременность?"
        // =========================================================================
        starter.AddPlayerLine(
            "dls_ask_how_pregnancy",
            "hero_main_options",
            "dls_pregnancy_status_response",
            "{=dls_ask_status}Как протекает твоя беременность? Всё ли благополучно?",
            AskStatusCondition,
            null,
            110
        );

        // Ответ 1: Законная жена игрока с ребёнком игрока (тепло, без тайн)
        starter.AddDialogLine(
            "dls_ans_status_player_wife",
            "dls_pregnancy_status_response",
            "hero_main_options",
            "{=dls_ans_status_player_wife}Благодарю за заботу, любимый. Мы с будущим ребёнком чувствуем себя хорошо. С нетерпением жду, когда наше дитя появится на свет!",
            StatusPlayerWifeCondition,
            null,
            130
        );

        // Ответ 2: Замужняя дама с законным ребёнком своего мужа (нормальная беременность)
        starter.AddDialogLine(
            "dls_ans_status_husband",
            "dls_pregnancy_status_response",
            "hero_main_options",
            "{=dls_ans_status_husband}Благодарю за заботу, {PLAYER.NAME}. Мы с будущим ребёнком чувствуем себя хорошо. {FATHER_NAME} окружил меня вниманием и заботой.",
            StatusHusbandCondition,
            null,
            120
        );

        // Ответ 3: Раскрытая скандальная тайна (благодарность за молчание)
        starter.AddDialogLine(
            "dls_ans_status_secret_known",
            "dls_pregnancy_status_response",
            "hero_main_options",
            "{=dls_ans_status_secret_known}Благодарю за заботу, {PLAYER.NAME}. Мы с будущим ребёнком чувствуем себя хорошо. И спасибо, что хранишь нашу с {FATHER_NAME} тайну.",
            StatusSecretKnownCondition,
            null,
            110
        );
    }

    // -------------------------------------------------------------------------
    // Условия для ветки "Кто отец?"
    // -------------------------------------------------------------------------
    private bool AskFatherCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (!IsPregnantHero(currentHero)) return false;
        if (!IsScandalousPregnancy(currentHero)) return false;
        return !HasPlayerDiscoveredSecret(currentHero);
    }

    private bool FatherIsPlayerCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null) return false;
        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        return father == Hero.MainHero;
    }

    private void FatherIsPlayerConsequence()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null) return;

        if (!HasPlayerDiscoveredSecret(currentHero))
        {
            MarkSecretDiscovered(currentHero);
            DramalordHelper.ChangeTrust(currentHero, Hero.MainHero, 5);

            PersistentHistoryTracker.RecordEncounter(currentHero, Hero.MainHero, CampaignTime.Now, "Близость", false);
            PersistentHistoryTracker.Save();

            var notif = new TextObject("{=dls_notif_player}Secret revealed: {MOTHER_NAME} is carrying your child!");
            notif.SetTextVariable("MOTHER_NAME", currentHero.Name.ToString());
            InformationManager.DisplayMessage(new InformationMessage(notif.ToString(), Colors.Magenta));
        }
    }

    private bool CheckTrust(Hero currentHero)
    {
        int relation = currentHero.GetRelation(Hero.MainHero);
        int love = DramalordHelper.GetLove(currentHero, Hero.MainHero);
        bool isSpouse = IsPlayerWife(currentHero);
        return isSpouse || relation >= 15 || love >= 10;
    }

    private bool FatherConfessWifeCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null || !IsPlayerWife(currentHero)) return false;

        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        if (father == null || father == Hero.MainHero) return false;

        if (CheckTrust(currentHero))
        {
            MBTextManager.SetTextVariable("FATHER_NAME", father.Name.ToString());
            return true;
        }
        return false;
    }

    private bool FatherConfessMarriedCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null || IsPlayerWife(currentHero)) return false;
        if (currentHero.Spouse == null) return false;

        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        if (father == null || father == currentHero.Spouse) return false;

        if (CheckTrust(currentHero))
        {
            string name = father == Hero.MainHero ? "тобой" : father.Name.ToString();
            MBTextManager.SetTextVariable("FATHER_NAME", name);
            return true;
        }
        return false;
    }

    private bool FatherConfessUnmarriedCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null || IsPlayerWife(currentHero)) return false;
        if (currentHero.Spouse != null) return false;

        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        if (father == null || father == Hero.MainHero) return false;

        if (CheckTrust(currentHero))
        {
            MBTextManager.SetTextVariable("FATHER_NAME", father.Name.ToString());
            return true;
        }
        return false;
    }

    private void FatherConfessConsequence()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null) return;

        if (!HasPlayerDiscoveredSecret(currentHero))
        {
            MarkSecretDiscovered(currentHero);

            Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
            if (father != null && father != Hero.MainHero && currentHero.Spouse != father)
            {
                DramalordHelper.SetAsLovers(currentHero, father);
            }

            DramalordHelper.ChangeTrust(currentHero, Hero.MainHero, 2);

            if (father != null)
            {
                PersistentHistoryTracker.RecordEncounter(currentHero, father, CampaignTime.Now, "Близость", true);
                PersistentHistoryTracker.Save();
            }

            string fatherName = father != null ? father.Name.ToString() : "Неизвестного";
            var notif = new TextObject("{=dls_notif_other}Secret revealed: the father of {MOTHER_NAME}'s child is {FATHER_NAME}!");
            notif.SetTextVariable("MOTHER_NAME", currentHero.Name.ToString());
            notif.SetTextVariable("FATHER_NAME", fatherName);
            InformationManager.DisplayMessage(new InformationMessage(notif.ToString(), Colors.Green));
        }
    }

    private void FatherRejectConsequence()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null) return;

        DramalordHelper.ChangeTrust(currentHero, Hero.MainHero, -3);

        var notif = new TextObject("{=dls_notif_reject}{MOTHER_NAME} deemed your question tactless (-3 relation).");
        notif.SetTextVariable("MOTHER_NAME", currentHero.Name.ToString());
        InformationManager.DisplayMessage(new InformationMessage(notif.ToString(), Colors.Red));
    }

    // -------------------------------------------------------------------------
    // Условия для ветки "Как протекает беременность?"
    // -------------------------------------------------------------------------
    private bool AskStatusCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (!IsPregnantHero(currentHero)) return false;

        if (IsPlayerWifeWithPlayerChild(currentHero)) return true;
        if (IsHusbandChild(currentHero)) return true;
        return HasPlayerDiscoveredSecret(currentHero);
    }

    private bool StatusPlayerWifeCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        return currentHero != null && IsPlayerWifeWithPlayerChild(currentHero);
    }

    private bool StatusHusbandCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null || !IsHusbandChild(currentHero)) return false;

        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        string name = father != null ? father.Name.ToString() : "Муж";
        MBTextManager.SetTextVariable("FATHER_NAME", name);
        return true;
    }

    private bool StatusSecretKnownCondition()
    {
        Hero currentHero = Hero.OneToOneConversationHero;
        if (currentHero == null || !HasPlayerDiscoveredSecret(currentHero)) return false;

        Hero? father = DramalordHelper.GetPregnancyFather(currentHero);
        string name = father != null ? (father == Hero.MainHero ? "тобой" : father.Name.ToString()) : "отцом ребёнка";
        MBTextManager.SetTextVariable("FATHER_NAME", name);
        return true;
    }
}
