using Dalamud.Configuration;
using System;

namespace AutoPincher;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Show the "Auto Pinch" button on AutoRetainer's retainer-list controls and
    /// allow the /autopinch command. When false the plugin stays loaded but inert.
    /// </summary>
    public bool EnablePinch { get; set; } = true;

    /// <summary>
    /// Delay in milliseconds between each item's price-edit UI steps. Lower is
    /// faster but more likely to outrun the game's addon transitions.
    /// </summary>
    public int PinchPerItemDelayMs { get; set; } = 100;

    /// <summary>
    /// Minimum delay in milliseconds between consecutive in-game market-board
    /// "Compare Prices" requests. FFXIV rate-limits these server-side; ~2s is
    /// conservative. Going below risks "please wait a short while" rejections.
    /// </summary>
    public int PinchMarketBoardDelayMs { get; set; } = 2000;

    /// <summary>
    /// What to do when an item has no live competitor on the board (nobody else
    /// is selling it). When false (default) Pinch falls back to the history
    /// window and matches the most recent sale price. When true Pinch leaves the
    /// listing unchanged and moves on — being the only seller is often a chance
    /// to raise the price by hand, so don't auto-reprice it.
    /// </summary>
    public bool PinchSkipIfNoCompetitor { get; set; } = false;

    /// <summary>
    /// Skip repricing when the cheapest competitor is far below the recent sale
    /// price (or current asking price if no history arrived). This protects against
    /// bait listings such as a 3000 gil market suddenly showing one 100 gil listing.
    /// </summary>
    public bool PinchSkipSuspiciousLowCompetitor { get; set; } = true;

    /// <summary>
    /// Competitors below this percent of the reference price are treated as bait.
    /// Example: 50 means a 100 gil competitor is ignored when recent sales are 3000.
    /// </summary>
    public int PinchSuspiciousLowPercent { get; set; } = 50;

    /// <summary>
    /// Optional absolute floor for automatic repricing. 0 disables it.
    /// </summary>
    public int PinchMinimumTargetPrice { get; set; } = 0;

    /// <summary>
    /// Optional maximum amount an automatic reprice may reduce the current listing
    /// by in one pass. 0 disables it.
    /// </summary>
    public int PinchMaxDropAmount { get; set; } = 0;

    /// <summary>
    /// Pause AutoPincher when a retainer venture is already complete or will
    /// complete soon, so venture result dialogs do not interrupt price editing.
    /// </summary>
    public bool PinchPauseNearVentureCompletion { get; set; } = true;

    /// <summary>
    /// Lead time, in minutes, for the retainer venture completion guard.
    /// </summary>
    public int PinchVentureCompletionLeadMinutes { get; set; } = 2;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
