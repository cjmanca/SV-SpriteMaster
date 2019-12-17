﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading;

using System.Reflection;
using System.Linq;
using System.Collections.Generic;

using WeakTexture = System.WeakReference<Microsoft.Xna.Framework.Graphics.Texture2D>;
using WeakScaledTexture = System.WeakReference<SpriteMaster.ScaledTexture>;
using WeakTextureMap = System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.Xna.Framework.Graphics.Texture2D, SpriteMaster.ScaledTexture>;
using WeakSpriteMap = System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.Xna.Framework.Graphics.Texture2D, System.Collections.Generic.Dictionary<ulong, SpriteMaster.ScaledTexture>>;
using SpriteMaster.Types;
using System.Runtime.InteropServices;

namespace SpriteMaster {
	// Modified from PyTK.Types.ScaledTexture2D
	// Origial Source: https://github.com/Platonymous/Stardew-Valley-Mods/blob/master/PyTK/Types/ScaledTexture2D.cs
	// Original Licence: GNU General Public License v3.0
	// Original Author: PlatonymousUpscale

	internal abstract class ITextureMap {
		protected readonly SharedLock Lock = new SharedLock();
		protected readonly List<WeakScaledTexture> ScaledTextureReferences = Config.Debug.CacheDump.Enabled ? new List<WeakScaledTexture>() : null;

		internal static ITextureMap Create () {
			return Config.Resample.DeSprite ? (ITextureMap)new SpriteMap() : (ITextureMap)new TextureMap();
		}

		internal abstract void Add (in Texture2D reference, in ScaledTexture texture, in Bounds sourceRectangle);

		internal abstract bool TryGet (in Texture2D texture, in Bounds sourceRectangle, out ScaledTexture result);

		internal abstract void Remove (in ScaledTexture scaledTexture, in Texture2D texture);

		internal abstract void Purge (in Texture2D reference, in Bounds? sourceRectangle = null);

		protected void OnAdd (in Texture2D reference, in ScaledTexture texture, in Bounds sourceRectangle) {
			if (!Config.Debug.CacheDump.Enabled)
				return;
			ScaledTextureReferences.Add(new WeakScaledTexture(texture));
		}

		protected void OnRemove (in ScaledTexture scaledTexture, in Texture2D texture) {
			if (!Config.Debug.CacheDump.Enabled)
				return;
			try {
				List<WeakScaledTexture> removeElements = new List<WeakScaledTexture>();
				foreach (var element in ScaledTextureReferences) {
					if (element.TryGetTarget(out var elementTexture)) {
						if (elementTexture == scaledTexture) {
							removeElements.Add(element);
						}
					}
					else {
						removeElements.Add(element);
					}
				}

				foreach (var element in removeElements) {
					ScaledTextureReferences.Remove(element);
				}
			}
			catch { }
		}

		internal Dictionary<Texture2D, List<ScaledTexture>> GetDump () {
			if (!Config.Debug.CacheDump.Enabled)
				return null;

			var result = new Dictionary<Texture2D, List<ScaledTexture>>();

			foreach (var element in ScaledTextureReferences) {
				if (element.TryGetTarget(out var scaledTexture)) {
					if (scaledTexture.Reference.TryGetTarget(out var referenceTexture)) {
						List<ScaledTexture> resultList;
						if (!result.TryGetValue(referenceTexture, out resultList)) {
							resultList = new List<ScaledTexture>();
							result.Add(referenceTexture, resultList);
						}
						resultList.Add(scaledTexture);
					}
				}
			}

			return result;
		}
	}

	sealed class TextureMap : ITextureMap {
		private readonly WeakTextureMap Map = new WeakTextureMap();

		internal override void Add (in Texture2D reference, in ScaledTexture texture, in Bounds sourceRectangle) {
			using (Lock.LockExclusive()) {
				Map.Add(reference, texture);
				OnAdd(reference, texture, sourceRectangle);
			}
		}

		internal override bool TryGet (in Texture2D texture, in Bounds sourceRectangle, out ScaledTexture result) {
			result = null;

			using (Lock.LockShared()) {
				if (Map.TryGetValue(texture, out var scaledTexture)) {
					if (scaledTexture.Texture != null && scaledTexture.Texture.IsDisposed) {
						Lock.Promote();
						Map.Remove(texture);
					}
					else {
						if (scaledTexture.IsReady) {
							result = scaledTexture;
						}
						return true;
					}
				}
			}

			return false;
		}

		internal override void Remove (in ScaledTexture scaledTexture, in Texture2D texture) {
			try {
				using (Lock.LockExclusive()) {
					OnRemove(scaledTexture, texture);
					Map.Remove(texture);
				}
			}
			finally {
				if (scaledTexture.Texture != null && !scaledTexture.Texture.IsDisposed) {
					Debug.InfoLn($"Disposing Active HD Texture: {scaledTexture.SafeName()}");

					//scaledTexture.Texture.Dispose();
				}
			}
		}

		internal override void Purge (in Texture2D reference, in Bounds? sourceRectangle = null) {
			try {
				using (Lock.LockShared()) {
					if (Map.TryGetValue(reference, out var scaledTexture)) {
						using (Lock.Promote()) {
							Debug.InfoLn($"Purging Texture {reference.SafeName()}");
							Map.Remove(reference);
							// TODO dispose sprite?
						}
					}
				}
			}
			catch { }
		}
	}

	sealed class SpriteMap : ITextureMap {
		private readonly WeakSpriteMap Map = new WeakSpriteMap();

		static private ulong SpriteHash (in Texture2D texture, in Bounds sourceRectangle) {
			return ScaledTexture.ExcludeSprite(texture) ? 0UL : sourceRectangle.Hash();
		}

		internal override void Add (in Texture2D reference, in ScaledTexture texture, in Bounds sourceRectangle) {
			using (Lock.LockExclusive()) {
				OnAdd(reference, texture, sourceRectangle);

				var rectangleHash = SpriteHash(reference, sourceRectangle);

				var spriteMap = Map.GetOrCreateValue(reference);
				spriteMap.Add(rectangleHash, texture);
				ScaledTextureReferences.Add(new WeakScaledTexture(texture));
			}
		}

		internal override bool TryGet (in Texture2D texture, in Bounds sourceRectangle, out ScaledTexture result) {
			result = null;

			using (Lock.LockShared()) {
				if (Map.TryGetValue(texture, out var spriteMap)) {
					var rectangleHash = SpriteHash(texture, sourceRectangle);
					if (spriteMap.TryGetValue(rectangleHash, out var scaledTexture)) {
						if (scaledTexture.Texture != null && scaledTexture.Texture.IsDisposed) {
							Lock.Promote();
							Map.Remove(texture);
						}
						else {
							if (scaledTexture.IsReady) {
								result = scaledTexture;
							}
							return true;
						}
					}
				}
			}

			return false;
		}

		internal override void Remove (in ScaledTexture scaledTexture, in Texture2D texture) {
			try {
				using (Lock.LockExclusive()) {
					OnRemove(scaledTexture, texture);
					Map.Remove(texture);
				}
			}
			finally {
				if (scaledTexture.Texture != null && !scaledTexture.Texture.IsDisposed) {
					Debug.InfoLn($"Disposing Active HD Texture: {scaledTexture.SafeName()}");

					//scaledTexture.Texture.Dispose();
				}
			}
		}

		internal override void Purge (in Texture2D reference, in Bounds? sourceRectangle = null) {
			try {
				using (Lock.LockShared()) {
					if (Map.TryGetValue(reference, out var scaledTextureMap)) {
						using (Lock.Promote()) {
							Debug.InfoLn($"Purging Texture {reference.SafeName()}");
							Map.Remove(reference);
							// TODO dispose sprites?
						}
					}
				}
			}
			catch { }
		}
	}

	internal sealed class ScaledTexture {
		// TODO : This can grow unbounded. Should fix.
		public static readonly ITextureMap TextureMap = ITextureMap.Create();

		private static readonly List<Action> PendingActions = Config.AsyncScaling.Enabled ? new List<Action>() : null;

		static internal bool ExcludeSprite (in Texture2D texture) {
			return false;// && (texture.Name == "LooseSprites\\Cursors");
		}

		static internal bool HasPendingActions () {
			if (!Config.AsyncScaling.Enabled) {
				return false;
			}
			lock (PendingActions) {
				return PendingActions.Count != 0;
			}
		}

		static internal void AddPendingAction (in Action action) {
			lock (PendingActions) {
				PendingActions.Add(action);
			}
		}

		static internal void ProcessPendingActions (int processCount = Config.AsyncScaling.MaxLoadsPerFrame) {
			if (!Config.AsyncScaling.Enabled) {
				return;
			}

			// TODO : use GetUpdateToken

			lock (PendingActions) {
				if (processCount >= PendingActions.Count) {
					foreach (var action in PendingActions) {
						action.Invoke();
					}
					PendingActions.Clear();
				}
				else {
					while (processCount-- > 0) {
						PendingActions.Last().Invoke();
						PendingActions.RemoveAt(PendingActions.Count - 1);
					}
				}
			}
		}

		static internal ScaledTexture Get (Texture2D texture, Bounds sourceRectangle, Bounds indexRectangle, bool allowPadding) {
			int textureArea = texture.Width * texture.Height;

			if (textureArea == 0 || texture.IsDisposed) {
				return null;
			}

			if (Config.IgnoreUnknownTextures && (texture.Name == null || texture.Name == "")) {
				return null;
			}

			bool LegalFormat (SurfaceFormat format) {
				switch (format) {
					case SurfaceFormat.Color:
						return true;
				}
				return false;
			}
			if (!LegalFormat(texture.Format)) {
				return null;
			}

			if (TextureMap.TryGet(texture, indexRectangle, out var scaleTexture)) {
				return scaleTexture;
			}

			if (Config.AsyncScaling.Enabled && !Patches.GetUpdateToken(textureArea)) {
				return null;
			}

			bool isSprite = Config.Resample.DeSprite && !ExcludeSprite(texture) && ((sourceRectangle.X != 0 || sourceRectangle.Y != 0) || (sourceRectangle.Width != texture.Width || sourceRectangle.Height != texture.Height));
			var textureWrapper = new TextureWrapper(texture, sourceRectangle, indexRectangle);
			ulong hash = Upscaler.GetHash(textureWrapper, isSprite);

			if (Config.EnableCachedHashTextures) {
				lock (LocalTextureCache) {
					if (LocalTextureCache.TryGetValue(hash, out var scaledTextureRef)) {
						if (scaledTextureRef.TryGetTarget(out var cachedTexture)) {
							Debug.InfoLn($"Using Cached Texture for \"{cachedTexture.SafeName()}\"");
							TextureMap.Add(texture, cachedTexture, indexRectangle);
							texture.Disposing += (object sender, EventArgs args) => { cachedTexture.OnParentDispose((Texture2D)sender); };
							if (!cachedTexture.IsReady || cachedTexture.Texture == null) {
								return null;
							}
							return cachedTexture;
						}
						else {
							LocalTextureCache.Remove(hash);
						}
					}
				}
			}

			if (TotalMemoryUsage >= Config.MaxMemoryUsage) {
				Debug.ErrorLn($"Over Max Memory Usage: {TotalMemoryUsage.AsDataSize()}");
			}

			ScaledTexture newTexture = null;
			const int scale = Config.Resample.Scale ? 2 : 1;
			newTexture = new ScaledTexture(
				assetName: texture.Name,
				textureWrapper: textureWrapper,
				source: texture,
				sourceRectangle: sourceRectangle,
				indexRectangle: indexRectangle,
				scale: scale,
				isSprite: isSprite,
				hash: hash,
				allowPadding: allowPadding
			);
			if (Config.EnableCachedHashTextures)
				lock (LocalTextureCache) {
					LocalTextureCache.Add(hash, new WeakScaledTexture(newTexture));
				}
			if (Config.AsyncScaling.Enabled) {
				// It adds itself to the relevant maps.
				if (newTexture.IsReady && newTexture.Texture != null) {
					return newTexture;
				}
				return null;
			}
			else {
				return newTexture;
			}

		}
		private static bool hackOnce = false;
		private static void TextureSizeHack (in GraphicsDevice device) {
			if (hackOnce)
				return;
			hackOnce = true;

			try {
				FieldInfo getPrivateField (in object obj, in string name) {
					return obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
				}

				var capabilitiesProperty = getPrivateField(device, "_profileCapabilities");
				var capabilities = capabilitiesProperty.GetValue(device);

				var maxTextureSizeProperty = getPrivateField(capabilities, "MaxTextureSize");
				if ((int)maxTextureSizeProperty.GetValue(capabilities) < Config.PreferredDimension) {
					maxTextureSizeProperty.SetValue(capabilities, Config.PreferredDimension);
					getPrivateField(capabilities, "MaxTextureAspectRatio").SetValue(capabilities, Config.PreferredDimension / 2);
					Config.ClampDimension = Config.PreferredDimension;
				}
			}
			catch (Exception ex) {
				Debug.ErrorLn($"Failed to hack: {ex.Message}");
			}
		}

		internal Texture2D Texture;
		internal readonly string Name;
		internal Vector2 Scale;
		internal readonly bool IsSprite;
		internal volatile bool IsReady = false;

		internal Vector2B Wrapped = new Vector2B(false);

		internal readonly WeakTexture Reference;
		internal readonly Bounds OriginalSourceRectangle;
		internal readonly ulong Hash;

		internal Vector2I Padding = Vector2I.Zero;
		internal Vector2I UnpaddedSize;
		private readonly Vector2I originalSize;
		private readonly Bounds sourceRectangle;
		private int refScale;

		internal static volatile uint TotalMemoryUsage = 0;

		internal long MemorySize {
			get {
				if (!IsReady || Texture == null) {
					return 0;
				}
				return Texture.Width * Texture.Height * sizeof(int);
			}
		}

		internal long OriginalMemorySize {
			get {
				return originalSize.Width * originalSize.Height * sizeof(int);
			}
		}

		internal static readonly Dictionary<ulong, WeakScaledTexture> LocalTextureCache = new Dictionary<ulong, WeakScaledTexture>();

		internal sealed class ManagedTexture2D : Texture2D {
			public readonly Texture2D Reference;
			public readonly ScaledTexture Texture;

			public ManagedTexture2D (ScaledTexture texture, Texture2D reference, Vector2I dimensions, SurfaceFormat format) : base(reference.GraphicsDevice, dimensions.Width, dimensions.Height, false, format) {
				Reference = reference;
				Texture = texture;
			}
		}

		internal ScaledTexture (string assetName, TextureWrapper textureWrapper, Texture2D source, Bounds sourceRectangle, Bounds indexRectangle, int scale, ulong hash, bool isSprite, bool allowPadding) {
			TextureSizeHack(source.GraphicsDevice);
			IsSprite = isSprite;
			Hash = hash;

			this.OriginalSourceRectangle = new Bounds(sourceRectangle);
			this.Reference = new WeakTexture(source);
			this.sourceRectangle = sourceRectangle;
			this.refScale = scale;
			TextureMap.Add(source, this, indexRectangle);

			this.Name = source.Name.IsBlank() ? assetName : source.Name;
			originalSize = IsSprite ? sourceRectangle.Extent : new Vector2I(source);

			if (Config.AsyncScaling.Enabled) {
				new Thread(() => {
					Thread.CurrentThread.Priority = ThreadPriority.Lowest;
					Thread.CurrentThread.Name = "Texture Resampling Thread";
					Upscaler.Upscale(
						texture: this,
						scale: ref refScale,
						input: textureWrapper,
						desprite: IsSprite,
						hash: Hash,
						wrapped: ref Wrapped,
						allowPadding: allowPadding
					);
				}).Start();
			}
			else {
				// TODO store the HD Texture in _this_ object instead. Will confuse things like subtexture updates, though.
				this.Texture = Upscaler.Upscale(
					texture: this,
					scale: ref refScale,
					input: textureWrapper,
					desprite: IsSprite,
					hash: Hash,
					wrapped: ref Wrapped,
					allowPadding: allowPadding
				);

				if (this.Texture != null) {
					Finish();
				}
			}

			// TODO : I would love to dispose of this texture _now_, but we rely on it disposing to know if we need to dispose of ours.
			// There is a better way to do this using weak references, I just need to analyze it further. Memory leaks have been a pain so far.
			source.Disposing += (object sender, EventArgs args) => { OnParentDispose((Texture2D)sender); };
		}

		// Async Call
		internal void Finish () {
			TotalMemoryUsage += Texture.SizeBytes();
			Texture.Disposing += (object sender, EventArgs args) => { TotalMemoryUsage -= Texture.SizeBytes(); };

			if (IsSprite) {
				Debug.InfoLn($"Creating HD Sprite [scale {refScale}]: {this.SafeName()} {sourceRectangle}");
			}
			else {
				Debug.InfoLn($"Creating HD Spritesheet [scale {refScale}]: {this.SafeName()}");
			}

			this.Scale = new Vector2(Texture.Width, Texture.Height) / new Vector2(originalSize.Width, originalSize.Height);

			if (Config.RestrictSize) {
				var scaledSize = originalSize * refScale;
				if (scaledSize.Height > Config.ClampDimension) {
					Scale.Y = 1f;
				}
				if (scaledSize.Width > Config.ClampDimension) {
					Scale.X = 1f;
				}
			}

			IsReady = true;
		}

		internal void Destroy (Texture2D texture) {
			TextureMap.Remove(this, texture);
		}

		private void OnParentDispose (Texture2D texture) {
			Debug.InfoLn($"Parent Texture Disposing: {texture.SafeName()}");

			Destroy(texture);
		}
	}
}
