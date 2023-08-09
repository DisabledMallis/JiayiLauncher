﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiayiLauncher.Appearance;
using JiayiLauncher.Features.Mods;
using JiayiLauncher.Utils;
using Microsoft.Win32;

namespace JiayiLauncher.Settings;

[Serializable]
public class JiayiSettings
{
	[JsonIgnore] // idk if static fields are serialized but just in case
	public static JiayiSettings? Instance { get; set; }
	
	private static string _settingsPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiayiLauncher", "settings.json");
	private static JsonSerializerOptions? _options;

	// general settings
	[Setting("Mod folder path", "General", "The path to the folder containing your mods.")]
	public string ModCollectionPath { get; set; } = string.Empty;

	// note these types of settings should probably be ignored by the json serializer
	[Setting("Export collection", "General",
		"Export your mod collection to a file. You can share this with other people.")]
	[JsonIgnore] public (string, Action) ExportCollection { get; set; } = ("Export", () =>
	{
		var dialog = new SaveFileDialog
		{
			DefaultExt = "jiayi",
			Filter = "Jiayi mod collection (*.jiayi)|*.jiayi",
			InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
			Title = "Export mod collection"
		};
		if (dialog.ShowDialog() != true) return; // this is an action why do i have to return a bool

		var path = dialog.FileName;

		if (ModCollection.Current != null) ModCollection.Current.Export(path);
	});
	
	[Setting("Profile folder path", "General", "The path to the folder containing your profiles.")]
	public string ProfileCollectionPath { get; set; } = string.Empty;
	
	[Setting("Version folder path", "General", "The path to the folder containing your game installs.")]
	public string VersionsPath { get; set; } = string.Empty;
	
	[Setting("Shader folder path", "General", "The path to the folder containing your shaders.")]
	public string ShadersPath { get; set; } = string.Empty;
	
	// appearance settings (my favorite)
	[Setting("Primary background color", "Appearance", "The primary background color of the launcher.")]
	public Color PrimaryBackgroundColor { get; set; } = Color.FromArgb(15, 15, 15);
	
	[Setting("Secondary background color", "Appearance", "The secondary background color of the launcher.")]
	public Color SecondaryBackgroundColor { get; set; } = Color.FromArgb(30, 30, 30);

	[Setting("Accent color", "Appearance", "The accent color of the launcher.")]
	public Color AccentColor { get; set; } = Color.FromArgb(220, 0, 0);
	
	[Setting("Text color", "Appearance", "The color of text seen throughout the launcher.")]
	public Color TextColor { get; set; } = Color.White;
	
	[Setting("Text color (on accent)", "Appearance", "The color of text on top of the accent color.")]
	public Color AccentTextColor { get; set; } = Color.White;
	
	[Setting("Gray text color", "Appearance", "A gray version of the text color.")]
	public Color GrayTextColor { get; set; } = Color.FromArgb(126, 126, 126);

	[Setting("Shadow distance", "Appearance", "The distance of the shadows on UI elements.")]
	public int[] ShadowDistance { get; set; } = { 0, 10, 5 };
	
	[Setting("UI movement speed", "Appearance", "The speed at which the UI moves.")]
	public float[] MovementSpeed { get; set; } = { 0, 0.5f, 0.2f };
	
	[Setting("Use background image", "Appearance", "Use an image as the background of the launcher.")]
	public bool UseBackgroundImage { get; set; } // = false;
	
	[Setting("Background image URL", "Appearance", "The URL of the image to use as the background.", 
		"UseBackgroundImage", "The URL should be a link to an image on the internet. A path to a local image will not work.")]
	public string BackgroundImageUrl { get; set; } = string.Empty;
	
	[Setting("Background blur", "Appearance", "How much to blur the background by.", "UseBackgroundImage")]
	public int[] BackgroundBlur { get; set; } = { 0, 10, 0 };
	
	[Setting("Background brightness", "Appearance", "How bright the background should be.", "UseBackgroundImage")]
	public int[] BackgroundBrightness { get; set; } = { 0, 100, 100 };
	
	[Setting("Rounding", "Appearance", "How much to round the corners of most UI elements.")]
	public int[] Rounding { get; set; } = { 0, 10, 0 };
	
	[Setting("Border color", "Appearance", "The border color seen throughout the launcher.")]
	public Color BorderColor { get; set; } = Color.FromArgb(0, 0, 0);
	
	[Setting("Border color (on accent)", "Appearance", "The color of the border around the accent color.")]
	public Color AccentBorderColor { get; set; } = Color.FromArgb(0, 0, 0);
	
	[Setting("Border thickness", "Appearance", "The thickness of the borders on UI elements.")]
	public int[] BorderThickness { get; set; } = { 0, 5, 0 };

	[Setting("Save theme", "Appearance",
		"Save changes made to your theme. Hit F5 to see it in action.")]
	[JsonIgnore] public (string, Action) SaveTheme { get; set; } = ("Save", ThemeManager.ApplyTheme);

	[Setting("Show theme", "Appearance",
		"Reveal your theme in File Explorer. You can share this with other people or use other people's themes.")]
	[JsonIgnore] public (string, Action) OpenTheme { get; set; } = ("Open", () =>
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "explorer.exe ",
			Arguments = $"/select, \"{ThemeManager.ThemePath}\"",
			UseShellExecute = true
		});
	});

	[Setting("Restore default theme", "Appearance", 
		"Go back to Jiayi's default theme. Again, press F5 to see this take effect.", confirm: true)]
	[JsonIgnore] public (string, Action) RestoreDefaultTheme { get; set; } = ("Restore", () =>
	{
		var baseSettings = new JiayiSettings();
		
		var appearanceSettings = baseSettings.GetSettingsInCategory("Appearance")
			.Select(setting => typeof(JiayiSettings).GetProperty(setting.Name));
		
		foreach (var property in appearanceSettings)
		{
			property?.SetValue(Instance, property.GetValue(baseSettings));
		}
		
		Instance!.Save();
		ThemeManager.ApplyTheme();
	});

	// discord settings
	[Setting("Enable rich presence", "Discord", "Show what you're doing in Jiayi on Discord.", tooltip: """
	Available formatting strings for rich presence:

	%mod_name% - the name of the mod you're playing with
	%game_version% - your current Minecraft version
	%mod_count% - how many mods you have in your collection
	%shader_name% - the name of the shader you're using
	%profile_count% - how many profiles you have in your collection

	Only 1 formatting string can be used per field.
	""")]
	public bool RichPresence { get; set; } = true;
	
	[Setting("Top text", "Discord", "The top-most status text.", "RichPresence")]
	public string DiscordDetails { get; set; } = "Playing with %mod_name%";
	
	[Setting("Bottom text", "Discord", "The bottom-most status text.", "RichPresence")]
	public string DiscordState { get; set; } = "on %game_version%";
	
	[Setting("Show elapsed time", "Discord", "Show how long you've been playing for.", "RichPresence")]
	public bool DiscordShowElapsedTime { get; set; } = true;
	
	[Setting("Show download button", "Discord", "Show a button to download Jiayi.", "RichPresence")]
	public bool DiscordShowDownloadButton { get; set; } // = false; (because advertising by default is bad)

	[Setting("Large image key", "Discord", "The large image key to use for rich presence.", "RichPresence")]
	public string DiscordLargeImageKey { get; set; } = "logo";
	
	[Setting("Small image key", "Discord", "The small image key to use for rich presence.", "RichPresence")]
	public string DiscordSmallImageKey { get; set; } = "minecraft";
	
	[Setting("Large image text", "Discord", "The large image text to use for rich presence.", "RichPresence")]
	public string DiscordLargeImageText { get; set; } = "Jiayi Launcher";

	[Setting("Small image text", "Discord", "The small image text to use for rich presence.", "RichPresence")]
	public string DiscordSmallImageText { get; set; } = "Minecraft for Windows";
	
	// update settings
	[Setting("Enable the updater", "Update", "Allow the launcher to check for and download updates.")]
	public bool EnableUpdater { get; set; } = true;
	
	[Setting("Automatically download updates", "Update", "Always download updates when they're available.")]
	public bool AutoDownloadUpdates { get; set; } = false;
	
	// launch settings
	[Setting("Use injection delay", "Launch", "Wait for a set amount of time instead of waiting for the game to load before injecting.")]
	public bool UseInjectionDelay { get; set; } = false;

	[Setting("Injection delay", "Launch", "The amount of time to wait before injecting, in seconds.",
		"UseInjectionDelay")]
	public int[] InjectionDelay { get; set; } = { 0, 30, 5 };
	
	[Setting("Override module requirement", "Launch",
		"Override the amount of loaded modules needed before Jiayi will inject.\n\n" +
		"If the launcher injects too early or too late, try changing this value.")]
	public bool OverrideModuleRequirement { get; set; } = false;
	
	[Setting("Module requirement", "Launch",
		"The amount of loaded modules needed before Jiayi will inject.", "OverrideModuleRequirement")]
	public int[] ModuleRequirement { get; set; } = { 150, 185, 165 };
	
	[Setting("Accelerate game loading", "Launch",
		"Speed up loading times by terminating unnecessary processes. Beware of jank.", 
		tooltip: "May cause issues related to Microsoft Store licensing (determining whether you own the game or not).")]
	public bool AccelerateGameLoading { get; set; } = false;
	
	// log settings
	[Setting("Anonymize logs", "Logs", 
		"Hides any identifying information from logs. This does not apply retroactively.")]
	public bool AnonymizeLogs { get; set; } = true;
	
	[Setting("Open log folder", "Logs", "Open the log folder.")]
	[JsonIgnore] public (string, Action) OpenLogFolder { get; set; } = ("Open", () =>
	{
		var info = new ProcessStartInfo
		{
			UseShellExecute = true,
			Verb = "open",
			FileName = Log.LogPath
		};
		
		Process.Start(info);
	});

	[Setting("Clear previous logs", "Logs", "Clear all previous log files.", confirm: true)]
	[JsonIgnore] public (string, Action) ClearPreviousLogs { get; set; } = ("Clear", () =>
	{
		var path = Path.Combine(Log.LogPath, "Previous");
		if (!Directory.Exists(path)) return;
		
		Directory.Delete(path, true);
		Directory.CreateDirectory(path);
	});

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
		
		// wipe first
		File.WriteAllText(_settingsPath, string.Empty);

		_options ??= new JsonSerializerOptions
		{
			WriteIndented = true,
			Converters = { new ColorJsonConverter() }
		};

		using var stream = File.OpenWrite(_settingsPath);
		JsonSerializer.Serialize(stream, this, _options);
		Log.Write(this, "Saved settings.");
	}

	public static void Load()
	{
		if (!File.Exists(_settingsPath))
		{
			Instance = new JiayiSettings();
			Instance.Save();
			Log.Write(Instance, "Created new settings file.");
			return;
		}

		using var stream = File.OpenRead(_settingsPath);

		try
		{
			_options ??= new JsonSerializerOptions
			{
				WriteIndented = true,
				Converters = { new ColorJsonConverter() }
			};
			
			var settings = JsonSerializer.Deserialize<JiayiSettings>(stream, _options);
			if (settings == null)
			{
				Instance = new JiayiSettings();
				Instance.Save();
				// TODO: show a notification
				Log.Write(Instance, "Settings file was corrupted or invalid. Created new settings file.");
				return;
			}

			Instance = settings;
			Log.Write(Instance, "Loaded settings.");
		}
		catch (Exception e)
		{
			stream.Close();
			Instance = new JiayiSettings();
			Instance.Save();
			Log.Write(Instance, $"Settings file was corrupted or invalid. Created new settings file. Error: {e}");
		}
	}
	
	public List<PropertyInfo> GetSettings() => GetType().GetProperties().Where(p => p.GetCustomAttribute<SettingAttribute>() != null).ToList();
	
	public PropertyInfo? GetSetting(string name) => GetSettings().FirstOrDefault(p => p.Name == name);
	
	public List<string> GetCategories() => GetSettings().Select(p => p.GetCustomAttribute<SettingAttribute>()!.Category).Distinct().ToList();
	
	public List<PropertyInfo> GetSettingsInCategory(string category) => GetSettings().Where(p => p.GetCustomAttribute<SettingAttribute>()!.Category == category).ToList();
}