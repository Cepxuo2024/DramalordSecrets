using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Localization;

namespace DramalordSecrets;

[HarmonyPatch(typeof(EncyclopediaHeroPageVM), "UpdateInformationText")]
public static class EncyclopediaHeroInformationPatch
{
    public static void CleanEncyclopediaText(Hero hero)
    {
        if (hero == null) return;
        try
        {
            if (!TextObject.IsNullOrEmpty(hero.EncyclopediaText))
            {
                string text = hero.EncyclopediaText.ToString();
                if (text.Contains("Отец будущего ребёнка:") || text.Contains("Father of the unborn child:") ||
                    text.Contains("СВОДКА") || text.Contains("ЖУРНАЛ") || text.Contains("---"))
                {
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var cleanLines = lines.Where(l =>
                        !l.Contains("Отец будущего ребёнка:") &&
                        !l.Contains("Father of the unborn child:") &&
                        !l.Contains("СВОДКА") &&
                        !l.Contains("ЖУРНАЛ") &&
                        !l.Contains("---")).ToList();
                    hero.EncyclopediaText = cleanLines.Count > 0 ? new TextObject(string.Join("\n", cleanLines)) : TextObject.GetEmpty();
                }
            }
        }
        catch { }
    }

    [HarmonyPostfix]
    public static void UpdateInformationText(ref EncyclopediaHeroPageVM __instance)
    {
        try
        {
            if (__instance != null && __instance.Obj is Hero hero)
            {
                // 1. Очищаем запечённый системный текст персонажа от старых строк Unknown
                CleanEncyclopediaText(hero);

                // 2. Очищаем текущий InformationText от дубликатов
                if (!string.IsNullOrEmpty(__instance.InformationText))
                {
                    var lines = __instance.InformationText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var cleanLines = lines.Where(l =>
                        !l.Contains("Отец будущего ребёнка:") &&
                        !l.Contains("Father of the unborn child:") &&
                        !l.Contains("СВОДКА") &&
                        !l.Contains("ЖУРНАЛ") &&
                        !l.Contains("---")).ToList();
                    __instance.InformationText = string.Join("\n", cleanLines).Replace("\r", "");
                }

                // 3. Если героиня беременна и отец раскрыт (или включён чит-режим) — выводим РОВНО ОДНУ чистую строку
                if (hero.IsPregnant)
                {
                    bool isKnown = DramalordSecretsCampaignBehavior.Instance != null && 
                                   DramalordSecretsCampaignBehavior.Instance.HasPlayerDiscoveredSecret(hero);
                    bool isCheat = DramalordSecretsSettings.Instance != null && 
                                   DramalordSecretsSettings.Instance.ShowPartnerHistory;

                    if (isKnown || isCheat)
                    {
                        Hero? father = DramalordHelper.GetPregnancyFather(hero);
                        string fatherName = father != null ? father.Name.ToString() : "Неизвестен";
                        __instance.InformationText += "\nОтец будущего ребёнка: " + fatherName + ".";
                    }
                }

                // 4. Если включён чит-режим — добавляем структурированную сводку (для мужчин и женщин)
                if (DramalordSecretsSettings.Instance != null && DramalordSecretsSettings.Instance.ShowPartnerHistory)
                {
                    string summaryText = HistoryHelper.GetHeroSummaryText(hero);
                    if (!string.IsNullOrEmpty(summaryText))
                    {
                        __instance.InformationText += "\n" + summaryText;
                    }
                }

                // Финальная очистка от возможных '\r'
                if (!string.IsNullOrEmpty(__instance.InformationText))
                {
                    __instance.InformationText = __instance.InformationText.Replace("\r", "");
                }
            }
        }
        catch
        {
            // Fail-safe
        }
    }
}
