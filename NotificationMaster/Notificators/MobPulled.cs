﻿using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationMaster
{
    internal class MobPulled : IDisposable
    {
        NotificationMaster p;
        HashSet<uint> ignoreMobIds = new();
        HashSet<int> watchedMobNamesHashes = new();
        public void Dispose()
        {

        }

        public MobPulled(NotificationMaster plugin)
        {
            this.p = plugin;
            TerritoryChanged(null, Svc.ClientState.TerritoryType);
            Svc.ClientState.TerritoryChanged += TerritoryChanged;
        }

        void RebuildMobNames()
        {
            foreach (var s in p.cfg.mobPulled_Names)
            {
                watchedMobNamesHashes.Add(s.GetHashCode());
            }
            PluginLog.Debug($"Mob names hash table rebuilt, config entries={string.Join(",", p.cfg.mobPulled_Names)}; hashes={string.Join(",", watchedMobNamesHashes)}");
        }

        void ClearIgnoredMobs()
        {
            ignoreMobIds.Clear();
            PluginLog.Debug("Cleared ignored mobs ids cache");
        }

        void TerritoryChanged(object _, ushort newTerritory)
        {
            Svc.Framework.Update -= MobPulledWatcher;
            PluginLog.Debug("MobPulledWatcher unregistered.");
            if (p.cfg.mobPulled_Territories.Contains(newTerritory))
            {
                Svc.Framework.Update += MobPulledWatcher;
                PluginLog.Debug($"MobPulledWatcher registered, territory type={newTerritory}");
            }
        }

        void MobPulledWatcher(Framework framework)
        {
            if (Svc.ClientState.LocalPlayer != null)
            {
                foreach (var o in Svc.Objects)
                {
                    if (o is BattleNpc bnpc && !ignoreMobIds.Contains(o.ObjectId))
                    {
                        var bnpcNameHash = bnpc.Name.GetHashCode();
                        if (!watchedMobNamesHashes.Contains(bnpcNameHash))
                        {
                            ignoreMobIds.Add(o.ObjectId);
                        }
                        else
                        {
                            if (bnpc.MaxHp != bnpc.CurrentHp)
                            {
                                PluginLog.Debug($"Detected pulled mob: {bnpc.Name} with id={o.ObjectId} and hash={bnpcNameHash}");
                                ignoreMobIds.Add(o.ObjectId);
                                if (p.cfg.mobPulled_AlwaysExecute || !p.ThreadUpdActivated.IsApplicationActivated)
                                {
                                    PluginLog.Debug($"Notifying; app activated = {p.ThreadUpdActivated.IsApplicationActivated}");
                                    if (p.cfg.mobPulled_FlashTrayIcon && !p.ThreadUpdActivated.IsApplicationActivated)
                                    {
                                        Native.Impl.FlashWindow();
                                    }
                                    if (p.cfg.mobPulled_AutoActivateWindow && !p.ThreadUpdActivated.IsApplicationActivated) Native.Impl.Activate();
                                    if (p.cfg.mobPulled_ShowToastNotification)
                                    {
                                        TrayIconManager.ShowToast($"{bnpc.Name} has been pulled!", "");
                                    }
                                    if (p.cfg.mobPulled_HttpRequestsEnable)
                                    {
                                        p.httpMaster.DoRequests(p.cfg.mobPulled_HttpRequests,
                                            new string[][]
                                            {
                                            }
                                        );
                                    }
                                    if (p.cfg.mobPulled_SoundSettings.PlaySound)
                                    {
                                        p.audioPlayer.Play(p.cfg.mobPulled_SoundSettings);
                                    }
                                    if (p.cfg.mobPulled_ChatMessage)
                                    {
                                        Svc.Chat.Print(
                                            new SeStringBuilder()
                                            .AddUiForeground(16)
                                            .AddText($"{bnpc.Name} has been pulled!")
                                            .AddUiForegroundOff()
                                            .Build());
                                    }
                                    if (p.cfg.mobPulled_Toast)
                                    {
                                        Svc.Toasts.ShowQuest(
                                            new SeStringBuilder()
                                            .AddUiForeground(16)
                                            .AddText($"{bnpc.Name} has been pulled!")
                                            .AddUiForegroundOff()
                                            .Build()
                                            , new QuestToastOptions()
                                            {
                                                DisplayCheckmark = true,
                                                PlaySound = true
                                            });
                                    }
                                }
                                continue;
                            }
                        }
                    }
                }
            }
        }

        internal static void Setup(bool enable, NotificationMaster p)
        {
            if (enable)
            {
                if (p.mobPulled == null)
                {
                    p.mobPulled = new MobPulled(p);
                    PluginLog.Information("Enabling mobPulled module");
                }
                else
                {
                    PluginLog.Information("mobPulled module already enabled");
                }
            }
            else
            {
                if (p.mobPulled != null)
                {
                    p.mobPulled.Dispose();
                    p.mobPulled = null;
                    PluginLog.Information("Disabling mobPulled module");
                }
                else
                {
                    PluginLog.Information("mobPulled module already disabled");
                }
            }
        }
    }
}
