﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace SpriteMaster {
	internal static class DrawState {
		private static readonly SamplerState DefaultSamplerState = SamplerState.LinearClamp;
		private static bool FetchedThisFrame = false;
		private static int RemainingTexelFetchBudget = Config.AsyncScaling.ScalingBudgetPerFrame;
		private static bool PushedUpdateThisFrame = false;
		public static TextureAddressMode CurrentAddressModeU = DefaultSamplerState.AddressU;
		public static TextureAddressMode CurrentAddressModeV = DefaultSamplerState.AddressV;
		public static Blend CurrentBlendSourceMode = BlendState.AlphaBlend.AlphaSourceBlend;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void SetCurrentAddressMode (SamplerState samplerState) {
			CurrentAddressModeU = samplerState.AddressU;
			CurrentAddressModeV = samplerState.AddressV;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool GetUpdateToken (int texels) {
			if (FetchedThisFrame && texels > RemainingTexelFetchBudget) {
				return false;
			}

			FetchedThisFrame = true;
			RemainingTexelFetchBudget -= texels;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void OnPresent () {
			if (Config.AsyncScaling.CanFetchAndLoadSameFrame || !PushedUpdateThisFrame) {
				ScaledTexture.ProcessPendingActions();
			}
			RemainingTexelFetchBudget = Config.AsyncScaling.ScalingBudgetPerFrame;
			FetchedThisFrame = false;
			PushedUpdateThisFrame = false;
		}

		internal static void OnBegin (
			SpriteBatch @this,
			SpriteSortMode sortMode,
			BlendState blendState,
			SamplerState samplerState,
			DepthStencilState depthStencilState,
			RasterizerState rasterizerState,
			Effect effect,
			Matrix transformMatrix
		) {
			SetCurrentAddressMode(samplerState);
			CurrentBlendSourceMode = blendState.AlphaSourceBlend;
		}
	}
}