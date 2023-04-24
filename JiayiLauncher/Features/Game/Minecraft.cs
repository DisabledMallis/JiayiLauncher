﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using JiayiLauncher.Features.Mods;
using JiayiLauncher.Features.Stats;
using JiayiLauncher.Settings;
using JiayiLauncher.Utils;

namespace JiayiLauncher.Features.Game;

public static class Minecraft
{
	private static readonly List<Mod> _modsLoaded = new();
	private static readonly Timer _timer = new(1000);

	public static List<Mod> ModsLoaded
	{
		get
		{
			if (!IsOpen) _modsLoaded.Clear();
			return _modsLoaded;
		}
	}
	
	public static bool IsOpen
	{
		get
		{
			var processes = Process.GetProcessesByName("Minecraft.Windows");
			if (processes.Length == 0) return false;
			Process = processes[0];
			return true;
		}
	}

	public static Process Process { get; private set; } = null!;

	public static async Task Open()
	{
		var minecraftApp = await PackageData.GetPackage();
		if (minecraftApp == null) return;
		await minecraftApp.LaunchAsync();
		
		Process = Process.GetProcessesByName("Minecraft.Windows")[0];
	}

	public static void TrackGameTime()
	{
		_timer.Elapsed += (_, _) =>
		{
			if (!IsOpen)
				_timer.Stop();
			else
			{
				JiayiStats.Instance!.TotalPlayTime += TimeSpan.FromSeconds(1);

				foreach (var mod in _modsLoaded)
				{
					mod.PlayTime += TimeSpan.FromSeconds(1);
				}
				
				foreach (var mod in ModCollection.Current!.Mods)
				{
					JiayiStats.Instance!.MostPlayedMod ??= mod;
					
					// get highest playtime
					if (mod.PlayTime > JiayiStats.Instance.MostPlayedMod.PlayTime)
					{
						JiayiStats.Instance.MostPlayedMod = mod;
					}
				}
			}
			
			JiayiStats.Save();
		};
		
		_timer.Start();
	}

	public static async Task WaitForModules()
	{
		await Task.Run(() =>
		{
			while (true)
			{
				Process.Refresh();
				if (JiayiSettings.Instance!.OverrideModuleRequirement)
					if (Process.Modules.Count > JiayiSettings.Instance.ModuleRequirement[2]) break;
				
				if (Process.Modules.Count > 160) break;

				if (JiayiSettings.Instance.AccelerateGameLoading)
				{
					var brokers = Process.GetProcessesByName("RuntimeBroker");
					if (brokers.Length > 0)
					{
						foreach (var broker in brokers)
						{
							broker.Kill();
						}
					}
				}

				// wait for a bit
				Task.Delay(100).Wait();
			}
		});
	}

	public static async Task<bool> ModSupported(Mod mod)
	{
		var version = await PackageData.GetVersion();
		Log.Write(nameof(Minecraft), $"Current game version is {version} and mod supports {string.Join(", ", mod.SupportedVersions)}");
		return mod.SupportedVersions.Contains(version) || mod.SupportedVersions.Contains("any version");
	}
}