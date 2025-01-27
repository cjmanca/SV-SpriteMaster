﻿using HarmonyLib;
using SpriteMaster.Extensions;
using SpriteMaster.Harmonize;
using SpriteMaster.Metadata;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;

namespace SpriteMaster {
	public sealed class SpriteMaster : Mod {
		public static SpriteMaster Self { get; private set; } = default;

		private static readonly bool DotNet = (Runtime.Framework == Runtime.FrameworkType.DotNET);
		private readonly Thread MemoryPressureThread = null;
		private readonly Thread GarbageCollectThread = null;
		private readonly object CollectLock = DotNet ? new() : null;
		internal static string AssemblyPath { get; private set; }

		internal static void DumpStats() {
			var currentProcess = Process.GetCurrentProcess();
			var workingSet = currentProcess.WorkingSet64;
			var vmem = currentProcess.VirtualMemorySize64;
			var gc_allocated = GC.GetTotalMemory(false);

			var lines = new List<string>();

			lines.Add("SpriteMaster Stats Dump:");
			lines.Add("\tVM:");
			lines.Add($"\t\tProcess Working Set    : {workingSet.AsDataSize()}");
			lines.Add($"\t\tProcess Virtual Memory : {vmem.AsDataSize()}:");
			lines.Add($"\t\tGC Allocated Memory    : {gc_allocated.AsDataSize()}:");
			lines.Add("");

			ManagedTexture2D.DumpStats(lines);

			foreach (var line in lines) {
				Debug.InfoLn(line);
			}
		}

		private void MemoryPressureLoop() {
			for (;;) {
				if (DrawState.TriggerGC && DrawState.TriggerGC.Wait()) {
					continue;
				}

				lock (CollectLock) {
					try {
						using var _ = new MemoryFailPoint(Config.Garbage.RequiredFreeMemory);
						Thread.Sleep(128);
					}
					catch (InsufficientMemoryException) {
						Debug.WarningLn($"Less than {(Config.Garbage.RequiredFreeMemory * 1024 * 1024).AsDataSize(decimals: 0)} available for block allocation, forcing full garbage collection");
						MTexture2D.PurgeDataCache();
						DrawState.TriggerGC.Set(true);
						Thread.Sleep(10000);
					}
				}
			}
		}

		private void GarbageCheckLoop() {
			try {
				for(;;) {
					GC.RegisterForFullGCNotification(10, 10);
					GC.WaitForFullGCApproach();
					if (Garbage.ManualCollection) {
						Thread.Sleep(128);
						continue;
					}
					lock (CollectLock) {
						if (DrawState.TriggerGC && DrawState.TriggerGC.Wait()) {
							continue;
						}

						MTexture2D.PurgeDataCache();
						DrawState.TriggerGC.Set(true);
						// TODO : Do other cleanup attempts here.
					}
				}
			}
			catch {
				
			}
		}
		
		private const string ConfigName = "config.toml";

		private static volatile string CurrentSeason = "";

		public SpriteMaster () {
			Contract.AssertNull(Self);
			Self = this;

			if (DotNet) {
				MemoryPressureThread = new Thread(MemoryPressureLoop) {
					Name = "Memory Pressure Thread",
					Priority = ThreadPriority.BelowNormal,
					IsBackground = true
				};

				GarbageCollectThread = new Thread(GarbageCheckLoop) {
					Name = "Garbage Collection Thread",
					Priority = ThreadPriority.BelowNormal,
					IsBackground = true
				};
			}
		}

		private bool IsVersionOutdated(string configVersion) {
			string referenceVersion = Config.ClearConfigBefore;

			var configStrArray = configVersion.Split('.').ToList();
			var referenceStrArray = referenceVersion.Split('.').ToList();

			try {
				while (configStrArray.Count > referenceStrArray.Count) {
					referenceStrArray.Add("0");
				}
				while (referenceStrArray.Count > configStrArray.Count) {
					configStrArray.Add("0");
				}

				foreach (int i in 0..configStrArray.Count) {
					if (configStrArray[i] == "") {
						return true;
					}

					var configElement = int.Parse(configStrArray[i]);
					var referenceElement = int.Parse(referenceStrArray[i]);

					if (configElement < referenceElement)
						return true;
				}
			}
			catch {
				return true;
			}
			return false;
		}

		public override void Entry (IModHelper help) {
			AssemblyPath = help.DirectoryPath;

			var ConfigPath = Path.Combine(help.DirectoryPath, ConfigName);

			using (var tempStream = new MemoryStream()) {
				SerializeConfig.Save(tempStream);

				if (!Config.IgnoreConfig)
					SerializeConfig.Load(ConfigPath);

				if (IsVersionOutdated(Config.ConfigVersion)) {
					Debug.WarningLn("config.toml is out of date, rewriting it.");

					SerializeConfig.Load(tempStream, retain: true);
					Config.ConfigVersion = Config.CurrentVersion;
				}
			}

			if (Config.ShowIntroMessage && !Config.SkipIntro) {
				help.Events.GameLoop.GameLaunched += (_, _1) => {
					Game1.drawLetterMessage("Welcome to SpriteMaster!\nSpriteMaster must resample sprites as it sees them and thus some lag will likely be apparent at the start of the game, upon entering new areas, and when new sprites are seen.\n\nPlease be patient and do not take this as an indication that your computer is incapable of running SpriteMaster.\n\nEnjoy!".Replace("\n", "^"));
				};
				Config.ShowIntroMessage = false;
			}

			SerializeConfig.Save(ConfigPath);

			ConfigureHarmony();
			help.Events.Input.ButtonPressed += OnButtonPressed;

			help.ConsoleCommands.Add("spritemaster_stats", "Dump SpriteMaster Statistics", (_, _1) => { DumpStats(); });
			help.ConsoleCommands.Add("spritemaster_memory", "Dump SpriteMaster Memory", (_, _1) => { Debug.DumpMemory(); });
			help.ConsoleCommands.Add("spritemaster_gc", "Trigger Spritemaster GC", (_, _1) => {
				lock (CollectLock) {
					Garbage.Collect(compact: true, blocking: true, background: false);
					DrawState.TriggerGC.Set(true);
				}
			});
			help.ConsoleCommands.Add("spritemaster_purge", "Trigger Spritemaster Purge", (_, _1) => {
				lock (CollectLock) {
					Garbage.Collect(compact: true, blocking: true, background: false);
					MTexture2D.PurgeDataCache();
					Garbage.Collect(compact: true, blocking: true, background: false);
					DrawState.TriggerGC.Set(true);
				}
			});

			//help.ConsoleCommands.Add("night", "make it dark", (_, _1) => { help.ConsoleCommands.Trigger("world_settime", new string[] { "2100" }); });

			help.Events.GameLoop.DayStarted += OnDayStarted;
			// GC after major events
			help.Events.GameLoop.SaveLoaded += (_, _1) => Garbage.Collect(compact: true, blocking: true, background: false);

			if (MemoryPressureThread != null)
				MemoryPressureThread.Start();
			if (GarbageCollectThread != null)
				GarbageCollectThread.Start();
		}

		// SMAPI/CP won't do this, so we do. Purge the cached textures for the previous season on a season change.
		private static void OnDayStarted(object _, DayStartedEventArgs _1) {
			// Do a full GC at the start of each day
			Garbage.Collect(compact: true, blocking: true, background: false);
			
			var season = SDate.Now().Season.ToLower();
			if (season != CurrentSeason) {
				CurrentSeason = season;
				ScaledTexture.SpriteMap.SeasonPurge(season);
			}
		}

		private void ConfigureHarmony() {
			var instance = new Harmony($"DigitalCarbide.${Config.ModuleName}");
			instance.ApplyPatches();
		}

		private static void OnButtonPressed (object sender, ButtonPressedEventArgs args) {
			if (args.Button == Config.ToggleButton)
				Config.Enabled = !Config.Enabled;
		}
	}
}
