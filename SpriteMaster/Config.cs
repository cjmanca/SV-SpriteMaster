﻿using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.IO;
using TeximpNet.Compression;

namespace SpriteMaster {
	static class Config {
		internal sealed class CommentAttribute : Attribute {
			public readonly string Message;

			public CommentAttribute (string message) {
				Message = message;
			}
		}

		internal sealed class ConfigIgnoreAttribute : Attribute { }

		internal static readonly string ModuleName = typeof(Config).Namespace;

		internal static bool Enabled = true;
		internal static SButton ToggleButton = SButton.F11;

		internal const int MaxSamplers = 16;
		[ConfigIgnore]
		internal static int ClampDimension = BaseMaxTextureDimension; // this is adjustable by the system itself. The user shouldn't be able to touch it.
		[Comment("The preferred maximum texture edge length, if allowed by the hardware")]
		internal const int AbsoluteMaxTextureDimension = 16384;
		internal const int BaseMaxTextureDimension = 4096;
		internal static int PreferredMaxTextureDimension = 8192;
		internal const bool RestrictSize = false;
		internal const bool ClampInvalidBounds = true;
		internal const uint MaxMemoryUsage = 2048U * 1024U * 1024U;
		internal const bool EnableCachedHashTextures = false;
		internal const bool IgnoreUnknownTextures = false;
		internal static long ForceGarbageCompactAfter = 64 * 1024 * 1024;
		internal static long ForceGarbageCollectAfter = 128 * 1024 * 1024;
		internal static bool GarbageCollectAccountUnownedTextures = true;
		internal static bool GarbageCollectAccountOwnedTexture = true;
		internal static bool DiscardDuplicates = true;
		internal static int DiscardDuplicatesFrameDelay = 2;

		internal enum Configuration {
			Debug,
			Release
		}

#if DEBUG
		internal const Configuration BuildConfiguration = Configuration.Debug;
#else
		internal const Configuration BuildConfiguration = Configuration.Release;
#endif

		internal const bool IsDebug = BuildConfiguration == Configuration.Debug;
		internal const bool IsRelease = BuildConfiguration == Configuration.Release;

		internal static readonly string LocalRoot = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"StardewValley",
			"Mods",
			ModuleName
		);

		internal static class Debug {
			internal static class Logging {
				internal const bool LogInfo = true;
				internal static bool LogWarnings = true;
				internal static bool LogErrors = true;
				internal const bool OwnLogFile = true;
				internal const bool UseSMAPI = true;
			}

			internal static class Sprite {
				internal const bool DumpReference = false;
				internal const bool DumpResample = false;
			}
		}

		internal static class DrawState {
			internal static bool SetLinear = true;
			internal static bool EnableMSAA = true;
			internal static bool DisableDepthBuffer = true;
			internal static SurfaceFormat BackbufferFormat = SurfaceFormat.Rgba1010102;
		}

		internal static class Resample {
			internal const bool Smoothing = true;
			internal const bool Scale = Smoothing;
			internal const bool SmartScale = true;
			internal static int MaxScale = 5;
			internal static int MinimumTextureDimensions = 4;
			internal const bool DeSprite = true;
			internal const bool EnableWrappedAddressing = true;
			internal const bool UseBlockCompression = true;
			internal static CompressionQuality BlockCompressionQuality = CompressionQuality.Highest;
			internal static int BlockHardAlphaDeviationThreshold = 7;
			internal static class Padding {
				internal const bool Enabled = true;
				internal static int MinimumSizeTexels = 4;
				internal const bool IgnoreUnknown = true;
				internal static List<string> Whitelist = new List<string>() { "foo" };
				internal static List<string> Blacklist = new List<string>() { "bar" };
			}
		}

		internal static class WrapDetection {
			internal const bool Enabled = true;
			internal const float edgeThreshold = 0.4f;
			internal static byte alphaThreshold = 1;
		}

		internal static class AsyncScaling {
			internal static bool Enabled = true;
			internal static bool EnabledForUnknownTextures = false;
			internal static bool CanFetchAndLoadSameFrame = true;
			internal static int MaxLoadsPerFrame = 2;
			internal static long MinimumSizeTexels = 0;
			internal static long ScalingBudgetPerFrameTexels = 2 * 256 * 256;
			internal static int MaxInFlightTasks = 4;
		}

		internal static class Cache {
			internal static bool Enabled = true;
			internal const int LockRetries = 32;
			internal const int LockSleepMS = 32;
		}
	}
}
