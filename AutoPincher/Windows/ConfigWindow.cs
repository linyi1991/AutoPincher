using System;
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
        Size = new Vector2(420, 0);
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
        }

        string last = _driver.LastResultText;
        if (!string.IsNullOrEmpty(last))
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"上次執行：{last}");
        }
    }

    public void Dispose() { }
}
