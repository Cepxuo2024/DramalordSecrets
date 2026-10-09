using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Localization;

namespace DramalordSecrets;

[HarmonyPatch(typeof(EncyclopediaHeroPageVM), "UpdateInformationText")]
public static class EncyclopediaHeroInformationPatch
{
    /// <summary>
    /// Удаляет исключительно блоки и строки, сгенерированные нашим модом (отец ребёнка, сводка связей),
    /// не затрагивая биографию персонажа, клановую информацию и описания от других модов.
    /// Не использует общих совпадений по '---', 'СВОДКА' или 'ЖУРНАЛ'.
    /// </summary>
    public static string RemoveModContent(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        bool hasFather = text.Contains("Отец будущего ребёнка:") || text.Contains("Father of the unborn child:");
        bool hasSummary = text.Contains("СВОДКА СВЯЗЕЙ (ЧИТ") || text.Contains("\u0420\u0421\u0420'\u0420\u040E");
        bool hasLegacy = text.Contains("СВОДКА СВЯЗЕЙ (ЧИТ)") || text.Contains("ЖУРНАЛ ПОСЛЕДНИХ ВСТРЕЧ:");

        if (!hasFather && !hasSummary && !hasLegacy)
        {
            return text;
        }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var result = new List<string>(lines.Length);
        bool inSummaryBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // 1. Строки раскрытого отцовства от нашего мода
            if (trimmed.StartsWith("Отец будущего ребёнка:") || trimmed.StartsWith("Father of the unborn child:"))
            {
                continue;
            }

            // 2. Начало блока сводки или журнала нашего мода
            if (trimmed.StartsWith("СВОДКА СВЯЗЕЙ (ЧИТ") ||
                trimmed.StartsWith("\u0420\u0421\u0420'\u0420\u040E") ||
                trimmed == "ЖУРНАЛ ПОСЛЕДНИХ ВСТРЕЧ:")
            {
                inSummaryBlock = true;
                // Если перед блоком шла наша 50-символьная разделительная черта, удаляем её
                if (result.Count > 0 && result[result.Count - 1].Trim() == "--------------------------------------------------")
                {
                    result.RemoveAt(result.Count - 1);
                }
                continue;
            }

            if (inSummaryBlock)
            {
                // Завершение блока нашей сводки — строго 50-символьная черта
                if (trimmed == "--------------------------------------------------")
                {
                    // Проверяем, не следует ли следом вторая секция нашего мода (например, журнал)
                    bool nextIsSubBlock = false;
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string nextTrimmed = lines[j].Trim();
                        if (string.IsNullOrEmpty(nextTrimmed)) continue;
                        if (nextTrimmed == "ЖУРНАЛ ПОСЛЕДНИХ ВСТРЕЧ:" || nextTrimmed.StartsWith("СВОДКА СВЯЗЕЙ"))
                        {
                            nextIsSubBlock = true;
                        }
                        break;
                    }

                    if (!nextIsSubBlock)
                    {
                        inSummaryBlock = false;
                    }
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join("\n", result).TrimEnd();
    }

    /// <summary>
    /// Очищает Hero.EncyclopediaText ТОЛЬКО если туда ранее были записаны устаревшие блоки мода.
    /// Никогда не стирает оригинальные данные персонажа и не трогает чистых героев.
    /// </summary>
    public static void CleanEncyclopediaText(Hero hero)
    {
        if (hero == null) return;
        try
        {
            if (!TextObject.IsNullOrEmpty(hero.EncyclopediaText))
            {
                string raw = hero.EncyclopediaText.ToString();
                if (raw.Contains("СВОДКА СВЯЗЕЙ (ЧИТ") || raw.Contains("\u0420\u0421\u0420'\u0420\u040E"))
                {
                    string cleaned = RemoveModContent(raw);
                    hero.EncyclopediaText = !string.IsNullOrWhiteSpace(cleaned) ? new TextObject(cleaned) : TextObject.GetEmpty();
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
                // 1. Очищаем Hero.EncyclopediaText только при наличии старых запечённых меток нашего мода
                CleanEncyclopediaText(hero);

                // 2. Очищаем текущий InformationText только от предыдущих записей нашего мода
                if (!string.IsNullOrEmpty(__instance.InformationText))
                {
                    __instance.InformationText = RemoveModContent(__instance.InformationText).Replace("\r", "");
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

                // 4. Если включён чит-режим — добавляем компактную сводку
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
