﻿using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.RegularExpressions;
using CSPracc.DataModules;
using System.IO;
using CSPracc.Managers;
using CSPracc.DataModules.Constants;
using CSPracc.EventHandler;
using CSPracc.CommandHandler;
using CSPracc.Modes;
using static CSPracc.DataModules.Enums;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;

namespace CSPracc
{
    public  class MatchMode : BaseMode
    {       
        private static DataModules.Enums.match_state state = DataModules.Enums.match_state.warmup;

        private static bool ReadyTeamCT = false;
        private static bool ReadyTeamT = false;

        public static List<ulong>? ListCoaches { get; set; }

        private static BaseEventHandler EventHandler {  get; set; } = null;


        public MatchMode() : base()
        {

        }

        public override void Dispose()
        {
            EventHandler?.Dispose();
        }

        public static void Pause()
        {
            if (state == DataModules.Enums.match_state.warmup) { return; }
            Methods.MsgToServer("Match paused. Waiting for both teams to .unpause");
            Server.ExecuteCommand(DataModules.Constants.COMMANDS.PAUSE_MATCH);
        }

        public static void Ready(CCSPlayerController player)
        {

            if (state != match_state.warmup){ return; }
            if (player == null ) { return; }
            if(!player.IsValid) { return; }
            switch (player.TeamNum)
            {
                case (byte)CsTeam.Terrorist:
                    {
                        if(ReadyTeamT)
                        {
                            break;
                        }
                        ReadyTeamT = true;
                        Methods.MsgToServer("T side is ready!");
                        break;
                    }
                case (byte)CsTeam.CounterTerrorist:
                    {
                        if (ReadyTeamCT)
                        {
                            break;
                        }
                        ReadyTeamCT = true;
                        Methods.MsgToServer("CT side is ready!");
                        break;
                    }
            }
            if(ReadyTeamT && ReadyTeamCT)
            {
                internalStart();
            }
        }

        public static void UnReady(CCSPlayerController player)
        {
            if (player == null) { return; }
            if (!player.IsValid) { return; }

            switch (player.TeamNum)
            {
                case (byte)CsTeam.Terrorist:
                    {
                        if (ReadyTeamT)
                        {
                            break;
                        }
                        ReadyTeamT = false;
                        Methods.MsgToServer("T side is not ready!");
                        break;
                    }
                case (byte)CsTeam.CounterTerrorist:
                    {
                        if (ReadyTeamCT)
                        {
                            break;
                        }
                        ReadyTeamCT = false;
                        Methods.MsgToServer("CT side is not ready!");
                        break;
                    }
            }
        }

        public static void Unpause(CCSPlayerController player)
        {
            if (state == DataModules.Enums.match_state.warmup) { return; }
            if(player.TeamNum == (float)CsTeam.CounterTerrorist)
            {
                ReadyTeamCT = true;
                DataModules.Constants.Methods.MsgToServer("CT Side is now ready!");
            }
            if (player.TeamNum == (float)CsTeam.Terrorist)
            {
                ReadyTeamT = true;
                DataModules.Constants.Methods.MsgToServer("T Side is now ready!");
            }
            if(ReadyTeamCT && ReadyTeamT) 
            {
                Methods.MsgToServer("Both Teams are now ready. Unpausing match!");
                Server.ExecuteCommand(DataModules.Constants.COMMANDS.UNPAUSE_MATCH);
            }
            
        }

        public static void Restart(CCSPlayerController player)
        {
            if (player == null) return;
            if (!player.PlayerPawn.IsValid) return;
            if (!player.IsAdmin())
            {
                player.PrintToCenter("Only admins can execute this command!");
                return;
            }
            if (state == DataModules.Enums.match_state.warmup ) { return; }
            Methods.MsgToServer("Restarting game.");
            Server.ExecuteCommand(DataModules.Constants.COMMANDS.RESTART_GAME);
        }

        public static void Rewarmup(CCSPlayerController? player)
        {
            if (player == null) return;
            if (!player.PlayerPawn.IsValid) return;
            if (!player.IsAdmin())
            {
                player.PrintToCenter("Only admins can execute this command!");
                return;
            }
            state = match_state.warmup;
            Methods.MsgToServer("Starting Warmup.");
            Server.ExecuteCommand("exec CSPRACC\\5on5_warmup.cfg");
            Server.ExecuteCommand(DataModules.Constants.COMMANDS.START_WARMUP);
        }

        public static void Start(CCSPlayerController? player)
        {
            if(player == null) { return; }
            if(!player.IsValid) { return; }
            if(!player.IsAdmin()) { player.PrintToCenter("Only admins can execute this command!"); return; }
            internalStart();
        }

        private static void internalStart()
        {
            ReadyTeamCT = false;
            ReadyTeamT = false;
            if (state == DataModules.Enums.match_state.live) { return; }
            state = DataModules.Enums.match_state.live;
            RoundRestoreManager.CleanupOldFiles();
            Server.ExecuteCommand("exec CSPRACC\\5on5.cfg");
            Methods.MsgToServer("Starting Match!");
            Server.ExecuteCommand("bot_kick");
            Server.ExecuteCommand("mp_warmup_end 1");
            if (DemoManager.DemoManagerSettings.RecordingMode == Enums.RecordingMode.Automatic)
            {
                DemoManager.StartRecording();
            }
        }

        public static void StopCoach(CCSPlayerController playerController)
        {
            if (playerController == null) return;

            if(ListCoaches == null ||  ListCoaches.Count == 0) return;

            Server.PrintToChatAll($"Looking for coach now {playerController.SteamID} , count of coaches {ListCoaches.Count}");

            if(ListCoaches.Remove(playerController.SteamID))
            {
                Server.PrintToChatAll($"Found coach, removing now");
                playerController.PrintToCenter("You`re no longer a coach.");
            }
            
        }

        public static void AddCoach(CCSPlayerController playerController)
        {
            if (playerController == null) return;
            if (!playerController.PlayerPawn.IsValid) return;

            if (ListCoaches == null)
            {
                ListCoaches = new List<ulong>();
            }
            if(ListCoaches.Contains(playerController.SteamID))
            {
                playerController.PrintToCenter("You already are a coach.");
                return;
            }
            ListCoaches.Add(playerController.SteamID);
            playerController.Clan = "COACH";
            playerController.PrintToCenter("You`re a coach now.");
        }

        public static void RestoreBackup(CCSPlayerController player)
        {
            if(player == null) { return; }
            if(!player.IsValid) { return; }
            if(!player.IsAdmin()) { player.PrintToCenter("Only admins can execute this command!"); return; }
            Pause();
            Methods.MsgToServer("Admin is using round restore manager.");
            RoundRestoreManager.OpenBackupMenu(player);
        }

        public static void ForceUnpause(CCSPlayerController player)
        {
            if (player == null) { return; }
            if (!player.IsValid) { return; }
            if (!player.IsAdmin()) { player.PrintToCenter("Only admins can execute this command!"); return; }
            ReadyTeamCT = true;
            ReadyTeamT = true;
        }

        public static HookResult OnFreezeTimeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            if (state == match_state.warmup) { return HookResult.Continue; }
            if (MatchMode.ListCoaches != null && MatchMode.ListCoaches.Count > 0)
            {
                CSPraccPlugin.Instance!.AddTimer(2.0f, () => SwitchTeamsCoach(ListCoaches));
            }
            return HookResult.Changed;
        }

        private static void SwitchTeamsCoach(List<ulong> playerList)
        {
            if (playerList == null || playerList.Count == 0) return;

            
            foreach (ulong id in playerList)
            {
                CCSPlayerController player = Utilities.GetPlayerFromSteamId(id);
                if (player == null || !player.IsValid)
                {
                    return;
                }
                CsTeam oldTeam = (CsTeam)player.TeamNum;
                player.ChangeTeam(CsTeam.Spectator);
                player.ChangeTeam(oldTeam);
            }
        }

        public static HookResult OnPlayerSpawnHandler(EventPlayerSpawn @event,GameEventInfo info)
        {
            if(state == match_state.warmup) { return HookResult.Continue; }

            if(@event.Userid == null ) return HookResult.Continue;

            if (ListCoaches == null || ListCoaches.Count == 0) return HookResult.Continue;

            Server.PrintToChatAll("Going through the coaches");
            foreach (ulong id in ListCoaches)
            {
                if (id ==  @event.Userid!.SteamID)
                {
                    Server.PrintToChatAll($"found coach {@event.Userid.PlayerName}!");
                    @event.Userid.InGameMoneyServices!.Account = 0;
                    Server.ExecuteCommand("mp_suicide_penalty 0");
                    CCSPlayerController player = Utilities.GetPlayerFromSteamId(id);
                    if(player == null || !player.IsValid) { return HookResult.Continue; }
                    CSPraccPlugin.Instance!.AddTimer(0.5f, () => player!.PlayerPawn!.Value!.CommitSuicide(false, true));
                    Server.ExecuteCommand("mp_suicide_penalty 1");
                }
            }

            return HookResult.Continue;
        }

        public override void ConfigureEnvironment()
        {
            DataModules.Constants.Methods.MsgToServer("Loading match mode.");
            Server.ExecuteCommand("exec CSPRACC\\undo_pracc.cfg");
            Server.ExecuteCommand("exec CSPRACC\\5on5_warmup.cfg");
            EventHandler?.Dispose();
            EventHandler = new MatchEventHandler(CSPraccPlugin.Instance!, new MatchCommandHandler());
            state = Enums.match_state.warmup;
        }    
    }
}
