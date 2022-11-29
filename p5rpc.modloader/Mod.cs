﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using CriFs.V2.Hook.Interfaces;
using CriFsV2Lib.Definitions;
using FileEmulationFramework.Lib.Utilities;
using p5rpc.modloader.Patches.Common;
using p5rpc.modloader.Template;
using p5rpc.modloader.Utilities;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;

// Free perf gains, but you'll need to remember that any stackalloc isn't 0 initialized.
[module: SkipLocalsInit]

namespace p5rpc.modloader;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to this mod's common configuration.
    /// </summary>
    public static Config Configuration = null!;

    /// <summary>
    /// Assume Persona 5 Royal unless otherwise.
    /// </summary>
    public static Game Game = Game.P5R;
    
    /// <summary>
    /// Current process.
    /// </summary>
    public static Process CurrentProcess = null!;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private readonly Logger _logger;

    public Mod(ModContext context)
    {
        var modLoader = context.ModLoader;
        IReloadedHooks? hooks = context.Hooks;
        Configuration = context.Configuration;
        _logger = new Logger(context.Logger, Configuration.Common.LogLevel);
        _modConfig = context.ModConfig;

        // For more information about this template, please see
        // https://reloaded-project.github.io/Reloaded-II/ModTemplate/

        // If you want to implement e.g. unload support in your mod,
        // and some other neat features, override the methods in ModBase.
        modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);
        var scanHelper = new SigScanHelper(_logger, startupScanner);
        CurrentProcess = Process.GetCurrentProcess();
        var mainModule = CurrentProcess.MainModule;
        var baseAddr = mainModule!.BaseAddress;
        
        var patchContext = new PatchContext()
        {
            BaseAddress = baseAddr,
            Config = Configuration,
            Logger = _logger,
            Hooks = hooks!,
            ScanHelper = scanHelper
        };
        
        // Game Specific Patches
        var fileName = Path.GetFileName(mainModule.FileName);
        if (fileName.StartsWith("p5r", StringComparison.OrdinalIgnoreCase))
            Game = Game.P5R;
        else if (fileName.StartsWith("p4g", StringComparison.OrdinalIgnoreCase))
            Game = Game.P4G;
        else
            _logger.Warning("Executable name does not match any known game. Will use Persona 5 Royal profile.\n" +
                            "Consider renaming your EXE back to something that starts with 'p4g' or 'p5r'.");

        modLoader.GetController<ICriFsRedirectorApi>().TryGetTarget(out var redirectorApi);
        redirectorApi!.AddBindCallback(OnBind);
        
        if (Game == Game.P5R)
        {
            Patches.P5R.SkipIntro.Activate(patchContext);
            var criLib = redirectorApi.GetCriFsLib();
            criLib.SetDefaultEncryptionFunction(criLib.GetKnownDecryptionFunction(KnownDecryptionFunction.P5R)!);
        }
        
        // Common Patches
        NoPauseOnFocusLoss.Activate(patchContext);
    }

    private void OnBind(ICriFsRedirectorApi.BindContext context)
    {
        // TODO: File Merging Here.
    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        Configuration = configuration;
        _logger.LogLevel = Configuration.Common.LogLevel;
        _logger.Info($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion Standard Overrides

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public Mod()
    { }

#pragma warning restore CS8618

    #endregion For Exports, Serialization etc.
}

/// <summary>
/// The game we're currently running.
/// </summary>
public enum Game
{
    P4G,
    P5R
}