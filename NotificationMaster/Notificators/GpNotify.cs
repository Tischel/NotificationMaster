﻿using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NotificationMaster
{
    unsafe class GpNotify
    {
        internal int nextTick = 0;
        internal bool needNotification = false;
        private NotificationMaster p;
        public const byte PotionCDGroup = 69;

        public void Dispose()
        {
            Svc.Framework.Update -= Tick;
            Svc.Commands.RemoveHandler("/gp");
        }

        public GpNotify(NotificationMaster plugin)
        {
            this.p = plugin;
            Svc.Framework.Update += Tick;
            Svc.Commands.AddHandler("/gp", new CommandInfo(OnCommand) 
            { 
                HelpMessage = "open config\n/gp <number> → set trigger GP amount"    
            });
        }

        private void OnCommand(string command, string arguments)
        {
            if(arguments == "")
            {
                p.configGui.open = true;
            }
            else
            {
                try
                {
                    var newgp = int.Parse(arguments.Trim());
                    if(newgp < 0)
                    {
                        Svc.Toasts.ShowError("GP can't be negative");
                    }
                    else
                    {
                        p.cfg.gp_GPTreshold = newgp;
                        p.cfg.Save();
                        Svc.Toasts.ShowQuest("Trigger GP amount set to " + p.cfg.gp_GPTreshold,
                            new QuestToastOptions() { DisplayCheckmark = true, PlaySound = true });
                    }
                }
                catch(Exception e)
                {
                    Svc.Toasts.ShowError("Error: " + e.Message);
                }
            }
        }

        void Tick(Framework framework)
        {
            if (Environment.TickCount < nextTick) return;
            nextTick = Environment.TickCount + 5000;
            if (Svc.ClientState?.LocalPlayer == null) return;
            if (Svc.ClientState.LocalPlayer.ClassJob.Id != 16
                && Svc.ClientState.LocalPlayer.ClassJob.Id != 17
                && Svc.ClientState.LocalPlayer.ClassJob.Id != 18)
            {
                needNotification = false;
                return;
            }
            var gp = Svc.ClientState.LocalPlayer.CurrentGp;
            //pi.Framework.Gui.Chat.Print(actMgr.GetCooldown(ActionManager.PotionCDGroup).IsCooldown + "/" + actMgr.GetCooldown(ActionManager.PotionCDGroup).CooldownElapsed + "/" + actMgr.GetCooldown(ActionManager.PotionCDGroup).CooldownTotal);
            if (!p.actMgr.GetCooldown(PotionCDGroup).IsCooldown) gp += (uint)p.cfg.gp_PotionCapacity;
            //pi.Framework.Gui.Chat.Print(DateTimeOffset.Now + ": " + gp);
            if(gp >= p.cfg.gp_GPTreshold)
            {
                if (needNotification) 
                {
                    needNotification = false;
                    if (!Native.ApplicationIsActivated())
                    {
                        if (p.cfg.gp_FlashTrayIcon)
                        {
                            Native.ActivateWindow();
                        }
                        if (p.cfg.gp_AutoActivateWindow) Native.SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);
                        if (p.cfg.gp_ShowToastNotification)
                        {
                            Native.ShowToast(gp + " GP ready!");
                        }
                    }
                }
            }
            else
            {
                if (gp + p.cfg.gp_Tolerance < p.cfg.gp_GPTreshold)
                {
                    needNotification = true;
                }
            }
        }
    }
}
