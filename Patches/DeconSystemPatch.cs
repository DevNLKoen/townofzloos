﻿using HarmonyLib;

namespace TOZ.Patches;

[HarmonyPatch(typeof(DeconSystem), nameof(DeconSystem.UpdateSystem))]
public static class DeconSystemUpdateSystemPatch
{
    public static void Prefix(DeconSystem __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (Options.ChangeDecontaminationTime.GetBool())
        {
            var deconTime = Main.CurrentMap switch
            {
                MapNames.Mira => Options.DecontaminationTimeOnMiraHQ.GetFloat(),
                MapNames.Polus => Options.DecontaminationTimeOnPolus.GetFloat(),
                _ => 3f
            };

            __instance.DoorOpenTime = deconTime;
            __instance.DeconTime = deconTime;
        }
        else
        {
            __instance.DoorOpenTime = 3f;
            __instance.DeconTime = 3f;
        }
    }
}