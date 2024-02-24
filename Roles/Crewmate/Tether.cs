using System.Collections.Generic;
using System.Text;
using Hazel;

namespace TOHE.Roles.Crewmate
{
    using static Options;

    public static class Tether
    {
        private static readonly int Id = 640300;
        public static List<byte> playerIdList = [];
        private static byte Target = byte.MaxValue;

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem TetherAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tether, 1);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Times);
            TetherAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 14, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Times);
            CancelVote = CreateVoteCancellingUseSetting(Id + 13, CustomRoles.Tether, TabGroup.CrewmateRoles);
        }
        public static void Init()
        {
            playerIdList = [];
            Target = byte.MaxValue;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPCSyncTarget(byte targetId)
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTetherTarget, SendOption.Reliable);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncTarget(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            Target = reader.ReadByte();
        }
        public static void OnEnterVent(PlayerControl pc, int ventId, bool isPet = false)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Tether)) return;

            if (Target != byte.MaxValue)
            {
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask)
                    {
                        pc.TP(Utils.GetPlayerById(Target).Pos());
                    }
                }, isPet ? 0.1f : 2f, "Tether TP");
            }
            else if (!isPet)
            {
                _ = new LateTask(() =>
                {
                    pc.MyPhysics?.RpcBootFromVent(ventId);
                }, 0.5f, "Tether No Target Boot From Vent");
            }
        }
        public static bool OnVote(PlayerControl pc, PlayerControl target)
        {
            if (pc == null || target == null || !pc.Is(CustomRoles.Tether) || pc.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                Target = target.PlayerId;
                SendRPCSyncTarget(Target);
                Main.DontCancelVoteList.Add(pc.PlayerId);
                return true;
            }
            return false;
        }
        public static void OnReportDeadBody()
        {
            Target = byte.MaxValue;
            SendRPCSyncTarget(Target);
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            var sb = new StringBuilder();

            sb.Append(Utils.GetTaskCount(playerId, comms));
            sb.Append(Utils.GetAbilityUseLimitDisplay(playerId, Target != byte.MaxValue));

            return sb.ToString();
        }
        public static string TargetText => Target != byte.MaxValue ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{Utils.GetPlayerById(Target).GetRealName()}</color>" : string.Empty;
    }
}