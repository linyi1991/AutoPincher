using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using AutoPincher.Bridge;
using AutoPincher.Windows;

namespace AutoPincher;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IMarketBoard MarketBoard { get; private set; } = null!;

    private const string CommandName = "/autopincher";
    private const string PinchCommandName = "/autopinch";
    private const string PinchAllCommandName = "/autopinchall";
    private const string ScanAllCommandName = "/autopinchscan";

    public static Configuration Configuration { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("AutoPincher");
    private ConfigWindow ConfigWindow { get; init; }
    private readonly PinchOverlay _pinchOverlay;

    private readonly MarketBoardListener _mbListener;
    private readonly PinchDriver _pinchDriver;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ECommonsMain.Init(PluginInterface, this);

        _mbListener = new MarketBoardListener(MarketBoard, Log);
        _pinchDriver = new PinchDriver(Log, ChatGui, _mbListener);

        ConfigWindow = new ConfigWindow(_pinchDriver);
        _pinchOverlay = new PinchOverlay(_pinchDriver);

        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "開啟 AutoPincher 降價助手設定視窗",
        });
        CommandManager.AddHandler(PinchCommandName, new CommandInfo(OnPinchCommand)
        {
            HelpMessage = "對目前開啟的僱員販售清單執行降價",
        });
        CommandManager.AddHandler(PinchAllCommandName, new CommandInfo(OnPinchAllCommand)
        {
            HelpMessage = "從目前開啟的僱員清單處理所有有上架品的僱員",
        });
        CommandManager.AddHandler(ScanAllCommandName, new CommandInfo(OnScanAllCommand)
        {
            HelpMessage = "從目前開啟的僱員清單掃描所有僱員上架品，不改價",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        Log.Information("AutoPincher loaded");
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        _pinchOverlay.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(PinchCommandName);
        CommandManager.RemoveHandler(PinchAllCommandName);
        CommandManager.RemoveHandler(ScanAllCommandName);

        _pinchDriver.Dispose();
        _mbListener.Dispose();
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();

    private void OnPinchCommand(string command, string args)
    {
        if (!Configuration.EnablePinch)
        {
            ChatGui.PrintError("[autopincher] 設定中尚未啟用。");
            return;
        }
        if (_pinchDriver.IsBusy)
        {
            ChatGui.PrintError("[autopincher] 目前正在執行。");
            return;
        }
        if (!_pinchDriver.CanPinchNow())
        {
            ChatGui.PrintError("[autopincher] 請先開啟僱員的販售清單。");
            return;
        }
        _ = Task.Run(() => _pinchDriver.RunAsync(CancellationToken.None));
    }

    private void OnPinchAllCommand(string command, string args)
    {
        if (!Configuration.EnablePinch)
        {
            ChatGui.PrintError("[autopincher] 設定中尚未啟用。");
            return;
        }
        if (_pinchDriver.IsBusy)
        {
            ChatGui.PrintError("[autopincher] 目前正在執行。");
            return;
        }
        if (!_pinchDriver.CanRunAllNow())
        {
            ChatGui.PrintError("[autopincher] 請先打開遊戲內的僱員鈴，停在僱員清單，再輸入 /autopinchall。");
            return;
        }
        ChatGui.Print("[autopincher] 已啟動全部僱員自動降價。");
        _ = Task.Run(() => _pinchDriver.RunAllAsync(CancellationToken.None));
    }

    private void OnScanAllCommand(string command, string args)
    {
        if (_pinchDriver.IsBusy)
        {
            ChatGui.PrintError("[autopincher] 目前正在執行。");
            return;
        }
        if (!_pinchDriver.CanRunAllNow())
        {
            ChatGui.PrintError("[autopincher] 請先打開遊戲內的僱員鈴，停在僱員清單，再輸入 /autopinchscan。");
            return;
        }
        _ = Task.Run(() => _pinchDriver.ScanAllAsync(CancellationToken.None));
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
