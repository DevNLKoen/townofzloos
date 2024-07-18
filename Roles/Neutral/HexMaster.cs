using TOZ.Impostor;
using static TOZ.Options;

namespace TOZ.Neutral;

public class HexMaster : ISettingHolder
{
    private const int Id = 11900;
    public static OptionItem ModeSwitchAction;

    public void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HexMaster);
        ModeSwitchAction = new StringOptionItem(Id + 10, "WitchModeSwitchAction", Witch.SwitchTriggerText, 2, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);
    }
}