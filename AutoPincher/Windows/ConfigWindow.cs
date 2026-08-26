using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AutoPincher.Bridge;

namespace AutoPincher.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly PinchDriver _driver;

    public ConfigWindow(PinchDriver driver) : base("AutoPincher 降價助手###autopincher-config")
    {
        _driver = driver;
        Size = new Vector2(680, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var cfg = Plugin.Configuration;

        ImGui.TextWrapped(
            "依市場看板即時最低競爭價格，將每筆僱員上架品調成低 1 gil。自己的僱員與住宅模特兒不會被當成競爭者。" +
            "全程本機處理，不會把資料送到外部服務。");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        bool enable = cfg.EnablePinch;
        if (ImGui.Checkbox("啟用 AutoPincher", ref enable))
        {
            cfg.EnablePinch = enable;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("關閉時，自動降價按鈕與 /autopinch 指令不會執行。");

        int delayMs = cfg.PinchPerItemDelayMs;
        if (ImGui.SliderInt("每項操作延遲 (ms)##itemdelay", ref delayMs, 50, 2000))
        {
            cfg.PinchPerItemDelayMs = delayMs;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("調整價格 UI 步驟之間的延遲。越低越快，但越容易卡 UI 或被限制。");

        int mbDelay = cfg.PinchMarketBoardDelayMs;
        if (ImGui.SliderInt("市場查價間隔 (ms)##mbdelay", ref mbDelay, 500, 5000))
        {
            cfg.PinchMarketBoardDelayMs = mbDelay;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("每次「比較價格」查詢之間的最短間隔。\nFFXIV 會限制查價頻率，約 2 秒較穩。");

        bool skipNoComp = cfg.PinchSkipIfNoCompetitor;
        if (ImGui.Checkbox("沒有即時競爭者時略過", ref skipNoComp))
        {
            cfg.PinchSkipIfNoCompetitor = skipNoComp;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "當市場上沒有其他人販售該物品時：\n" +
                "  關閉（預設）：改成歷史成交紀錄裡最近一次的價格。\n" +
                "  開啟：不更動價格，保留手動拉高價格的機會。");

        bool skipLow = cfg.PinchSkipSuspiciousLowCompetitor;
        if (ImGui.Checkbox("略過疑似惡意低價", ref skipLow))
        {
            cfg.PinchSkipSuspiciousLowCompetitor = skipLow;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "當最低競爭價明顯低於最近成交價時不跟價，避免 3000 gil 的市場被 100 gil 釣魚單打到 99。\n" +
                "沒有最近成交價時，會用你目前上架價當參考。");

        int lowPercent = Math.Clamp(cfg.PinchSuspiciousLowPercent, 1, 99);
        bool changedLowPercent = ImGui.SliderInt("低價判定比例 (%)##lowpercent", ref lowPercent, 1, 99);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        changedLowPercent |= ImGui.InputInt("##lowpercentinput", ref lowPercent, 1, 5);
        if (changedLowPercent)
        {
            cfg.PinchSuspiciousLowPercent = Math.Clamp(lowPercent, 1, 99);
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("最低競爭價低於參考價格的這個比例時會略過。預設 50%，例如最近成交 3000，低於 1500 就不自動改價。");

        int minPrice = cfg.PinchMinimumTargetPrice;
        if (ImGui.InputInt("自動改價最低價##minprice", ref minPrice, 100, 1000))
        {
            cfg.PinchMinimumTargetPrice = Math.Max(0, minPrice);
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("自動降價不會改到低於這個價格；0 表示不使用固定最低價。");

        int maxDrop = cfg.PinchMaxDropAmount;
        if (ImGui.InputInt("單次最大降價差額##maxdrop", ref maxDrop, 10, 100))
        {
            cfg.PinchMaxDropAmount = Math.Max(0, maxDrop);
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "限制每次自動改價最多比你目前價格低多少；0 表示不限制。\n" +
                "例：你目前 1000、最低競爭價 800、這裡設 100，原本目標 799 會改成 900。");

        bool pauseNearVenture = cfg.PinchPauseNearVentureCompletion;
        if (ImGui.Checkbox("探險快完成時暫停", ref pauseNearVenture))
        {
            cfg.PinchPauseNearVentureCompletion = pauseNearVenture;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("執行前若有僱員探險已完成或即將完成，AutoPincher 會先停止並在聊天窗告知，避免回報視窗打斷降價流程。");

        int ventureLead = Math.Clamp(cfg.PinchVentureCompletionLeadMinutes, 1, 10);
        bool changedVentureLead = ImGui.SliderInt("探險完成保護時間 (分)##venturelead", ref ventureLead, 1, 10);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        changedVentureLead |= ImGui.InputInt("##ventureleadinput", ref ventureLead, 1, 1);
        if (changedVentureLead)
        {
            cfg.PinchVentureCompletionLeadMinutes = Math.Clamp(ventureLead, 1, 60);
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("預設 2 分鐘。若任何僱員探險剩餘時間小於等於此值，AutoPincher 會暫停，讓你先回報並重新派遣。");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(
            "先開啟某個僱員的販售清單，再按下方按鈕；或在 AutoRetainer 的僱員列表控制區使用「自動降價」按鈕，一次跑完全部僱員。");
        ImGui.Spacing();

        if (_driver.IsBusy)
        {
            if (ImGui.Button("取消##cfgcancel"))
                _driver.AbortAll();
        }
        else
        {
            bool canPinch = cfg.EnablePinch && _driver.CanPinchNow();
            if (!canPinch) ImGui.BeginDisabled();
            if (ImGui.Button("立即處理目前僱員"))
                _ = Task.Run(() => _driver.RunAsync(CancellationToken.None));
            if (!canPinch) ImGui.EndDisabled();
            if (!_driver.CanPinchNow() && ImGui.IsItemHovered())
                ImGui.SetTooltip("請先開啟僱員的販售清單。");

            ImGui.SameLine();
            if (ImGui.Button("處理全部僱員##pinchall"))
            {
                Plugin.ChatGui.Print("[autopincher] 已點擊處理全部僱員，正在檢查僱員鈴狀態。");
                _ = Task.Run(() => _driver.RunAllAsync(CancellationToken.None));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_driver.CanRunAllNow()
                    ? "從目前開啟的遊戲僱員鈴清單，逐一處理所有有上架品的僱員。"
                    : "點擊後會檢查遊戲原生僱員鈴清單。\n若尚未打開僱員鈴，聊天窗會提示你先打開。");

            ImGui.SameLine();
            if (ImGui.Button("掃描全部僱員##scanall"))
                _ = Task.Run(() => _driver.ScanAllAsync(CancellationToken.None));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_driver.CanRunAllNow()
                    ? "只掃描所有僱員目前上架品並更新總覽，不查市場、不改價。"
                    : "請先打開遊戲原生僱員鈴清單。");
        }

        string last = _driver.LastResultText;
        if (!string.IsNullOrEmpty(last))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"上次執行：{last}");
        }

        DrawListingSnapshot();

        var records = _driver.RecentReprices;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted($"最近改價紀錄（{records.Length}）");
        ImGui.SameLine();
        if (ImGui.SmallButton("清除##clear-reprice-records"))
            _driver.ClearRecentReprices();

        if (records.Length == 0)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "尚無改價紀錄。");
            return;
        }

        if (ImGui.BeginTable("##recent-reprice-table", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, 180)))
        {
            ImGui.TableSetupColumn("時間", ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableSetupColumn("商品");
            ImGui.TableSetupColumn("舊價", ImGuiTableColumnFlags.WidthFixed, 64);
            ImGui.TableSetupColumn("新價", ImGuiTableColumnFlags.WidthFixed, 64);
            ImGui.TableSetupColumn("原因", ImGuiTableColumnFlags.WidthFixed, 82);
            ImGui.TableHeadersRow();

            foreach (var r in records)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.Time.ToString("HH:mm:ss"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.Hq ? $"{r.ItemName} HQ" : r.ItemName);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.OldPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.NewPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.Reason);
            }

            ImGui.EndTable();
        }
    }

    private void DrawListingSnapshot()
    {
        var listings = _driver.ListingSnapshot
            .OrderBy(row => row.RetainerName)
            .ThenBy(row => row.ItemName)
            .ThenByDescending(row => row.Hq)
            .ToArray();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var totalQty = listings.Aggregate(0UL, (sum, row) => sum + row.Quantity);
        var totalValue = listings.Aggregate(0UL, (sum, row) => sum + (ulong)row.Quantity * row.UnitPrice);
        ImGui.TextUnformatted($"目前上架總覽（{listings.Length} 筆，{totalQty:N0} 個，估值 {totalValue:N0} gil）");
        ImGui.SameLine();
        if (ImGui.SmallButton("複製 TSV##copy-listings"))
            ImGui.SetClipboardText(ToTsv(listings));
        ImGui.SameLine();
        if (ImGui.SmallButton("清除##clear-listings"))
            _driver.ClearListingSnapshot();

        if (listings.Length == 0)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "尚無上架快照。執行「處理全部僱員」後會更新。");
            return;
        }

        if (ImGui.BeginTable("##listing-snapshot-table", 6,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, 220)))
        {
            ImGui.TableSetupColumn("僱員", ImGuiTableColumnFlags.WidthFixed, 82);
            ImGui.TableSetupColumn("商品");
            ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 34);
            ImGui.TableSetupColumn("數量", ImGuiTableColumnFlags.WidthFixed, 58);
            ImGui.TableSetupColumn("單價", ImGuiTableColumnFlags.WidthFixed, 82);
            ImGui.TableSetupColumn("小計", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableHeadersRow();

            foreach (var row in listings)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.RetainerName);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.ItemName);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Hq ? "HQ" : "");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Quantity.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.UnitPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(((ulong)row.Quantity * row.UnitPrice).ToString("N0"));
            }

            ImGui.EndTable();
        }
    }

    private static string ToTsv(PinchDriver.ListingRecord[] listings)
    {
        static string Clean(string value)
            => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        var lines = listings.Select(row =>
            string.Join('\t',
                row.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                Clean(row.RetainerName),
                Clean(row.ItemName),
                row.Hq ? "HQ" : "",
                row.Quantity.ToString(),
                row.UnitPrice.ToString(),
                ((ulong)row.Quantity * row.UnitPrice).ToString()));

        return "Time\tRetainer\tItem\tHQ\tQuantity\tUnitPrice\tTotal\n" + string.Join('\n', lines);
    }

    public void Dispose() { }
}
