using System;
using System.Collections;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using TMPro;
using TOZ.Modules;
using Unity.Services.Core.Internal;
using UnityEngine;
//using Il2CppInterop.Runtime.InteropTypes.Arrays;
using static TOZ.Translator;
using unityobjecty = UnityEngine.Object;
using TOZ.Roles.Core;
using static Il2CppSystem.Net.Http.Headers.Parser;
using LibCpp2IL;
//using System.Collections.Generic;
//using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TOZ.Neutral;
using AmongUs.GameOptions;
//using System.Collections.Generic;

namespace TOZ;

// Credit: https://github.com/Yumenopai/TownOfHost_Y

public static class ModGameOptionsMenu
{
    public static int TabIndex;
    //public static List<RoleBase> GetNormalOptions(CustomRoleTypes type);
    public static Dictionary<OptionBehaviour, int> OptionList = new();
    public static Dictionary<int, OptionBehaviour> BehaviourList = new();
    public static Dictionary<int, CategoryHeaderMasked> CategoryHeaderList = new();
    public static Dictionary<int, CategoryHeaderEditRole> categoryHeaderEditRole = new();
    public static Dictionary<int, RoleOptionSetting> RoleOptionList = new();
    public static readonly System.Collections.Generic.Dictionary<OptionBehaviour, CustomRoles> HelpIcons = [];
}

[HarmonyPatch(typeof(GameOptionsMenu))]
public static class GameOptionsMenuPatch
{
    public static GameOptionsMenu Instance;

    [HarmonyPatch(nameof(GameOptionsMenu.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(GameOptionsMenu __instance)
    {
        Instance ??= __instance;
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (__instance.Children == null || __instance.Children.Count == 0)
        {
            __instance.MapPicker.gameObject.SetActive(false);
            __instance.Children = new();
            __instance.CreateSettings();
            __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            __instance.InitializeControllerNavigation();
        }

        return false;
    }

    [HarmonyPatch(nameof(GameOptionsMenu.Initialize)), HarmonyPostfix]
    private static void InitializePostfix()
    {
        GameObject.Find("PlayerOptionsMenu(Clone)")?.transform.FindChild("Background")?.gameObject.SetActive(false);
    }

    private static void IncreaseChance(OptionBehaviour option, CustomRoles role)
    {
        float chance = Options.GetRoleChance(role);

        //Options.SetRoleChance(role, chance + 1f);
        OnValueChanged(option, role);
    }

    private static void DecreaseChance(OptionBehaviour option, CustomRoles role)
    {
        float chance = Options.GetRoleChance(role);
        //Options.SetRoleChance(role, chance - 1f);
        OnValueChanged(option, role);
    }
    private static void IncreaseCount(OptionBehaviour option, CustomRoles role)
    {
        int count = Options.GetRoleCount(role);

        Options.SetRoleCount(role, count + 1);
        OnValueChanged(option, role);
    }

    private static void DecreaseCount(OptionBehaviour option, CustomRoles role)
    {
        int count = Options.GetRoleCount(role);
        Options.SetRoleCount(role, count);
        OnValueChanged(option, role);
    }

    private static void OnValueChanged(OptionBehaviour optionnonconv, CustomRoles role)
    {
        RoleOptionSetting option = optionnonconv as RoleOptionSetting;
        if (option == null) return;

        float roleChance = Options.GetRoleChance(role) * 5f;
        int roleCount = Options.GetRoleCount(role);

        option.roleMaxCount = roleCount;
        option.roleChance = (int)roleChance;
        option.countText.text = option.roleMaxCount.ToString();
        option.chanceText.text = option.roleChance.ToString();
    }

    private static void CreateCustomQuotaOption(RolesSettingsMenu inst, RoleBase dog, ref float yPos)
    {
        RoleOptionSetting roleOptionSetting = Object.Instantiate(inst.roleOptionSettingOrigin, Vector3.zero, Quaternion.identity, inst.RoleChancesSettings.transform);
        roleOptionSetting.transform.localPosition = new Vector3(-0.15f, yPos, -2f);
        roleOptionSetting.transform.Find("Role #").gameObject.SetActive(false);

        // Set Role
        SpriteRenderer[] componentsInChildren = roleOptionSetting.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            componentsInChildren[i].material.SetInt(PlayerMaterial.MaskLayer, 20);
        }
        foreach (TextMeshPro textMeshPro in roleOptionSetting.GetComponentsInChildren<TextMeshPro>(true))
        {
            textMeshPro.fontMaterial.SetFloat("_StencilComp", 3f);
            textMeshPro.fontMaterial.SetFloat("_Stencil", 20);
        }
        roleOptionSetting.role = RoleManager.Instance.AllRoles[0];
        roleOptionSetting.titleText.text = Utils.GetRoleName(dog.ThisCustomRole);

        if (dog.ThisCustomRole.IsImpostorTeam())
        {
            roleOptionSetting.labelSprite.color = Palette.ImpostorRoleRed;
        }
        else if (dog.ThisCustomRole.IsNeutral())
        {
            _ = ColorUtility.TryParseHtmlString("#7f8c8d", out Color c);
            roleOptionSetting.labelSprite.color = c;
        }
        else
        {
            roleOptionSetting.labelSprite.color = Palette.CrewmateRoleBlue;
        }

        //PassiveButton minusButton = roleOptionSetting.transform.Find("Chance %").Find("MinusButton (1)").GetComponent<PassiveButton>();
        //PassiveButton plusButton = roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetComponent<PassiveButton>();

        //plusButton.OnClick.RemoveAllListeners();
        //minusButton.OnClick.RemoveAllListeners();
        //plusButton.OnClick.AddListener(new Action(() => IncreaseChance(roleOptionSetting, dog.ThisCustomRole)));
        //minusButton.OnClick.AddListener(new Action(() => DecreaseChance(roleOptionSetting, dog.ThisCustomRole)));

        roleOptionSetting.OnValueChanged = new Action<OptionBehaviour>((OptionBehaviour a) => OnValueChanged(a, dog.ThisCustomRole));
        roleOptionSetting.SetClickMask(inst.ButtonClickMask);
        inst.roleChances.Add(roleOptionSetting);
        //List<UiElement> quotaMono = inst.QuotaTabSelectables.ToManaged();
        //quotaMono.AddRange(roleOptionSetting.ControllerSelectable.ToManaged());
        //inst.QuotaTabSelectables = quotaMono.ToIl2Cpp();
        OnValueChanged(roleOptionSetting, Enum.Parse<CustomRoles>(dog.GetType().Name, true));
        yPos -= 0.43f;
    }

    private static void LoadRoleOptions(RolesSettingsMenu thiz, VanillaLikeRoleTypes type)
    {
        Logger.Info(type.ToString(), "LoadRoleOptions");

        foreach (var header in thiz.RoleChancesSettings.GetComponentsInChildren<CategoryHeaderEditRole>())
        {
            Object.Destroy(header.gameObject);
        }

        foreach (var opt in thiz.roleChances)
        {
            if (opt != null)
            {
                Object.Destroy(opt.gameObject);
            }
        }
        thiz.roleChances.Clear();

        Dictionary<string, List<RoleBase>> roles = new Dictionary<string, List<RoleBase>>();
        switch (type)
        {
            case VanillaLikeRoleTypes.Crewmate:
                ////roles.Add("Vanilla", CustomRoleManager.GetNormalOptions(RoleOptionType.Crewmate_Normal));
                ////roles.Add("Imposter based", CustomRoleManager.GetNormalOptions(RoleOptionType.Crewmate_ImpostorBased));
                //roles.Add("Vanilla Ghost", CustomRoleManager.GetNormalOptions(CustomRoleTypes.CrewmateVanillaGhosts));
                //roles.Add("Ghost", CustomRoleManager.GetNormalOptions(RoleOptionType.CrewmateGhosts));
                break;
            case VanillaLikeRoleTypes.Impostor:
                ////roles.Add("Vanilla", CustomRoleManager.GetNormalOptions(RoleOptionType.Impostor));
                //roles.Add("Support", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorSupport));
                //roles.Add("Killing", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorKilling));
                //roles.Add("Hindering", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorHindering));
                //roles.Add("Concealing", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorConcealing));
                //roles.Add("Ghost", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorGhosts));
                break;
            case VanillaLikeRoleTypes.Neutral:
                ////roles.Add("Benign", CustomRoleManager.GetNormalOptions(RoleOptionType.Neutral_NonKilling));
                ////roles.Add("Evil", CustomRoleManager.GetNormalOptions(RoleOptionType.Neutral_Killing));
                //roles.Add("Killing", CustomRoleManager.GetNormalOptions(RoleOptionType.NeutralKilling));
                //roles.Add("Chaos", CustomRoleManager.GetNormalOptions(RoleOptionType.NeutralChaos));
                break;
        }

        float num = 0.662f;

        CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(thiz.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, thiz.RoleChancesSettings.transform);

        // Set Header
        categoryHeaderEditRole.Title.text = type.ToString();//GetString(type.ToString());
        categoryHeaderEditRole.Background.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Divider?.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);

        categoryHeaderEditRole.transform.localPosition = new Vector3(4.986f, num, -2f);
        num -= 0.522f;
        int num3 = 0;
        foreach (var roleCategory in roles)
        {
            CreateHeader(roleCategory.Key, thiz, ref num);
            num3++;
            foreach (var role in roleCategory.Value)
            {
                CreateCustomQuotaOption(thiz, role, ref num);
                num3++;
            }
        }

        thiz.scrollBar.ContentYBounds.max = (num3 / 2.4f); // 2.5f works better sometimes, needs to find the correct number
        Logger.Info($"{type} Count: {roles.Count}", "LoadRoleOptions");
    }

    // Additional methods

    private static void CreateHeader(string name, RolesSettingsMenu thiz, ref float yPos)
    {
        CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(thiz.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, thiz.RoleChancesSettings.transform);
        categoryHeaderEditRole.transform.Find("QuotaHeader").gameObject.SetActive(false); ;
        categoryHeaderEditRole.transform.localPosition = new Vector3(4.98f, yPos - 0.1f, -2f);
        categoryHeaderEditRole.Title.text = name;
        categoryHeaderEditRole.Background.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Divider?.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);
        yPos -= 0.43f;
    }

    private static void SetTabColor(RoleSettingsTabButton tab, string hex)
    {
        if (tab == null) return;

        Color color = Color.blue;
        hex = hex.TrimStart('#');

        if (hex.Length != 6)
        {
            throw new InvalidOperationException("Hex color should be 6 characters long excluding the '#' symbol.");
        }

        float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;

        color = new Color(r, g, b);
        if (tab.button.inactiveSprites.GetComponent<SpriteRenderer>() != null && tab.button.activeSprites.GetComponent<SpriteRenderer>() != null)
        {
            SpriteRenderer inactiveSprite = tab.button.inactiveSprites.GetComponent<SpriteRenderer>();
            SpriteRenderer activeSprite = tab.button.inactiveSprites.GetComponent<SpriteRenderer>();
            inactiveSprite.color = GetInactiveColor(color);
            activeSprite.color = color;
        }
    }

    private static Color GetInactiveColor(this Color color, float shadowFactor = 0.5f)
    {
        shadowFactor = Mathf.Max(1.0f, shadowFactor);

        Color shadowColor = new(
            color.r / shadowFactor,
            color.g / shadowFactor,
            color.b / shadowFactor,
            color.a
        );

        return shadowColor;
    }

    enum VanillaLikeRoleTypes : int
    {
        Crewmate = 0,
        Impostor = 1,
        Neutral = 2,
    }

    [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings)), HarmonyPrefix]
    private static bool CreateSettingsPrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;
        __instance.scrollBar.SetYBoundsMax(CalculateScrollBarYBoundsMax());
        __instance.StartCoroutine(CoRoutine().WrapToIl2Cpp());
        return false;

        IEnumerator CoRoutine()
        {
            var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

            float num = 2.0f;
            const float posX = 0.952f;
            const float posZ = -2.0f;
            for (int index = 0; index < OptionItem.AllOptions.Count; index++)
            {
                var option = OptionItem.AllOptions[index];
                if (option.Tab != modTab) continue;

                var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

                if (option is TextOptionItem)
                {
                    CategoryHeaderMasked categoryHeaderMasked = Object.Instantiate(__instance.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                    categoryHeaderMasked.SetHeader(StringNames.RolesCategory, 20);
                    categoryHeaderMasked.Title.text = option.GetName();
                    categoryHeaderMasked.transform.localScale = Vector3.one * 0.63f;
                    categoryHeaderMasked.transform.localPosition = new(-0.903f, num, posZ);
                    //var chmText = categoryHeaderMasked.transform.FindChild("HeaderText").GetComponent<TextMeshPro>();
                    //chmText.fontStyle = FontStyles.Bold;
                    //chmText.outlineWidth = 0.17f;
                    categoryHeaderMasked.gameObject.SetActive(enabled);
                    ModGameOptionsMenu.CategoryHeaderList.TryAdd(index, categoryHeaderMasked);

                    if (enabled) num -= 0.63f;
                }
                else if (option is RoleTitleOptionItem)
                {
                    CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(__instance.RolesMenu.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);

                    // Set Header
                    categoryHeaderEditRole.SetHeader(StringNames.RolesCategory, 20);
                    categoryHeaderEditRole.Title.text = option.GetName();
                    categoryHeaderEditRole.Divider.color = Color.white;
                    categoryHeaderEditRole.chanceLabel.color = Color.white;
                    //categoryHeaderEditRole.gameObject.SetActive(enabled);
                    categoryHeaderEditRole.blankLabel.color = Palette.CrewmateRoleHeaderVeryDarkBlue;
                    categoryHeaderEditRole.Background.color = Palette.CrewmateRoleHeaderBlue;
                    //categoryHeaderEditRole.blankLabel.color = Palette.CrewmateRoleHeaderVeryDarkBlue;
                    // Set Role
                    SpriteRenderer[] componentsInChildren = categoryHeaderEditRole.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        componentsInChildren[i].material.SetInt(PlayerMaterial.MaskLayer, 20);
                    }
                    foreach (TextMeshPro textMeshPro in categoryHeaderEditRole.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        textMeshPro.fontMaterial.SetFloat("_StencilComp", 3f);
                        textMeshPro.fontMaterial.SetFloat("_Stencil", 20);
                    };
                    categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
                    categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);
                    categoryHeaderEditRole.Title.color = Palette.CrewmateRoleHeaderDarkBlue;
                    categoryHeaderEditRole.transform.localPosition = new Vector3(4.952f - 0.6f, num, 2f);
                    categoryHeaderEditRole.gameObject.SetActive(enabled);
                    ModGameOptionsMenu.categoryHeaderEditRole.TryAdd(index, categoryHeaderEditRole);

                    if (enabled) num -= 0.63f;
                    CreateHeader("test", __instance.RolesMenu, ref num);
                }
                else if (option is RoleOptionItem)
                {
                    /*CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(__instance.RolesMenu.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);

                    // Set Header
                    categoryHeaderEditRole.Title.text = "Crewmates";
                    categoryHeaderEditRole.Background.material.SetInt(PlayerMaterial.MaskLayer, 20);
                    categoryHeaderEditRole.Background.color = Palette.CrewmateRoleHeaderBlue;
                    categoryHeaderEditRole.blankLabel.color = Palette.CrewmateRoleHeaderVeryDarkBlue;
                    categoryHeaderEditRole.chanceLabel.color = Palette.CrewmateRoleHeaderDarkBlue;
                    categoryHeaderEditRole.countLabel.color = Palette.CrewmateRoleHeaderDarkBlue;
                    categoryHeaderEditRole.blankLabel.material.SetInt(PlayerMaterial.MaskLayer, 20);
                    categoryHeaderEditRole.chanceLabel.material.SetInt(PlayerMaterial.MaskLayer, 20);
                    categoryHeaderEditRole.countLabel.material.SetInt(PlayerMaterial.MaskLayer, 20);
                    categoryHeaderEditRole.Divider?.material.SetInt(PlayerMaterial.MaskLayer, 20);
                    categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
                    categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);
                    categoryHeaderEditRole.Title.color = Palette.CrewmateRoleHeaderDarkBlue;

                    categoryHeaderEditRole.transform.localPosition = new Vector3(4.952f - 0.6f, num, 2f);*/
                    RoleOptionSetting roleOptionSetting = Object.Instantiate(__instance.RolesMenu.roleOptionSettingOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                    roleOptionSetting.transform.localPosition = new Vector3(-0.15f - posX + 0.6f, num - 0.5f, posZ);
                    roleOptionSetting.transform.Find("Role #").gameObject.SetActive(true);
                    //Dictionary<string, List<RoleBase>> roles = [];
                    //CreateCustomQuotaOption(__instance.RolesMenu, roles, ref num);
                    // Set Role
                    SpriteRenderer[] componentsInChildren = roleOptionSetting.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        componentsInChildren[i].material.SetInt(PlayerMaterial.MaskLayer, 20);
                    }
                    foreach (TextMeshPro textMeshPro in roleOptionSetting.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        textMeshPro.fontMaterial.SetFloat("_StencilComp", 3f);
                        textMeshPro.fontMaterial.SetFloat("_Stencil", 20);
                    };




                    roleOptionSetting.labelSprite.color = Palette.CrewmateRoleBlue;



                    roleOptionSetting.titleText.text = option.GetName();
                    //roleOptionSetting.roleTitleText.text = option.GetName();
                    //roleOptionSetting.Role.TeamType = RoleTeamTypes.Crewmate;
                    //roleOptionSetting.chanceText.text = "test";
                    roleOptionSetting.roleChance = (int)CustomRoles.Jester.GetChance();
             //       roleOptionSetting.chanceText.text = option.roles.GetChance().ToString();
                    //roleOptionSetting.OnValueChanged();
                    //roleOptionSetting.countText.text = CustomRoles.Jester.GetCount().ToString();
                    roleOptionSetting.chanceText.text = roleOptionSetting.roleChance.ToString();

                    //roleOptionSetting.transform.localScale = Vector3.one * 0.63f;
                    //roleOptionSetting.transform.localPosition = new(-0.903f, num, posZ);
                    //var rosText = roleOptionSetting.transform.FindChild("RoleText").GetComponent<TextMeshPro>();
                    //rosText.fontStyle = FontStyles.Bold;
                    //rosText.outlineWidth = 0.17f;
                    roleOptionSetting.gameObject.SetActive(enabled);
                    //roleOptionSetting.countText.text = "count";
                    roleOptionSetting.countText.text = roleOptionSetting.RoleMaxCount.ToString();
                    //roleOptionSetting.role.Role = RoleTypes.Engineer;
                    roleOptionSetting.role.TeamType = RoleTeamTypes.Impostor;
                    //roleOptionSetting.transform.GetComponent<TextMeshPro>().enabled = true;
                    roleOptionSetting.LabelBackground.gameObject.SetActive(enabled);
                    roleOptionSetting.LabelBackground.color = Color.blue;

                    roleOptionSetting.SetClickMask(__instance.ButtonClickMask);
                    roleOptionSetting.roleChance = 29;
                    //roleOptionSetting.buttons[1].OnClick.AddCall(roleOptionSetting.IncreaseChance);
                    roleOptionSetting.data.name = "hi";
                    roleOptionSetting.IncreaseCount();
                    roleOptionSetting.role = RoleManager.Instance.AllRoles[0];
                    roleOptionSetting.labelSprite.color = Palette.ImpostorRoleRed;
                    roleOptionSetting.LabelBackground.color = Palette.ImpostorRed;
                    //roleOptionSetting.titleText.text = Utils.GetRoleName(dog.ThisRoleBase);
                    //roleOptionSetting.buttons.Count = 0;
                    ModGameOptionsMenu.RoleOptionList.TryAdd(index, roleOptionSetting);
                    //break;
                    //PassiveButton minusButton = roleOptionSetting.transform.Find("Chance %").Find("MinusButton (1)").GetComponent<PassiveButton>();
                    PassiveButton plusButton = roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetComponent<PassiveButton>();
                    //PassiveButton minusroleButton = roleOptionSetting.transform.Find("Role #").Find("MinusButton (1)").GetComponent<PassiveButton>();
                    PassiveButton plusroleButton = roleOptionSetting.transform.Find("Role #").Find("PlusButton (1)").GetComponent<PassiveButton>();
                    //Debug.Log(roleOptionSetting.transform.GetChild(0).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.GetChild(1).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.GetChild(2).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.GetChild(3).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.GetChild(4).gameObject.name);
                    ///Debug.Log(roleOptionSetting.transform.GetChild(5).gameObject.name);
                    Debug.Log(roleOptionSetting.transform.Find("Chance %").GetChild(1).gameObject.name);
                    Debug.Log(roleOptionSetting.transform.Find("Chance %").GetChild(2).gameObject.name);
                    Debug.Log(roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetComponent<PassiveButton>());
                    Debug.Log(roleOptionSetting.transform.childCount);
                    //Debug.Log(roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetChild(1).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetChild(2).gameObject.name);
                    //Debug.Log(roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetChild(3).gameObject.name);
                    plusButton.OnClick.RemoveAllListeners();
                    //minusButton.OnClick.RemoveAllListeners();
                    plusroleButton.OnClick.RemoveAllListeners();
                    //minusroleButton.OnClick.RemoveAllListeners();

                    //plusButton.OnClick.AddListener(new Action(() => IncreaseChance(roleOptionSetting, option.roles)));
                    plusButton.OnClick.AddListener(new Action(() => Debug.Log("TESTING THE BUTTON PLS WORK I DONT WANT TO TRY MANY THINGS AGAIN PLS")));
                    //minusButton.OnClick.AddListener(new Action(() => DecreaseChance(roleOptionSetting, option.roles)));

                    //plusButton.OnClick.AddListener(new Action(() => IncreaseChance(roleOptionSetting, RoleBaseL.GetCustomRoleTypes)));
                    //minusButton.OnClick.AddListener(new Action(() => DecreaseChance(roleOptionSetting, CustomRoles.Jester)));


                    //PassiveButton minuscountButton = roleOptionSetting.transform.Find("Count %").Find("MinusButton").GetComponent<PassiveButton>();
                    //PassiveButton pluscountButton = roleOptionSetting.transform.Find("Count %").Find("PlusButton").GetComponent<PassiveButton>();
                    //plusButton.OnClick.RemoveAllListeners();
                    //minuscountButton.OnClick.RemoveAllListeners();
                    //plusButton.OnClick.AddListener(new Action(() => IncreaseCount(roleOptionSetting, dog.ThisCustomRole)));
                    // minuscountButton.OnClick.AddListener(new Action(() => DecreaseCount(roleOptionSetting, dog.ThisCustomRole)));





                    roleOptionSetting.transform.FindChild("Chance %").FindChild("PlusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                    roleOptionSetting.transform.FindChild("Chance %").FindChild("MinusButton (1)").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                    var valueTMP = roleOptionSetting.transform.transform.FindChild("Chance %").FindChild("Value_TMP (1)");
                    valueTMP.localPosition += new Vector3(1.3f, 0f, 0f);
                    valueTMP.GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                    //goto default;



                }
                else if (option.IsHeader && enabled) num -= 0.3f;

                if (option is TextOptionItem) continue;
                else if (option is RoleOptionItem) continue;
                else if (option is RoleTitleOptionItem) continue;

                //else if (option.IsHeader && enabled) num -= 0.3f;

                //if (option is RoleOptionItem) continue;

                var baseGameSetting = GetSetting(option);
                if (baseGameSetting == null) continue;


                OptionBehaviour optionBehaviour;

                switch (baseGameSetting.Type)
                {
                    case OptionTypes.Checkbox:
                        {
                            optionBehaviour = Object.Instantiate(__instance.checkboxOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                            //optionBehaviour.transform.localPosition = new(posX, num, posZ);

                            //OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                            optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                            optionBehaviour.SetUpFromData(baseGameSetting, 20);
                            ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                            break;
                        }
                    case OptionTypes.String:
                        {
                            optionBehaviour = Object.Instantiate(__instance.stringOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                            //optionBehaviour.transform.localPosition = new(posX, num, posZ);

                            //OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                        if (option.Name == "GameMode" && !ModGameOptionsMenu.OptionList.ContainsValue(index))
                        {
                            GameSettingMenuPatch.GameModeBehaviour = (StringOption)optionBehaviour;
                        }

                        optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                        optionBehaviour.SetUpFromData(baseGameSetting, 20);
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                    case OptionTypes.Float:
                        {
                            optionBehaviour = Object.Instantiate(__instance.numberOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                            //optionBehaviour.transform.localPosition = new(posX, num, posZ);

                            //OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                            optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                            optionBehaviour.SetUpFromData(baseGameSetting, 20);
                            ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                            break;
                        }
                    case OptionTypes.Int:
                        {
                            optionBehaviour = Object.Instantiate(__instance.numberOptionOrigin, Vector3.zero, Quaternion.identity, __instance.settingsContainer);
                            //optionBehaviour.transform.localPosition = new(posX, num, posZ);

                            //OptionBehaviourSetSizeAndPosition(optionBehaviour, option, baseGameSetting.Type);

                        if (option.Name == "Preset" && !ModGameOptionsMenu.OptionList.ContainsValue(index))
                        {
                            GameSettingMenuPatch.PresetBehaviour = (NumberOption)optionBehaviour;
                        }

                        optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                        optionBehaviour.SetUpFromData(baseGameSetting, 20);
                        ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                        break;
                    }
                    default:
                        continue;
                }

                optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                optionBehaviour.SetClickMask(__instance.ButtonClickMask);
                optionBehaviour.SetUpFromData(baseGameSetting, 20);
                ModGameOptionsMenu.OptionList.TryAdd(optionBehaviour, index);
                ModGameOptionsMenu.BehaviourList.TryAdd(index, optionBehaviour);
                optionBehaviour.gameObject.SetActive(enabled);
                optionBehaviour.OnValueChanged = new Action<OptionBehaviour>(__instance.ValueChanged);
                __instance.Children.Add(optionBehaviour);

                option.OptionBehaviour = optionBehaviour;

                if (enabled) num -= 0.45f;

                if (index % 50 == 0) yield return null;
            }

            yield return null;

            __instance.ControllerSelectable.Clear();
            foreach (var x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            {
                __instance.ControllerSelectable.Add(x);
            }
        }

        float CalculateScrollBarYBoundsMax()
        {
            float num = 2.0f;
            foreach (var option in OptionItem.AllOptions)
            {
                if (option.Tab != (TabGroup)(ModGameOptionsMenu.TabIndex - 3)) continue;

                var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

                if (option is TextOptionItem) num -= 0.63f;
                else
                {
                    if (option.IsHeader && enabled) num -= 0.3f;
                    if (enabled) num -= 0.45f;
                }
            }

            return -num - 1.65f;
        }
    }

    private static void OptionBehaviourSetSizeAndPosition(OptionBehaviour optionBehaviour, OptionItem option, OptionTypes type)
    {
        Vector3 positionOffset = new(0f, 0f, 0f);
        Vector3 scaleOffset = new(0f, 0f, 0f);
        Color color = new(0.8f, 0.8f, 0.8f);
        float sizeDelta_x = 5.7f;

        if (option.Parent?.Parent?.Parent != null)
        {
            scaleOffset = new(-0.18f, 0, 0);
            positionOffset = new(0.3f, 0f, 0f);
            color = new(0.8f, 0.8f, 0.2f);
            sizeDelta_x = 5.1f;
        }
        else if (option.Parent?.Parent != null)
        {
            scaleOffset = new(-0.12f, 0, 0);
            positionOffset = new(0.2f, 0f, 0f);
            color = new(0.5f, 0.2f, 0.8f);
            sizeDelta_x = 5.3f;
        }
        else if (option.Parent != null)
        {
            scaleOffset = new(-0.05f, 0, 0);
            positionOffset = new(0.1f, 0f, 0f);
            color = new(0.2f, 0.8f, 0.8f);
            sizeDelta_x = 5.5f;
        }

        var labelBackground = optionBehaviour.transform.FindChild("LabelBackground");
        //labelBackground.GetComponent<SpriteRenderer>().color = color;
        //labelBackground.localScale += new Vector3(0.9f, -0.2f, 0f) + scaleOffset;
        //labelBackground.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;

        var titleText = optionBehaviour.transform.FindChild("Title Text");
        //titleText.localPosition += new Vector3(-0.4f, 0f, 0f) + positionOffset;
        titleText.GetComponent<RectTransform>().sizeDelta = new(sizeDelta_x, 0.37f);
        var textMeshPro = titleText.GetComponent<TextMeshPro>();
        textMeshPro.alignment = TextAlignmentOptions.MidlineLeft;
        textMeshPro.fontStyle = FontStyles.Bold;
        textMeshPro.outlineWidth = 0.17f;

        switch (type)
        {
            case OptionTypes.Checkbox:
                optionBehaviour.transform.FindChild("Toggle").localPosition = new(1.46f, -0.042f);
                break;

            case OptionTypes.String:
                var plusButton = optionBehaviour.transform.FindChild("PlusButton (1)");
                var minusButton = optionBehaviour.transform.FindChild("MinusButton (1)");
                plusButton.localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                minusButton.localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                var valueTMP = optionBehaviour.transform.FindChild("Value_TMP (1)");
                valueTMP.localPosition += new Vector3(1.3f, 0f, 0f);
                valueTMP.GetComponent<RectTransform>().sizeDelta = new(2.3f, 0.4f);
                goto default;

            case OptionTypes.Float:
            case OptionTypes.Int:
                optionBehaviour.transform.FindChild("PlusButton").localPosition += new Vector3(option.IsText ? 500f : 1.7f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("MinusButton").localPosition += new Vector3(option.IsText ? 500f : 0.9f, option.IsText ? 500f : 0f, option.IsText ? 500f : 0f);
                optionBehaviour.transform.FindChild("Value_TMP").localPosition += new Vector3(1.3f, 0f, 0f);
                goto default;

            default:
                var valueBox = optionBehaviour.transform.FindChild("ValueBox");
                valueBox.localScale += new Vector3(0.2f, 0f, 0f);
                valueBox.localPosition += new Vector3(1.3f, 0f, 0f);
                break;
        }
    }

    [HarmonyPatch(nameof(GameOptionsMenu.ValueChanged)), HarmonyPrefix]
    private static bool ValueChangedPrefix(GameOptionsMenu __instance, OptionBehaviour option)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return true;

        if (ModGameOptionsMenu.OptionList.TryGetValue(option, out var index))
        {
            var item = OptionItem.AllOptions[index];
            if (item != null && item.Children.Count > 0) ReCreateSettings(__instance);
        }

        return false;
    }

    public static void ReCreateSettings(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < 3) return;
        var modTab = (TabGroup)(ModGameOptionsMenu.TabIndex - 3);

        float num = 2.0f;
        for (int index = 0; index < OptionItem.AllOptions.Count; index++)
        {
            var option = OptionItem.AllOptions[index];
            if (option.Tab != modTab) continue;

            var enabled = !option.IsHiddenOn(Options.CurrentGameMode) && (option.Parent == null || (!option.Parent.IsHiddenOn(Options.CurrentGameMode) && option.Parent.GetBool()));

            if (ModGameOptionsMenu.CategoryHeaderList.TryGetValue(index, out var categoryHeaderMasked))
            {
                categoryHeaderMasked.transform.localPosition = new(-0.903f, num, -2f);
                categoryHeaderMasked.gameObject.SetActive(enabled);
                if (enabled) num -= 0.63f;
            }
            else if (option.IsHeader && enabled) num -= 0.3f;

            if (ModGameOptionsMenu.RoleOptionList.TryGetValue(index, out var roleOptionSetting))
            {
                roleOptionSetting.transform.localPosition = new(0.952f, num, -2f);
                roleOptionSetting.gameObject.SetActive(enabled);
                if (enabled) num -= 0.63f;
            }

            //else if (option.IsHeader && enabled) num -= 0.3f;

            if (ModGameOptionsMenu.BehaviourList.TryGetValue(index, out var optionBehaviour))
            {
                optionBehaviour.transform.localPosition = new(0.952f, num, -2f);
                optionBehaviour.gameObject.SetActive(enabled);
                if (enabled) num -= 0.45f;
            }
        }

        __instance.ControllerSelectable.Clear();
        foreach (var x in __instance.scrollBar.GetComponentsInChildren<UiElement>())
            __instance.ControllerSelectable.Add(x);
        __instance.scrollBar.SetYBoundsMax(-num - 1.65f);
    }

    private static BaseGameSetting GetSetting(OptionItem item)
    {
        //ReSharper disable Unity.IncorrectScriptableObjectInstantiation/
        BaseGameSetting baseGameSetting = item switch
        {
            BooleanOptionItem => new CheckboxGameSetting
            {
                Type = OptionTypes.Checkbox
            },
            IntegerOptionItem integerOptionItem => new IntGameSetting
            {
                Type = OptionTypes.Int,
                Value = integerOptionItem.GetInt(),
                Increment = integerOptionItem.Rule.Step,
                ValidRange = new(integerOptionItem.Rule.MinValue, integerOptionItem.Rule.MaxValue),
                ZeroIsInfinity = false,
                SuffixType = NumberSuffixes.Multiplier,
                FormatString = string.Empty
            },
            FloatOptionItem floatOptionItem => new FloatGameSetting
            {
                Type = OptionTypes.Float,
                Value = floatOptionItem.GetFloat(),
                Increment = floatOptionItem.Rule.Step,
                ValidRange = new(floatOptionItem.Rule.MinValue, floatOptionItem.Rule.MaxValue),
                ZeroIsInfinity = false,
                SuffixType = NumberSuffixes.Multiplier,
                FormatString = string.Empty
            },
            StringOptionItem stringOptionItem => new StringGameSetting
            {
                Type = OptionTypes.String,
                Values = new StringNames[stringOptionItem.Selections.Count],
                Index = stringOptionItem.GetInt()
            },
            RoleOptionItem roleOptionItem => new StringGameSetting
            {
                Type = OptionTypes.String,
                Values = new StringNames[roleOptionItem.Selections.Count],
                Index = roleOptionItem.GetInt()
            },
            PresetOptionItem presetOptionItem => new IntGameSetting
            {
                Type = OptionTypes.Int,
                Value = presetOptionItem.GetInt(),
                Increment = presetOptionItem.Rule.Step,
                ValidRange = new(presetOptionItem.Rule.MinValue, presetOptionItem.Rule.MaxValue),
                ZeroIsInfinity = false,
                SuffixType = NumberSuffixes.Multiplier,
                FormatString = string.Empty
            },
            _ => null
        };
        // ReSharper restore Unity.IncorrectScriptableObjectInstantiation

        if (baseGameSetting != null)
        {
            baseGameSetting.Title = StringNames.Accept;
        }

        return baseGameSetting;
    }

    public static void ReloadUI(int index)
    {
        GameSettingMenu.Instance?.Close();
        LateTask.New(() =>
        {
            if (GameStates.IsLobby) GameObject.Find("Host Buttons")?.transform.FindChild("Edit")?.GetComponent<PassiveButton>()?.ReceiveClickDown();
        }, 0.1f, log: false);
        LateTask.New(() =>
        {
            if (GameStates.IsLobby)
            {
                GameSettingMenu.Instance?.ChangeTab(index, Controller.currentTouchType == Controller.TouchType.Joystick);
                ModGameOptionsMenu.TabIndex = index;
            }
        }, 0.38f, log: false);
    }
}

[HarmonyPatch(typeof(ToggleOption))]
public static class ToggleOptionPatch
{
    [HarmonyPatch(nameof(ToggleOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(ToggleOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            __instance.TitleText.text = item.GetName();
            __instance.CheckMark.enabled = item.GetBool();
            item.OptionBehaviour = __instance;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(ToggleOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(ToggleOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            item.SetValue(__instance.GetBool() ? 1 : 0);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(NumberOption))]
public static class NumberOptionPatch
{
    private static int IncrementMultiplier
    {
        get
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return 5;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return 10;
            return 1;
        }
    }

    [HarmonyPatch(nameof(NumberOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(NumberOption __instance)
    {
        switch (__instance.Title)
        {
            case StringNames.GameVotingTime:
                __instance.ValidRange = new(0, 600);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameShortTasks:
            case StringNames.GameLongTasks:
            case StringNames.GameCommonTasks:
                __instance.ValidRange = new(0, 90);
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameKillCooldown:
                __instance.ValidRange = new(0, 180);
                __instance.Increment = 0.5f;
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GamePlayerSpeed:
            case StringNames.GameCrewLight:
            case StringNames.GameImpostorLight:
                __instance.Increment = 0.05f;
                __instance.Value = (float)Math.Round(__instance.Value, 2);
                break;
            case StringNames.GameNumImpostors when DebugModeManager.IsDebugMode:
                __instance.ValidRange.min = 0;
                break;
        }

        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            __instance.TitleText.text = item.GetName();
            item.OptionBehaviour = __instance;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(NumberOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(NumberOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            switch (item)
            {
                case IntegerOptionItem integerOptionItem:
                    integerOptionItem.SetValue(integerOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                    break;
                case FloatOptionItem floatOptionItem:
                    floatOptionItem.SetValue(floatOptionItem.Rule.GetNearestIndex(__instance.GetFloat()));
                    break;
                case PresetOptionItem presetOptionItem:
                    presetOptionItem.SetValue(presetOptionItem.Rule.GetNearestIndex(__instance.GetInt()));
                    GameOptionsMenuPatch.ReloadUI(ModGameOptionsMenu.TabIndex);
                    break;
            }

            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(NumberOption.FixedUpdate)), HarmonyPrefix]
    private static bool FixedUpdatePrefix(NumberOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            if (!Mathf.Approximately(__instance.oldValue, __instance.Value))
            {
                __instance.oldValue = __instance.Value;
                __instance.ValueText.text = GetValueString(__instance, __instance.Value, item);
            }

            return false;
        }

        return true;
    }

    private static string GetValueString(NumberOption __instance, float value, OptionItem item)
    {
        if (__instance.ZeroIsInfinity && Mathf.Abs(value) < 0.0001f) return "<b>∞</b>";
        return item == null ? value.ToString(__instance.FormatString) : item.GetString();
    }

    [HarmonyPatch(nameof(NumberOption.Increase)), HarmonyPrefix]
    public static bool IncreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.max))
        {
            __instance.Value = __instance.ValidRange.min;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        var increment = IncrementMultiplier * __instance.Increment;
        if (__instance.Value + increment < __instance.ValidRange.max)
        {
            __instance.Value += increment;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(NumberOption.Decrease)), HarmonyPrefix]
    public static bool DecreasePrefix(NumberOption __instance)
    {
        if (Mathf.Approximately(__instance.Value, __instance.ValidRange.min))
        {
            __instance.Value = __instance.ValidRange.max;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        var increment = IncrementMultiplier * __instance.Increment;
        if (__instance.Value - increment > __instance.ValidRange.min)
        {
            __instance.Value -= increment;
            __instance.UpdateValue();
            __instance.OnValueChanged.Invoke(__instance);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(StringOption))]
public static class StringOptionPatch
{
    [HarmonyPatch(nameof(StringOption.Initialize)), HarmonyPrefix]
    private static bool InitializePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            var name = item.GetName();
            item.OptionBehaviour = __instance;
            string name1 = name;
            if (Enum.GetValues<CustomRoles>().Find(x => Translator.GetString($"{x}") == name1.RemoveHtmlTags(), out var role))
            {
                name = name.RemoveHtmlTags();
                if (Options.UsePets.GetBool() && role.PetActivatedAbility()) name += Translator.GetString("SupportsPetIndicator");
                if (!Options.UsePets.GetBool() && role.OnlySpawnsWithPets()) name += Translator.GetString("RequiresPetIndicator");
                __instance.TitleText.fontWeight = FontWeight.Black;
                __instance.TitleText.outlineColor = new(255, 255, 255, 255);
                __instance.TitleText.outlineWidth = 0.04f;
                __instance.LabelBackground.color = Utils.GetRoleColor(role);
                __instance.TitleText.color = Color.white;
                name = $"<size=3.5>{name}</size>";
                SetupHelpIcon(role, __instance);
            }

            __instance.TitleText.text = name;
            return false;
        }

        return true;
    }

    private static void SetupHelpIcon(CustomRoles role, StringOption option)
    {
        var template = option.transform.FindChild("MinusButton (1)");
        var icon = Object.Instantiate(template, template.parent, true);
        icon.name = $"{role}HelpIcon";
        var text = icon.FindChild("Plus_TMP").GetComponent<TextMeshPro>();
        text.text = "?";
        text.color = Color.white;
        icon.FindChild("InactiveSprite").GetComponent<SpriteRenderer>().color = Color.black;
        icon.FindChild("ActiveSprite").GetComponent<SpriteRenderer>().color = Color.gray;
        icon.localPosition += new Vector3(-3.4f, 0f, 0f);
        icon.SetAsLastSibling();
    }

    [HarmonyPatch(nameof(StringOption.UpdateValue)), HarmonyPrefix]
    private static bool UpdateValuePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];
            item.SetValue(__instance.GetInt());
            if (item.Name == "GameMode") GameOptionsMenuPatch.ReloadUI(ModGameOptionsMenu.TabIndex);

            var name = item.GetName();
            string name1 = name;
            if (Enum.GetValues<CustomRoles>().Find(x => Translator.GetString($"{x}") == name1.RemoveHtmlTags(), out var role))
            {
                name = name.RemoveHtmlTags();
                if (Options.UsePets.GetBool() && role.PetActivatedAbility()) name += Translator.GetString("SupportsPetIndicator");
                if (!Options.UsePets.GetBool() && role.OnlySpawnsWithPets()) name += Translator.GetString("RequiresPetIndicator");
                __instance.TitleText.fontWeight = FontWeight.Black;
                __instance.TitleText.outlineColor = new(255, 255, 255, 255);
                __instance.TitleText.outlineWidth = 0.04f;
                __instance.LabelBackground.color = Utils.GetRoleColor(role);
                __instance.TitleText.color = Color.white;
                name = $"<size=3.5>{name}</size>";
            }

            __instance.TitleText.text = name;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(StringOption.FixedUpdate)), HarmonyPrefix]
    private static bool FixedUpdatePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index))
        {
            var item = OptionItem.AllOptions[index];

            if (item is StringOptionItem stringOptionItem)
            {
                if (__instance.oldValue != __instance.Value)
                {
                    __instance.oldValue = __instance.Value;
                    __instance.ValueText.text = stringOptionItem.noTranslation
                        ? stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)]
                        : Translator.GetString(stringOptionItem.Selections[stringOptionItem.Rule.GetValueByIndex(__instance.Value)]);
                }
            }

            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(StringOption.Increase)), HarmonyPrefix]
    public static bool IncreasePrefix(StringOption __instance)
    {
        if (__instance.Value == __instance.Values.Length - 1)
        {
            __instance.Value = 0;
            __instance.UpdateValue();
            __instance.OnValueChanged?.Invoke(__instance);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(StringOption.Decrease)), HarmonyPrefix]
    public static bool DecreasePrefix(StringOption __instance)
    {
        if (ModGameOptionsMenu.OptionList.TryGetValue(__instance, out var index) && !__instance.transform.FindChild("MinusButton (1)").GetComponent<PassiveButton>().activeSprites.activeSelf)
        {
            var item = OptionItem.AllOptions[index];
            var name = item.GetName();
            if (Enum.GetValues<CustomRoles>().Find(x => Translator.GetString($"{x}") == name.RemoveHtmlTags(), out var role))
            {
                var roleName = role.IsVanilla() ? role + "TOZ" : role.ToString();
                var str = Translator.GetString($"{roleName}InfoLong");
                string infoLong;
                try
                {
                    infoLong = HnSManager.AllHnSRoles.Contains(role) ? str : str[(str.IndexOf('\n') + 1)..(str.Split("\n\n")[0].Length)];
                }
                catch
                {
                    infoLong = str;
                }

                var info = $"<size=70%>{role.ToColoredString()}: {infoLong}</size>";
                GameSettingMenu.Instance.MenuDescriptionText.text = info;
                return false;
            }
        }

        if (__instance.Value == 0)
        {
            __instance.Value = __instance.Values.Length - 1;
            __instance.UpdateValue();
            __instance.OnValueChanged?.Invoke(__instance);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(GameSettingMenu))]
public class GameSettingMenuPatch
{
    private static readonly Vector3 ButtonPositionLeft = new(-3.9f, -0.4f, 0f);
    private static readonly Vector3 ButtonPositionRight = new(-2.4f, -0.4f, 0f);

    private static readonly Vector3 ButtonSize = new(0.45f, 0.4f, 1f);
    // private static readonly Vector3 ButtonSize = new(0.45f, 0.6f, 1f);

    private static GameOptionsMenu TemplateGameOptionsMenu;
    private static PassiveButton TemplateGameSettingsButton;

    private static System.Collections.Generic.Dictionary<TabGroup, PassiveButton> ModSettingsButtons = [];
    private static System.Collections.Generic.Dictionary<TabGroup, GameOptionsMenu> ModSettingsTabs = [];

    public static NumberOption PresetBehaviour;
    public static StringOption GameModeBehaviour;

    public static FreeChatInputField InputField;
    private static System.Collections.Generic.List<OptionItem> HiddenBySearch = [];

    [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        ModSettingsButtons = [];
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var button = Object.Instantiate(TemplateGameSettingsButton, __instance.GameSettingsButton.transform.parent);
            button.gameObject.SetActive(true);
            button.name = "Button_" + tab;
            var label = button.GetComponentInChildren<TextMeshPro>();
            label.DestroyTranslator();
            label.text = Translator.GetString($"TabGroup.{tab}");
            label.color = Color.white;
            button.activeTextColor = button.inactiveTextColor = Color.white;
            button.selectedTextColor = new(0.7f, 0.7f, 0.7f);

            // var activeButton = Utils.LoadSprite($"TOZ.Resources.Images.TabIcon_{tab}.png", 100f);
            // button.inactiveSprites.GetComponent<SpriteRenderer>().sprite = activeButton /*Utils.LoadSprite($"TOZ.Resources.Tab_Small_{tab}.png", 100f)*/;
            // button.activeSprites.GetComponent<SpriteRenderer>().sprite = activeButton;
            // button.selectedSprites.GetComponent<SpriteRenderer>().sprite = activeButton;
            Color color = tab switch
            {
                TabGroup.SystemSettings => new(0.2f, 0.2f, 0.2f),
                TabGroup.GameSettings => new(0.2f, 0.4f, 0.3f),
                TabGroup.TaskSettings => new(0.4f, 0.2f, 0.5f),
                TabGroup.ImpostorRoles => new(0.5f, 0.2f, 0.2f),
                TabGroup.CrewmateRoles => new(0.2f, 0.4f, 0.5f),
                TabGroup.NeutralRoles => new(0.5f, 0.4f, 0.2f),
                TabGroup.Addons => new(0.5f, 0.2f, 0.4f),
                //TabGroup.OtherRoles => new(0.4f, 0.4f, 0.4f),
                TabGroup.ZloosSettings => new(0.5f, 0.1f, 0.1f),
                _ => new(0.3f, 0.3f, 0.3f)
            };
            button.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
            button.activeSprites.GetComponent<SpriteRenderer>().color = color;
            button.selectedSprites.GetComponent<SpriteRenderer>().color = color;

            Vector3 offset = new(0f, 0.4f * (((int)tab + 1) / 2), 0f);
            button.transform.localPosition = ((((int)tab + 1) % 2 == 0) ? ButtonPositionLeft : ButtonPositionRight) - offset;
            button.transform.localScale = ButtonSize;

            var buttonComponent = button.GetComponent<PassiveButton>();
            buttonComponent.OnClick = new();
            buttonComponent.OnClick.AddListener((Action)(() => __instance.ChangeTab((int)tab + 3, false)));

            ModSettingsButtons.Add(tab, button);
        }

        ModSettingsTabs = [];
        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            var setTab = Object.Instantiate(TemplateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
            setTab.name = "tab_" + tab;
            setTab.gameObject.SetActive(false);

            ModSettingsTabs.Add(tab, setTab);
        }

        foreach (var tab in Enum.GetValues<TabGroup>())
        {
            if (ModSettingsButtons.TryGetValue(tab, out var button))
            {
                __instance.ControllerSelectable.Add(button);
            }
        }

        HiddenBySearch.Do(x => x.SetHidden(false));
        HiddenBySearch.Clear();

        SetupExtendedUI(__instance);
    }

    // Thanks: Drakos for the preset button and search bar code (https://github.com/0xDrMoe/TownofHost-Enhanced/pull/1115)
    private static void SetupExtendedUI(GameSettingMenu __instance)
    {
        var ParentLeftPanel = __instance.GamePresetsButton.transform.parent;
        var preset = Object.Instantiate(GameObject.Find("ModeValue"), ParentLeftPanel);

        preset.transform.localPosition = new(-1.83f, 0.1f, -2f);
        preset.transform.localScale = new(0.65f, 0.63f, 1f);
        var renderer = preset.GetComponentInChildren<SpriteRenderer>();
        renderer.color = Color.white;
        renderer.sprite = null;

        var presetTmp = preset.GetComponentInChildren<TextMeshPro>();
        presetTmp.DestroyTranslator();
        presetTmp.text = Translator.GetString($"Preset_{OptionItem.CurrentPreset + 1}");

        var IsRussian = DestroyableSingleton<TranslationController>.Instance.currentLanguage.languageID == SupportedLangs.Russian;
        float size = !IsRussian ? 2.45f : 1.45f;
        presetTmp.fontSizeMax = presetTmp.fontSizeMin = size;


        var TempMinus = GameObject.Find("MinusButton").gameObject;
        var GMinus = Object.Instantiate(__instance.GamePresetsButton.gameObject, preset.transform);
        GMinus.gameObject.SetActive(true);
        GMinus.transform.localScale = new(0.08f, 0.4f, 1f);

        var MLabel = GMinus.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        MLabel.alignment = TextAlignmentOptions.Center;
        MLabel.DestroyTranslator();
        MLabel.text = "-";
        MLabel.transform.localPosition = new(MLabel.transform.localPosition.x, MLabel.transform.localPosition.y + 0.26f, MLabel.transform.localPosition.z);
        MLabel.color = Color.white;
        MLabel.SetFaceColor(new Color(255f, 255f, 255f));
        MLabel.transform.localScale = new(12f, 4f, 1f);

        var Minus = GMinus.GetComponent<PassiveButton>();
        Minus.OnClick.RemoveAllListeners();
        Minus.OnClick.AddListener((Action)(() =>
        {
            if (PresetBehaviour == null) __instance.ChangeTab(3, false);
            PresetBehaviour.Decrease();
        }));
        Minus.activeTextColor = Minus.inactiveTextColor = Minus.disabledTextColor = Minus.selectedTextColor = Color.white;

        Minus.transform.localPosition = new(-2f, -3.37f, -4f);
        Minus.inactiveSprites.GetComponent<SpriteRenderer>().sprite = TempMinus.GetComponentInChildren<SpriteRenderer>().sprite;
        Minus.activeSprites.GetComponent<SpriteRenderer>().sprite = TempMinus.GetComponentInChildren<SpriteRenderer>().sprite;
        Minus.selectedSprites.GetComponent<SpriteRenderer>().sprite = TempMinus.GetComponentInChildren<SpriteRenderer>().sprite;

        Minus.inactiveSprites.GetComponent<SpriteRenderer>().color = new Color32(55, 59, 60, 255);
        Minus.activeSprites.GetComponent<SpriteRenderer>().color = new Color32(0, 255, 165, 255);
        Minus.selectedSprites.GetComponent<SpriteRenderer>().color = new Color32(0, 165, 255, 255);


        var PlusFab = Object.Instantiate(GMinus, preset.transform);
        var PLuLabel = PlusFab.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        PLuLabel.alignment = TextAlignmentOptions.Center;
        PLuLabel.DestroyTranslator();
        PLuLabel.text = "+";
        PLuLabel.color = Color.white;
        PLuLabel.transform.localPosition = new(PLuLabel.transform.localPosition.x, PLuLabel.transform.localPosition.y + 0.26f, PLuLabel.transform.localPosition.z);
        PLuLabel.transform.localScale = new(18f, 4f, 1f);

        var plus = PlusFab.GetComponent<PassiveButton>();
        plus.OnClick.RemoveAllListeners();
        plus.OnClick.AddListener((Action)(() =>
        {
            if (PresetBehaviour == null) __instance.ChangeTab(3, false);
            PresetBehaviour.Increase();
        }));
        plus.activeTextColor = plus.inactiveTextColor = plus.disabledTextColor = plus.selectedTextColor = Color.white;
        plus.transform.localPosition = new(-0.4f, -3.37f, -4f);


        var GameSettingsLabel = __instance.GameSettingsButton.transform.parent.parent.FindChild("GameSettingsLabel").GetComponent<TextMeshPro>();
        GameSettingsLabel.DestroyTranslator();
        GameSettingsLabel.text = Translator.GetString($"Mode{Options.CurrentGameMode}").Split(':').Last().TrimStart(' ');
        if (IsRussian)
        {
            
            GameSettingsLabel.transform.localScale = new(0.7f, 0.7f, 1f);
            GameSettingsLabel.transform.localPosition = new Vector3(-3.77f, 1.62f, -4);
        }
        var GameSettingsLabelPos = GameSettingsLabel.transform.localPosition;

        var gmCycler = Object.Instantiate(GMinus, GameSettingsLabel.transform, true);

        gmCycler.transform.localScale = new(0.25f, 0.7f, 1f);
        gmCycler.transform.localPosition = new(GameSettingsLabelPos.x + 0.8f, GameSettingsLabelPos.y - 2.9f, GameSettingsLabelPos.z);
        var gmTmp = gmCycler.transform.Find("FontPlacer/Text_TMP").GetComponent<TextMeshPro>();
        gmTmp.alignment = TextAlignmentOptions.Center;
        gmTmp.DestroyTranslator();
        gmTmp.text = "\u21c4";
        gmTmp.color = Color.white;
        var Offset2 = !IsRussian ? 3.35f : 3.65f;
        gmTmp.transform.localPosition = new(GameSettingsLabelPos.x + Offset2, GameSettingsLabelPos.y - 1.52f, GameSettingsLabelPos.z);
        gmTmp.transform.localScale = new(4f, 1.5f, 1f);

        var cycle = gmCycler.GetComponent<PassiveButton>();
        cycle.OnClick.RemoveAllListeners();
        cycle.OnClick.AddListener((Action)(() =>
        {
            if (GameModeBehaviour == null) __instance.ChangeTab(4, false);
            //__instance.ChangeTab(4, false);
            GameModeBehaviour.Increase();
        }));
        var Offset = !IsRussian ? 1.15f : 2.25f;
        cycle.activeTextColor = cycle.inactiveTextColor = cycle.disabledTextColor = cycle.selectedTextColor = Color.white;
        cycle.transform.localPosition = new(Offset, 0.08f, 1f);


        var FreeChatField = DestroyableSingleton<ChatController>.Instance.freeChatField;
        var TextField = Object.Instantiate(FreeChatField, ParentLeftPanel.parent);
        TextField.transform.localScale = new(0.3f, 0.59f, 1);
        TextField.transform.localPosition = new(-0.8f, -2.57f, -5f);
        TextField.textArea.outputText.transform.localScale = new(3.5f, 2f, 1f);
        TextField.textArea.outputText.font = PLuLabel.font;

        InputField = TextField;

        var button = TextField.transform.FindChild("ChatSendButton");

        Object.Destroy(button.FindChild("Normal").FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(button.FindChild("Hover").FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(button.FindChild("Disabled").FindChild("Icon").GetComponent<SpriteRenderer>());
        Object.Destroy(button.transform.FindChild("Text").GetComponent<TextMeshPro>());

        button.FindChild("Normal").FindChild("Background").GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("TOZ.Resources.Images.SearchIconActive.png", 100f);
        button.FindChild("Hover").FindChild("Background").GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("TOZ.Resources.Images.SearchIconHover.png", 100f);
        button.FindChild("Disabled").FindChild("Background").GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite("TOZ.Resources.Images.SearchIcon.png", 100f);

        if (IsRussian)
        {
            Vector3 FixedScale = new(0.7f, 1f, 1f);
            button.FindChild("Normal").FindChild("Background").transform.localScale = FixedScale;
            button.FindChild("Hover").FindChild("Background").transform.localScale = FixedScale;
            button.FindChild("Disabled").FindChild("Background").transform.localScale = FixedScale;
        }


        PassiveButton passiveButton = button.GetComponent<PassiveButton>();

        passiveButton.OnClick = new();
        passiveButton.OnClick.AddListener((Action)(() => SearchForOptions(TextField)));
        return;


        static void SearchForOptions(FreeChatInputField textField)
        {
            if (ModGameOptionsMenu.TabIndex < 3) return;

            HiddenBySearch.Do(x => x.SetHidden(false));
            string text = textField.textArea.text.Trim().ToLower();
            var Result = OptionItem.AllOptions.Where(x => x.Parent == null && !x.IsHiddenOn(Options.CurrentGameMode) && !Translator.GetString($"{x.Name}").Contains(text, StringComparison.OrdinalIgnoreCase) && x.Tab == (TabGroup)(ModGameOptionsMenu.TabIndex - 3)).ToList();
            HiddenBySearch = Result;
            var SearchWinners = OptionItem.AllOptions.Where(x => x.Parent == null && !x.IsHiddenOn(Options.CurrentGameMode) && x.Tab == (TabGroup)(ModGameOptionsMenu.TabIndex - 3) && !Result.Contains(x)).ToList();

            if (SearchWinners.Count == 0 || !ModSettingsTabs.TryGetValue((TabGroup)(ModGameOptionsMenu.TabIndex - 3), out var GameSettings) || GameSettings == null)
            {
                HiddenBySearch.Clear();
                Logger.SendInGame(Translator.GetString("SearchNoResult"));
                return;
            }

            Result.ForEach(x => x.SetHidden(true));

            GameOptionsMenuPatch.ReCreateSettings(GameSettings);
            textField.Clear();
        }
    }

    private static void SetDefaultButton(GameSettingMenu __instance)
    {
        __instance.GamePresetsButton.gameObject.SetActive(false);

        var gameSettingButton = __instance.GameSettingsButton;
        gameSettingButton.transform.localPosition = new(-3f, -0.4f, 0f);

        var textLabel = gameSettingButton.GetComponentInChildren<TextMeshPro>();
        textLabel.DestroyTranslator();
        textLabel.text = Translator.GetString("TabGroup.VanillaSettings");

        gameSettingButton.activeTextColor = gameSettingButton.inactiveTextColor = Color.white;
        gameSettingButton.selectedTextColor = Color.gray;

        gameSettingButton.inactiveSprites.GetComponent<SpriteRenderer>().color = Color.black;
        gameSettingButton.activeSprites.GetComponent<SpriteRenderer>().color = Color.gray;
        gameSettingButton.selectedSprites.GetComponent<SpriteRenderer>().color = Color.black;

        gameSettingButton.transform.localPosition = ButtonPositionLeft;
        gameSettingButton.transform.localScale = ButtonSize;

        __instance.RoleSettingsButton.gameObject.SetActive(true);

        __instance.DefaultButtonSelected = gameSettingButton;
        __instance.ControllerSelectable = new();
        __instance.ControllerSelectable.Add(gameSettingButton);
    }

    [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
    public static bool ChangeTabPrefix(GameSettingMenu __instance, ref int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        if (HiddenBySearch.Any())
        {
            HiddenBySearch.Do(x => x.SetHidden(false));
            if (ModSettingsTabs.TryGetValue((TabGroup)(ModGameOptionsMenu.TabIndex - 3), out var GameSettings) && GameSettings != null)
                GameOptionsMenuPatch.ReCreateSettings(GameSettings);

            HiddenBySearch.Clear();
        }

        if (!previewOnly || tabNum != 1) ModGameOptionsMenu.TabIndex = tabNum;

        GameOptionsMenu settingsTab;
        PassiveButton button;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            foreach (var tab in Enum.GetValues<TabGroup>())
            {
                if (ModSettingsTabs.TryGetValue(tab, out settingsTab) && settingsTab != null)
                {
                    settingsTab.gameObject.SetActive(false);
                }
            }

            foreach (var tab in Enum.GetValues<TabGroup>())
            {
                if (ModSettingsButtons.TryGetValue(tab, out button) && button != null)
                {
                    button.SelectButton(false);
                }
            }
        }

        if (tabNum < 3) return true;

        TabGroup tabGroup = (TabGroup)(tabNum - 3);
        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            __instance.PresetsTab.gameObject.SetActive(false);
            __instance.GameSettingsTab.gameObject.SetActive(false);
            __instance.RoleSettingsTab.gameObject.SetActive(false);
            __instance.GamePresetsButton.SelectButton(false);
            __instance.GameSettingsButton.SelectButton(false);
            __instance.RoleSettingsButton.SelectButton(false);

            if (ModSettingsTabs.TryGetValue(tabGroup, out settingsTab) && settingsTab != null)
            {
                settingsTab.gameObject.SetActive(true);
                __instance.MenuDescriptionText.DestroyTranslator();
                __instance.MenuDescriptionText.text = Translator.GetString("TabInfoTip");
            }
        }

        if (previewOnly)
        {
            __instance.ToggleLeftSideDarkener(false);
            __instance.ToggleRightSideDarkener(true);
            return false;
        }

        __instance.ToggleLeftSideDarkener(true);
        __instance.ToggleRightSideDarkener(false);

        if (ModSettingsButtons.TryGetValue(tabGroup, out button) && button != null)
        {
            button.SelectButton(true);
        }

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.OnEnable)), HarmonyPrefix]
    private static bool OnEnablePrefix(GameSettingMenu __instance)
    {
        if (TemplateGameOptionsMenu == null)
        {
            TemplateGameOptionsMenu = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
            TemplateGameOptionsMenu.gameObject.SetActive(false);
        }

        if (TemplateGameSettingsButton == null)
        {
            TemplateGameSettingsButton = Object.Instantiate(__instance.GameSettingsButton, __instance.GameSettingsButton.transform.parent);
            TemplateGameSettingsButton.gameObject.SetActive(false);
        }

        SetDefaultButton(__instance);

        ModGameOptionsMenu.OptionList = new();
        ModGameOptionsMenu.BehaviourList = new();
        ModGameOptionsMenu.CategoryHeaderList = new();

        ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable);
        DestroyableSingleton<HudManager>.Instance.menuNavigationPrompts.SetActive(false);
        if (Controller.currentTouchType != Controller.TouchType.Joystick)
        {
            __instance.ChangeTab(1, false);
        }

        __instance.StartCoroutine(__instance.CoSelectDefault());

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.Close)), HarmonyPostfix]
    public static void ClosePostfix()
    {
        foreach (var button in ModSettingsButtons.Values) Object.Destroy(button);
        foreach (var tab in ModSettingsTabs.Values) Object.Destroy(tab);
        ModSettingsButtons = [];
        ModSettingsTabs = [];
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
public static class FixInputChatField
{
    public static bool Prefix(FreeChatInputField __instance)
    {
        if (GameSettingMenuPatch.InputField != null && __instance == GameSettingMenuPatch.InputField)
        {
            Vector2 size = __instance.Background.size;
            size.y = Math.Max(0.62f, __instance.textArea.TextHeight + 0.2f);
            __instance.Background.size = size;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
public static class FixDarkThemeForSearchBar
{
    public static void Postfix()
    {
        if (!GameSettingMenu.Instance) return;
        var field = GameSettingMenuPatch.InputField;
        if (field != null)
        {
            field.background.color = new Color32(40, 40, 40, byte.MaxValue);
            field.textArea.compoText.Color(Color.white);
            field.textArea.outputText.color = Color.white;
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
public class RpcSyncSettingsPatch
{
    public static void Postfix()
    {
        OptionItem.SyncAllOptions();
    }
}

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.ChangeTab))]
public class TabChange
{
    public static void Prefix(ref int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        if (tabNum == 0)
        { // Disables preset menu in any instances
            tabNum = 1;
        }
    }
    public static void Postfix(GameSettingMenu __instance, [HarmonyArgument(0)] int tabNum)
    {

        if (tabNum == 1 && __instance.GameSettingsTab.isActiveAndEnabled)
        {
            _ = new LateTask(() => __instance.MenuDescriptionText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.GameSettingsDescription), 0.05f, "Fix Menu Description Text");
            return;
        }

    }



}
[HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.Start))]
public static class RolesSettingsMenuAwakePatch
{
    public static void Postfix(RolesSettingsMenu __instance)
    {
        //Transform mainAreaTransform = __instance.transform.Find("MainArea");
        //RolesSettingsMenu roleTabMenu = mainAreaTransform.Find("ROLES TAB").GetComponent<RolesSettingsMenu>();
        //Logger.Info($"{roleTabMenu == null}", "Check");
        //if (roleTabMenu == null) return;

        //__instance.
        //roleTabMenu.

        //var toheRoleSettings = Object.Instantiate(roleTabMenu, roleTabMenu.transform.parent);

        //toheRoleSettings.name = "TEST ADSDSF";
        //toheRoleSettings.enabled = true;
        //toheRoleSettings.
    }
}
//[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Update))]
public class GameOptionsMenuUpdatePatch
{
    private static float _timer = 1f;

    public static void Postfix(GameOptionsMenu __instance)
    {
        if (__instance.transform.parent.parent.name == "Game Settings") return;

        /*if (GameStates.IsHideNSeek)
        {
            Main.HideNSeekOptions.NumImpostors = Options.NumImpostorsHnS.GetInt();
        }*/

        foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
        {
            string tabcolor = tab switch
            {
                TabGroup.SystemSettings => Main.ModColor,
                TabGroup.GameSettings => "#59ef83",
                TabGroup.TaskSettings => "#EF59AF",
                TabGroup.ImpostorRoles => "#f74631",
                TabGroup.CrewmateRoles => "#8cffff",
                TabGroup.NeutralRoles => "#7f8c8d",
                TabGroup.Addons => "#ff9ace",
                _ => "#ffffff",
            };
            if (__instance.transform.parent.parent.name != tab + "Tab") continue;
            __instance.transform.Find("../../GameGroup/Text").GetComponent<TextMeshPro>().SetText($"<color={tabcolor}>"); //+ GetString("TabGroup." + tab) + "</color>");

            _timer += Time.deltaTime;
            if (_timer < 0.1f) return;
            _timer = 0f;

            float numItems = __instance.Children.Count;
            var offset = 2.7f;

            foreach (var option in OptionItem.AllOptions.Where(opt => tab == opt.Tab && opt.OptionBehaviour != null && opt.OptionBehaviour.gameObject != null).ToArray())
            {
                var enabled = true;
                var parent = option.Parent;

                enabled = AmongUsClient.Instance.AmHost &&
                    !option.IsHiddenOn(Options.CurrentGameMode);

                var opt = option.OptionBehaviour.transform.Find("Background").GetComponent<SpriteRenderer>();
                opt.size = new(5.0f, 0.45f);
                while (parent != null && enabled)
                {
                    enabled = parent.GetBool() && !parent.IsHiddenOn(Options.CurrentGameMode);
                    parent = parent.Parent;
                    opt.color = new(0f, 1f, 0f);
                    opt.size = new(4.8f, 0.45f);

                    /*if (!Main.Made.Value)
                    {
                        opt.transform.localPosition = new Vector3(0.11f, 0f);
                        option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-1.08f, 0f);
                        option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(5.1f, 0.28f);
                        if (option.Parent?.Parent != null)
                        {
                            opt.color = new(0f, 0f, 1f);
                            opt.size = new(4.6f, 0.45f);
                            opt.transform.localPosition = new Vector3(0.24f, 0f);
                            option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-0.88f, 0f);
                            option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(4.9f, 0.28f);
                            if (option.Parent?.Parent?.Parent != null)
                            {
                                opt.color = new(1f, 0f, 0f);
                                opt.size = new(4.4f, 0.45f);
                                opt.transform.localPosition = new Vector3(0.37f, 0f);
                                option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-0.68f, 0f);
                                option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(4.7f, 0.28f);
                            }
                        }
                    }*/
                    //else
                    if (true)
                    {
                        option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-0.95f, 0f);
                        option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(3.4f, 0.37f);
                        if (option.Parent?.Parent != null)
                        {
                            opt.color = new(0f, 0f, 1f);
                            opt.size = new(4.6f, 0.45f);
                            opt.transform.localPosition = new Vector3(0.24f, 0f);
                            option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-0.7f, 0f);
                            option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(3.3f, 0.37f);
                            if (option.Parent?.Parent?.Parent != null)
                            {
                                opt.color = new(1f, 0f, 0f);
                                opt.size = new(4.4f, 0.45f);
                                opt.transform.localPosition = new Vector3(0.37f, 0f);
                                option.OptionBehaviour.transform.Find("Title_TMP").transform.localPosition = new Vector3(-0.55f, 0f);
                                option.OptionBehaviour.transform.Find("Title_TMP").GetComponent<RectTransform>().sizeDelta = new Vector2(3.2f, 0.37f);
                            }
                        }
                    }
                }

                if (option.IsText)
                {
                    opt.color = new(0, 0, 0);
                    opt.transform.localPosition = new(300f, 300f, 300f);
                }

                option.OptionBehaviour.gameObject.SetActive(enabled);
                if (enabled)
                {
                    offset -= option.IsHeader ? 0.7f : 0.5f;
                    option.OptionBehaviour.transform.localPosition = new Vector3(
                        option.OptionBehaviour.transform.localPosition.x,
                        offset,
                        option.OptionBehaviour.transform.localPosition.z);

                    if (option.IsHeader)
                    {
                        //if (!Main.ModeForSmallScreen.Value)
                        //    numItems += 0.3f;
                        //else
                            numItems += 0.5f;
                    }
                }
                else
                {
                    numItems--;
                }
            }
            __instance.GetComponentInParent<Scroller>().ContentYBounds.max = (-offset) - 1.5f;
        }
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.SetUpFromData))]
public class StringOptionEnablePatch
{
    public static void Postfix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return;

        __instance.OnValueChanged = new Action<OptionBehaviour>((o) => { });
        /*if (option.van)
        {
            __instance.TitleText.text = option.GetNameVanilla();
        }
        else*/ if(true)
        {
            __instance.TitleText.text = option.GetName();
        }
        __instance.Value = __instance.oldValue = option.CurrentValue;
        __instance.ValueText.text = option.GetString();
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
public class StringOptionIncreasePatch
{
    public static bool Prefix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return true;

        if (option.Name == "GameMode")
        {
            var gameModeCount = Options.GameMode.CurrentValue - 1;
            switch (GameOptionsManager.Instance.CurrentGameOptions.GameMode)
            {
                // To prevent the Host from selecting CustomGameMode.HidenSeekTOHE
                case GameModes.NormalFools when option.CurrentValue == 0:
                //case GameModes.Normal when option.CurrentValue == gameModeCount - 1:
                // To prevent the Host from selecting CustomGameMode.Standard/FFA
                case GameModes.SeekFools when option.CurrentValue == gameModeCount:
                case GameModes.HideNSeek when option.CurrentValue == gameModeCount:
                    return false;
                default:
                    break;
            }
        }

        option.SetValue(option.CurrentValue + (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));

        if (option.Name == "Preset")
        {
            /*if (GameStates.hid)
            {
                // Set Hide & Seek game mode
                Options.GameMode.SetValue(2);
            }
            else if (Options.CurrentGameMode != CustomGameMode.Standard)
            {
                // Set standart game mode
                Options.GameMode.SetValue(0);
            }*/
        }
        return false;
    }
}

[HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
public class StringOptionDecreasePatch
{
    public static bool Prefix(StringOption __instance)
    {
        var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
        if (option == null) return true;

        if (option.Name == "GameMode")
        {
            switch (GameOptionsManager.Instance.CurrentGameOptions.GameMode)
            {
                // To prevent the Host from selecting CustomGameMode.HidenSeekTOHE
                case GameModes.NormalFools when option.CurrentValue == 0:
                case GameModes.Normal when option.CurrentValue == 0:
                // To prevent the Host from selecting CustomGameMode.Standard/FFA
                case GameModes.SeekFools when option.CurrentValue == Options.GameMode.CurrentValue - 1:
                case GameModes.HideNSeek when option.CurrentValue == Options.GameMode.CurrentValue - 1:
                    return false;
                default:
                    break;
            }
        }

        option.SetValue(option.CurrentValue - (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));

        if (option.Name == "Preset")
        {
            /*if (GameStates.IsHideNSeek)
            {
                // Set Hide & Seek game mode
                Options.GameMode.SetValue(2);
            }
            else if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
            {
                // Set standart game mode
                Options.GameMode.SetValue(0);
            }*/
        }
        return false;
    }
}
[HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.ChangeTab))]
public static class RolesSettingsMenu_ChangeTabPatch
{
    public static void Postfix(RolesSettingsMenu __instance)
    {
        //if (GameStates.IsHideNSeek) return;

        foreach (var ob in __instance.advancedSettingChildren.ToArray())
        {
            switch (ob.Title)
            {
                case StringNames.EngineerCooldown:
                    ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                    break;
                case StringNames.ShapeshifterCooldown:
                    ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                    break;
                default:
                    break;
            }
        }
    }
}
/*[HarmonyPatch(typeof(RolesSettingsMenu))]
public static class RolesSettingsMenu_ChanceTabPatch
{
    [HarmonyPatch(nameof(RolesSettingsMenu.SetQuotaTab)), HarmonyPrefix]
    public static bool SetQuotaTab(RolesSettingsMenu __instance)
    {
        //if (GameStates.IsHideNSeek) return true;

        float num2 = -1.928f;
        //__instance.roleTabs = new List<PassiveButton>().ToIl2Cpp();

        //List<RoleRulesCategory> list = GameManager.Instance.GameSettingsList.AllRoles.ToManaged().FindAll((RoleRulesCategory cat) => cat.Role.TeamType == RoleTeamTypes.Crewmate);
        //List<RoleRulesCategory> list2 = GameManager.Instance.GameSettingsList.AllRoles.ToManaged().FindAll((RoleRulesCategory cat) => cat.Role.TeamType == RoleTeamTypes.Impostor);

        // Impostor Tab
        AddRoleTabCustom(__instance, VanillaLikeRoleTypes.Impostor, ref num2);

        // Neutral Tab
        AddRoleTabCustom(__instance, VanillaLikeRoleTypes.Neutral, ref num2);
        return false;
    }

    [HarmonyPatch(nameof(RolesSettingsMenu.OpenChancesTab)), HarmonyPrefix]
    public static bool OpenChancesTab(RolesSettingsMenu __instance)
    {
        __instance.selectedRoleTab = 0;
        __instance.RoleChancesSettings.SetActive(true);

        _ = new LateTask(() =>
        {
            LoadRoleOptions(__instance, VanillaLikeRoleTypes.Crewmate);
        }, 0.1f, "OpenFirstChancesTab");

        return false;
    }

    public static void CreateCustomQuotaOption(RolesSettingsMenu inst, RoleBase dog, ref float yPos)
    {
        RoleOptionSetting roleOptionSetting = Object.Instantiate(inst.roleOptionSettingOrigin, Vector3.zero, Quaternion.identity, inst.RoleChancesSettings.transform);
        roleOptionSetting.transform.localPosition = new Vector3(-0.15f, yPos, -2f);
        roleOptionSetting.transform.Find("Role #").gameObject.SetActive(false);

        // Set Role
        SpriteRenderer[] componentsInChildren = roleOptionSetting.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            componentsInChildren[i].material.SetInt(PlayerMaterial.MaskLayer, 20);
        }
        foreach (TextMeshPro textMeshPro in roleOptionSetting.GetComponentsInChildren<TextMeshPro>(true))
        {
            textMeshPro.fontMaterial.SetFloat("_StencilComp", 3f);
            textMeshPro.fontMaterial.SetFloat("_Stencil", 20);
        }
        roleOptionSetting.role = RoleManager.Instance.AllRoles[0];
        roleOptionSetting.titleText.text = Utils.GetRoleName(dog.ThisCustomRole);

        if (dog.ThisCustomRole.IsImpostorTeam())
        {
            roleOptionSetting.labelSprite.color = Palette.ImpostorRoleRed;
        }
        else if (dog.ThisCustomRole.IsNeutral())
        {
            _ = ColorUtility.TryParseHtmlString("#7f8c8d", out Color c);
            roleOptionSetting.labelSprite.color = c;
        }
        else
        {
            roleOptionSetting.labelSprite.color = Palette.CrewmateRoleBlue;
        }

        PassiveButton minusButton = roleOptionSetting.transform.Find("Chance %").Find("MinusButton (1)").GetComponent<PassiveButton>();
        PassiveButton plusButton = roleOptionSetting.transform.Find("Chance %").Find("PlusButton (1)").GetComponent<PassiveButton>();

        plusButton.OnClick.RemoveAllListeners();
        minusButton.OnClick.RemoveAllListeners();
        plusButton.OnClick.AddListener(new Action(() => IncreaseChance(roleOptionSetting, dog.ThisCustomRole)));
        minusButton.OnClick.AddListener(new Action(() => DecreaseChance(roleOptionSetting, dog.ThisCustomRole)));

        roleOptionSetting.OnValueChanged = new Action<OptionBehaviour>((OptionBehaviour a) => OnValueChanged(a, dog.ThisCustomRole));
        roleOptionSetting.SetClickMask(inst.ButtonClickMask);
        inst.roleChances.Add(roleOptionSetting);
        //List<UiElement> quotaMono = inst.QuotaTabSelectables.ToManaged();
        //quotaMono.AddRange(roleOptionSetting.ControllerSelectable.ToManaged());
        //inst.QuotaTabSelectables = quotaMono.ToIl2Cpp();
        OnValueChanged(roleOptionSetting, Enum.Parse<CustomRoles>(dog.GetType().Name, true));
        yPos -= 0.43f;
    }

    private static void IncreaseChance(OptionBehaviour option, CustomRoles role)
    {
        float chance = Options.GetRoleChance(role);

        Options.SetRoleChance(role, chance + 1f);
        OnValueChanged(option, role);
    }

    private static void DecreaseChance(OptionBehaviour option, CustomRoles role)
    {
        float chance = Options.GetRoleChance(role);

        Options.SetRoleChance(role, chance - 1f);
        OnValueChanged(option, role);
    }

    private static void OnValueChanged(OptionBehaviour optionnonconv, CustomRoles role)
    {
        RoleOptionSetting option = optionnonconv as RoleOptionSetting;
        if (option == null) return;

        float roleChance = Options.GetRoleChance(role) * 5f;
        int roleCount = Options.GetRoleCount(role);

        option.roleMaxCount = roleCount;
        option.roleChance = (int)roleChance;
        option.countText.text = option.roleMaxCount.ToString();
        option.chanceText.text = option.roleChance.ToString();
    }


    private static RoleSettingsTabButton AddRoleTabCustom(RolesSettingsMenu thiz, VanillaLikeRoleTypes roleType, ref float tabXPos)
    {
        thiz.selectedRoleTab = (int)roleType;
        RoleSettingsTabButton tab = null;
        switch (roleType)
        {
            case VanillaLikeRoleTypes.Crewmate:
                tab = Object.Instantiate(thiz.roleSettingsTabButtonOrigin, Vector3.zero, Quaternion.identity, thiz.tabParent);
                break;
            case VanillaLikeRoleTypes.Impostor:
                tab = Object.Instantiate(thiz.roleSettingsTabButtonOriginImpostor, Vector3.zero, Quaternion.identity, thiz.tabParent);
                break;
            case VanillaLikeRoleTypes.Neutral:
                tab = Object.Instantiate(thiz.roleSettingsTabButtonOrigin, Vector3.zero, Quaternion.identity, thiz.tabParent);
                RoleBehaviour impRole = RoleManager.Instance.AllRoles.Where(r => r.Role == RoleTypes.Shapeshifter).FirstOrDefault();
                tab.icon.sprite = impRole.RoleIconWhite;
                SetTabColor(tab, "#7f8c8d");
                break;
            default:
                throw new InvalidOperationException("Only Crewmate, Impostor and Neutral are supported!");
        }
        tab.transform.localPosition = new Vector3(tabXPos, 2.27f, -2f);

        tab.button.OnClick.AddListener(new Action(() =>
        {
            LoadRoleOptions(thiz, roleType);
        }));
        tabXPos += 0.762f;
        thiz.roleTabs.Add(tab.Button);
        return tab;
    }

    private static void LoadRoleOptions(RolesSettingsMenu thiz, VanillaLikeRoleTypes type)
    {
        Logger.Info(type.ToString(), "LoadRoleOptions");

        foreach (var header in thiz.RoleChancesSettings.GetComponentsInChildren<CategoryHeaderEditRole>())
        {
            Object.Destroy(header.gameObject);
        }

        foreach (var opt in thiz.roleChances)
        {
            if (opt != null)
            {
                Object.Destroy(opt.gameObject);
            }
        }
        thiz.roleChances.Clear();

        Dictionary<string, List<RoleBase>> roles = [];
        switch (type)
        {
            case VanillaLikeRoleTypes.Crewmate:
                roles.Add("Vanilla", CustomRoleManager.GetNormalOptions(RoleOptionType.Crewmate_Normal));
                roles.Add("Imposter based", CustomRoleManager.GetNormalOptions(RoleOptionType.Crewmate_ImpostorBased));
                //roles.Add("Vanilla Ghost", CustomRoleManager.GetNormalOptions(CustomRoleTypes.CrewmateVanillaGhosts));
                //roles.Add("Ghost", CustomRoleManager.GetNormalOptions(RoleOptionType.CrewmateGhosts));
                break;
            case VanillaLikeRoleTypes.Impostor:
                roles.Add("Vanilla", CustomRoleManager.GetNormalOptions(RoleOptionType.Impostor));
                //roles.Add("Support", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorSupport));
                //roles.Add("Killing", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorKilling));
                //roles.Add("Hindering", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorHindering));
                //roles.Add("Concealing", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorConcealing));
                //roles.Add("Ghost", CustomRoleManager.GetNormalOptions(RoleOptionType.ImpostorGhosts));
                break;
            case VanillaLikeRoleTypes.Neutral:
                roles.Add("Benign", CustomRoleManager.GetNormalOptions(RoleOptionType.Neutral_NonKilling));
                roles.Add("Evil", CustomRoleManager.GetNormalOptions(RoleOptionType.Neutral_Killing));
                //roles.Add("Killing", CustomRoleManager.GetNormalOptions(RoleOptionType.NeutralKilling));
                //roles.Add("Chaos", CustomRoleManager.GetNormalOptions(RoleOptionType.NeutralChaos));
                break;
        }

        float num = 0.662f;

        CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(thiz.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, thiz.RoleChancesSettings.transform);

        // Set Header
        categoryHeaderEditRole.Title.text = type.ToString();//GetString(type.ToString());
        categoryHeaderEditRole.Background.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Divider?.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);

        categoryHeaderEditRole.transform.localPosition = new Vector3(4.986f, num, -2f);
        num -= 0.522f;
        int num3 = 0;
        foreach (var roleCategory in roles)
        {
            CreateHeader(roleCategory.Key, thiz, ref num);
            num3++;
            foreach (var role in roleCategory.Value)
            {
                Debug.Log(role.ToString());
                CreateCustomQuotaOption(thiz, role, ref num);
                num3++;
            }
        }

        thiz.scrollBar.ContentYBounds.max = (num3 / 2.4f); // 2.5f works better sometimes, needs to find the correct number
        Logger.Info($"{type} Count: {roles.Count}", "LoadRoleOptions");
    }

    // Additional methods

    private static void CreateHeader(string name, RolesSettingsMenu thiz, ref float yPos)
    {
        CategoryHeaderEditRole categoryHeaderEditRole = Object.Instantiate(thiz.categoryHeaderEditRoleOrigin, Vector3.zero, Quaternion.identity, thiz.RoleChancesSettings.transform);
        categoryHeaderEditRole.transform.Find("QuotaHeader").gameObject.SetActive(false); ;
        categoryHeaderEditRole.transform.localPosition = new Vector3(4.98f, yPos - 0.1f, -2f);
        categoryHeaderEditRole.Title.text = name;
        categoryHeaderEditRole.Background.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Divider?.material.SetInt(PlayerMaterial.MaskLayer, 20);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_StencilComp", 3f);
        categoryHeaderEditRole.Title.fontMaterial.SetFloat("_Stencil", 20);
        yPos -= 0.43f;
    }

    private static void SetTabColor(RoleSettingsTabButton tab, string hex)
    {
        if (tab == null) return;

        Color color = Color.blue;
        hex = hex.TrimStart('#');

        if (hex.Length != 6)
        {
            throw new InvalidOperationException("Hex color should be 6 characters long excluding the '#' symbol.");
        }

        float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;

        color = new Color(r, g, b);
        if (tab.button.inactiveSprites.GetComponent<SpriteRenderer>() != null && tab.button.activeSprites.GetComponent<SpriteRenderer>() != null)
        {
            SpriteRenderer inactiveSprite = tab.button.inactiveSprites.GetComponent<SpriteRenderer>();
            SpriteRenderer activeSprite = tab.button.inactiveSprites.GetComponent<SpriteRenderer>();
            inactiveSprite.color = GetInactiveColor(color);
            activeSprite.color = color;
        }
    }

    private static Color GetInactiveColor(this Color color, float shadowFactor = 0.5f)
    {
        shadowFactor = Mathf.Max(1.0f, shadowFactor);

        Color shadowColor = new(
            color.r / shadowFactor,
            color.g / shadowFactor,
            color.b / shadowFactor,
            color.a
        );

        return shadowColor;
    }

    enum VanillaLikeRoleTypes : int
    {
        Crewmate = 0,
        Impostor = 1,
        Neutral = 2,
    }
}*/