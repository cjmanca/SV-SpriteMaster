﻿using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using TeximpNet;
using TeximpNet.Unmanaged;

namespace SpriteMaster.HarmonyExt.Patches {
	[SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Harmony")]
	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Harmony")]
	internal static class PlatformHelper {

		//[DllImport("__Internal")]
		//private static extern IntPtr dlerror (String fileName, int flags);

		static PlatformHelper () {
			if (Runtime.IsLinux) {
				// This needs to be done because Debian-based systems don't always have a libdl.so, and instead have libdl.so.2.
				// We need to determine which libdl we actually need to talk to.
				var dlTypes = new Type[] {
					typeof(libdlbase),
					typeof(libdl2),
					typeof(libdl227),
					typeof(libdl1)
				};

				foreach (var dlType in dlTypes) {
					var newDL = (libdl)Activator.CreateInstance(dlType);
					try {
						newDL.error();
					}
					catch {
						Debug.TraceLn($"Failed DL: {dlType}");
						continue;
					}
					dl = newDL;
					Debug.TraceLn($"New DL: {dlType}");
					break;
				}

				if (dl == null) {
					Debug.ErrorLn("A valid libdl could not be found.");
					throw new NotSupportedException("A valid libdl could not be found.");
				}
			}
		}

		/*
[DllImport("__Internal", CharSet = CharSet.Ansi)]
private static extern void mono_dllmap_insert(IntPtr assembly, string dll, string func, string tdll, string tfunc);

// and then somewhere:
mono_dllmap_insert(IntPtr.Zero, "somelib", null, "/path/to/libsomelib.so", null);
		*/

		private static libdl dl = null;

		[HarmonyPatch(
			typeof(TeximpNet.Unmanaged.NvTextureToolsLibrary),
			"TeximpNet.Unmanaged.PlatformHelper",
			"GetAppBaseDirectory",
			HarmonyPatch.Fixation.Prefix,
			HarmonyExt.PriorityLevel.First
		)]
		internal static bool GetAppBaseDirectory (ref string __result) {
			__result = SpriteMaster.Self.AssemblyPath;
			Debug.ErrorLn($"Directory: {__result}");
			return false;
		}

		private const int RTLD_NOW = 2;

		[HarmonyPatch(
			typeof(NvTextureToolsLibrary),
			new string[] { "TeximpNet.Unmanaged.UnmanagedLibrary", "UnmanagedWindowsLibraryImplementation" },
			"NativeLoadLibrary",
			HarmonyPatch.Fixation.Prefix,
			HarmonyExt.PriorityLevel.First,
			platform: HarmonyPatch.Platform.Linux
		)]
		internal static bool NativeLoadLibrary (UnmanagedLibrary __instance, ref IntPtr __result, String path) {
			var libraryHandle = dl.open(path, RTLD_NOW);

			if (libraryHandle == IntPtr.Zero && __instance.ThrowOnLoadFailure) {
				var errPtr = dl.error();
				var msg = Marshal.PtrToStringAnsi(errPtr);
				if (!String.IsNullOrEmpty(msg))
					throw new TeximpException(String.Format("Error loading unmanaged library from path: {0}\n\n{1}", path, msg));
				else
					throw new TeximpException(String.Format("Error loading unmanaged library from path: {0}", path));
			}

			__result = libraryHandle;

			return false;
		}

		[HarmonyPatch(
			typeof(NvTextureToolsLibrary),
			new string[] { "TeximpNet.Unmanaged.UnmanagedLibrary", "UnmanagedWindowsLibraryImplementation" },
			"NativeGetProcAddress",
			HarmonyPatch.Fixation.Prefix,
			HarmonyExt.PriorityLevel.First,
			platform: HarmonyPatch.Platform.Linux
		)]
		internal static bool NativeGetProcAddress (ref IntPtr __result, IntPtr handle, String functionName) {
			__result = dl.sym(handle, functionName);

			return false;
		}

		[HarmonyPatch(
			typeof(NvTextureToolsLibrary),
			new string[] { "TeximpNet.Unmanaged.UnmanagedLibrary", "UnmanagedWindowsLibraryImplementation" },
			"NativeFreeLibrary",
			HarmonyPatch.Fixation.Prefix,
			HarmonyExt.PriorityLevel.First,
			platform: HarmonyPatch.Platform.Linux
		)]
		internal static bool NativeFreeLibrary (IntPtr handle) {
			dl.close(handle);
			return false;
		}

		private abstract class libdl {
			internal abstract IntPtr open (string fileName, int flags);

			internal abstract IntPtr sym (IntPtr handle, string functionName);

			internal abstract int close (IntPtr handle);

			internal abstract IntPtr error ();
		}

		private sealed class libdlbase : libdl {
			private const string lib = "libdl.so";

			internal override IntPtr open (string fileName, int flags) {
				return dlopen(fileName, flags);
			}

			internal override IntPtr sym (IntPtr handle, string functionName) {
				return dlsym(handle, functionName);
			}

			internal override int close (IntPtr handle) {
				return dlclose(handle);
			}

			internal override IntPtr error () {
				return dlerror();
			}

			[DllImport(lib)]
			private static extern IntPtr dlopen (String fileName, int flags);

			[DllImport(lib)]
			private static extern IntPtr dlsym (IntPtr handle, String functionName);

			[DllImport(lib)]
			private static extern int dlclose (IntPtr handle);

			[DllImport(lib)]
			private static extern IntPtr dlerror ();
		}

		private sealed class libdl2 : libdl {
			private const string lib = "libdl.so.2";

			internal override IntPtr open (string fileName, int flags) {
				return dlopen(fileName, flags);
			}

			internal override IntPtr sym (IntPtr handle, string functionName) {
				return dlsym(handle, functionName);
			}

			internal override int close (IntPtr handle) {
				return dlclose(handle);
			}

			internal override IntPtr error () {
				return dlerror();
			}

			[DllImport(lib)]
			private static extern IntPtr dlopen (String fileName, int flags);

			[DllImport(lib)]
			private static extern IntPtr dlsym (IntPtr handle, String functionName);

			[DllImport(lib)]
			private static extern int dlclose (IntPtr handle);

			[DllImport(lib)]
			private static extern IntPtr dlerror ();
		}

		private sealed class libdl227 : libdl {
			private const string lib = "libdl-2.27.so";

			internal override IntPtr open (string fileName, int flags) {
				return dlopen(fileName, flags);
			}

			internal override IntPtr sym (IntPtr handle, string functionName) {
				return dlsym(handle, functionName);
			}

			internal override int close (IntPtr handle) {
				return dlclose(handle);
			}

			internal override IntPtr error () {
				return dlerror();
			}

			[DllImport(lib)]
			private static extern IntPtr dlopen (String fileName, int flags);

			[DllImport(lib)]
			private static extern IntPtr dlsym (IntPtr handle, String functionName);

			[DllImport(lib)]
			private static extern int dlclose (IntPtr handle);

			[DllImport(lib)]
			private static extern IntPtr dlerror ();
		}

		private sealed class libdl1 : libdl {
			private const string lib = "libdl.so.1";

			internal override IntPtr open(string fileName, int flags) {
				return dlopen(fileName, flags);
			}

			internal override IntPtr sym(IntPtr handle, string functionName) {
				return dlsym(handle, functionName);
			}

			internal override int close(IntPtr handle) {
				return dlclose(handle);
			}

			internal override IntPtr error() {
				return dlerror();
			}

			[DllImport(lib)]
			private static extern IntPtr dlopen (String fileName, int flags);

			[DllImport(lib)]
			private static extern IntPtr dlsym (IntPtr handle, String functionName);

			[DllImport(lib)]
			private static extern int dlclose (IntPtr handle);

			[DllImport(lib)]
			private static extern IntPtr dlerror ();
		}
	}
}
