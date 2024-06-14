using HarmonyLib;
using NewHorizons.Components.EOTE;
using UnityEngine;

namespace NewHorizons.Patches;

[HarmonyPatch(typeof(GlobalMusicController))]
public class GlobalMusicControllerPatches
{
	private static AudioDetector _audioDetector;

	[HarmonyPrefix]
	[HarmonyPatch(nameof(GlobalMusicController.UpdateBrambleMusic))]
	public static bool GlobalMusicController_UpdateBrambleMusic(GlobalMusicController __instance)
	{
		// is this too hacky?
		if (_audioDetector == null) _audioDetector = Object.FindObjectOfType<AudioDetector>();


		var shouldBePlaying = Locator.GetPlayerSectorDetector().InBrambleDimension() &&
			!Locator.GetPlayerSectorDetector().InVesselDimension() &&
			PlayerState.AtFlightConsole() &&
			!PlayerState.IsHullBreached() &&
			!__instance._playingFinalEndTimes &&
			_audioDetector._activeVolumes.Count <= 1; // change - don't play if in another audio volume other than ambient
		var playing = __instance._darkBrambleSource.isPlaying &&
			!__instance._darkBrambleSource.IsFadingOut();
		if (shouldBePlaying && !playing)
		{
			__instance._darkBrambleSource.FadeIn(5f);
		}
		else if (!shouldBePlaying && playing)
		{
			__instance._darkBrambleSource.FadeOut(5f);
		}

		return false;
	}


	[HarmonyPrefix]
	[HarmonyPatch(nameof(GlobalMusicController.OnEnterDreamWorld))]
	public static bool GlobalMusicController_OnEnterDreamWorld(GlobalMusicController __instance)
	{
		if (__instance._playingFinalEndTimes)
		{
			__instance._finalEndTimesIntroSource.Stop();
			__instance._finalEndTimesLoopSource.Stop();
			__instance._finalEndTimesDarkBrambleSource.FadeIn(1f);
		}
		else
		{
			if (__instance.TryGetComponent(out DreamWorldEndTimes dreamWorldEndTimes))
			{
				dreamWorldEndTimes.AssignEndTimesDream(__instance._endTimesSource);
			}
			else
			{
				__instance._endTimesSource.Stop();
				__instance._endTimesSource.AssignAudioLibraryClip(AudioType.EndOfTime_Dream);
			}
			__instance._playingEndTimes = false;
		}

		return false;
	}


	[HarmonyPrefix]
	[HarmonyPatch(nameof(GlobalMusicController.OnExitDreamWorld))]
	public static bool GlobalMusicController_OnExitDreamWorld(GlobalMusicController __instance)
	{
		if (__instance._playingFinalEndTimes)
		{
			__instance._finalEndTimesLoopSource.FadeIn(1f);
			__instance._finalEndTimesDarkBrambleSource.Stop();
		}
		else
		{
			if (__instance.TryGetComponent(out DreamWorldEndTimes dreamWorldEndTimes))
			{
				dreamWorldEndTimes.AssignEndTimes(__instance._endTimesSource);
			}
			else
			{
				__instance._endTimesSource.Stop();
				__instance._endTimesSource.AssignAudioLibraryClip(AudioType.EndOfTime);
			}
			__instance._playingEndTimes = false;
		}

		return false;
	}
}
