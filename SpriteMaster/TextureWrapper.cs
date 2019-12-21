﻿using Microsoft.Xna.Framework.Graphics;
using SpriteMaster.Extensions;
using SpriteMaster.Types;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpriteMaster {
	internal sealed class TextureWrapper {
		internal readonly Texture2D Reference;
		internal readonly Vector2I ReferenceSize;
		internal readonly Bounds Size;
		internal readonly Bounds IndexRectangle;
		internal readonly Vector2B Wrapped;
		internal readonly bool BlendEnabled;
		internal byte[] Data = null;
		private ulong _Hash = 0;

		private static ConditionalWeakTable<Texture2D, WeakReference<byte[]>> DataCache = new ConditionalWeakTable<Texture2D, WeakReference<byte[]>>();

		private static unsafe byte[] MakeByteArray<T>(DataRef<T> data, long referenceSize = 0) where T : unmanaged {
			try {
				if (referenceSize == 0)
					referenceSize = (long)data.Length * sizeof(T);
				var newData = new byte[referenceSize];

				fixed (T* ptr = data.Data) {
					var bytePtr = (byte*)ptr;
					foreach (var i in 0.Until(referenceSize)) {
						newData[i] = bytePtr[i];
					}
				}

				return newData;
			}
			catch (Exception ex) {
				ex.PrintInfo();
				return null;
			}
		}

		private static void Purge(Texture2D reference) {
			try {
				DataCache.Remove(reference);
			}
			catch { /* do nothing */ }
		}

		// Attempt to update the bytedata cache for the reference texture, or purge if it that makes more sense or if updating
		// is not plausible.
		internal static unsafe void Purge<T>(Texture2D reference, Bounds? bounds, DataRef<T> data) where T : unmanaged {
			if (data.IsNull) {
				Purge(reference);
				return;
			}

			var refSize = (long)reference.Area() * sizeof(T);

			bool forcePurge = false;

			try {
				if (data.Offset == 0 && data.Length >= refSize) {
					DataCache.Remove(reference);
					var newByteArray = MakeByteArray(data, refSize);
					if (newByteArray == null)
						forcePurge = true;
					else
						DataCache.Add(reference, newByteArray.MakeWeak());
				}
				else if (DataCache.TryGetValue(reference, out var weakData) && weakData.TryGetTarget(out var currentData)) {
					fixed (T *ptr = data.Data) {
						var bytePtr = (byte*)ptr;
						long untilOffset = Math.Min(currentData.Length - data.Offset, (long)data.Length * sizeof(T));
						foreach (var i in 0.Until(untilOffset)) {
							currentData[i + data.Offset] = bytePtr[i];
						}
					}
				}
				else {
					forcePurge = true;
				}
			}
			catch (Exception ex) {
				ex.PrintInfo();
				forcePurge = true;
			}

			if (forcePurge) {
				Purge(reference);
			}
		}

		internal TextureWrapper (Texture2D reference, in Bounds dimensions, in Bounds indexRectangle) {
			ReferenceSize = new Vector2I(reference);
			Size = dimensions;
			IndexRectangle = indexRectangle;
			if (Size.Bottom > ReferenceSize.Height) {
				Size.Height -= (Size.Bottom - ReferenceSize.Height);
			}
			if (Size.Right > ReferenceSize.Width) {
				Size.Width -= (Size.Right - ReferenceSize.Width);
			}
			Reference = reference;

			if (DataCache.TryGetValue(reference, out var dataRef)) {
				if (!dataRef.TryGetTarget(out Data)) {
					DataCache.Remove(reference);
				}
			}

			if (Data == null) {
				Data = new byte[reference.Width * reference.Height * 4];
				reference.GetData(Data);
				DataCache.Add(reference, Data.MakeWeak());
			}

			BlendEnabled = DrawState.CurrentBlendSourceMode != Blend.One;
			Wrapped = new Vector2B(
				DrawState.CurrentAddressModeU == TextureAddressMode.Wrap,
				DrawState.CurrentAddressModeV == TextureAddressMode.Wrap
			);
		}

		internal ulong Hash () {
			if (_Hash == 0) {
				_Hash = Data.Hash();
			}
			return _Hash;

			/*
			ulong hash = ulong.MaxValue;
			foreach (int y in Size.Top.Until(Size.Bottom))
			{
				int yOffset = y * ReferenceSize.Width;
				foreach (int x in Size.Left.Until(Size.Right))
				{
					int offset = yOffset + x;
					hash ^= Data.Hash(offset, Size.Width * sizeof(int));
				}
			}

			return hash;
			*/
		}

		internal void Dispose () {
			Data = null;
		}
	}
}
