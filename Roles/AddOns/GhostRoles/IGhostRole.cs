﻿namespace TOZ.AddOns.GhostRoles
{
    public interface IGhostRole
    {
        public Team Team { get; }
        public int Cooldown { get; }
        public bool ChangeToGA { get; }
        public void OnProtect(PlayerControl pc, PlayerControl target);
        public void OnAssign(PlayerControl pc);
    }
}