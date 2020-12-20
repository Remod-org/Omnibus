using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Omnibus", "RFC1920", "1.0.3")]
    [Description("Simple all-in-one plugin for PVE, town teleport, and decay management")]
    class Omnibus : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private bool enabled = true;

        [PluginReference]
        private readonly Plugin JPipes, NoDecay, NextGenPVE, TruePVE, NTeleportation, RTeleportation, Teleportication;

        private readonly Dictionary<ulong, TPTimer> TeleportTimers = new Dictionary<ulong, TPTimer>();
        Dictionary<string, Vector3> teleport = new Dictionary<string, Vector3>();
        private const string permAdmin = "omnibus.admin";
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        #region init
        void Loaded() => LoadConfigValues();
        void Unload() => SaveData();

        void Init()
        {
            if(NoDecay != null)
            {
                Puts("NoDecay will conflict.  Disabling Omnibus.");
                enabled = false;
            }
            if(TruePVE != null)
            {
                Puts("TruePVE will conflict.  Disabling Omnibus.");
                enabled = false;
            }
            if(NextGenPVE != null)
            {
                Puts("NextGenPVE will conflict.  Disabling Omnibus.");
                enabled = false;
            }
            if(NTeleportation != null)
            {
                Puts("NTeleportation will conflict.  Disabling Omnibus.");
                enabled = false;
            }
            if(RTeleportation != null)
            {
                Puts("RTeleportation will conflict.  Disabling Omnibus.");
                enabled = false;
            }
            if(Teleportication  != null)
            {
                Puts("Teleportication will conflict.  Disabling Omnibus.");
                enabled = false;
            }

            permission.RegisterPermission(permAdmin, this);
            AddCovalenceCommand("town", "CmdTownTeleport");
            AddCovalenceCommand("bandit", "CmdTownTeleport");
            AddCovalenceCommand("outpost", "CmdTownTeleport");
            LoadData();
            FindMonuments();
        }

        private void LoadData() => teleport = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Vector3>>($"{Name}/teleport");
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/teleport", teleport);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["town"] = "Town",
                ["outpost"] = "Outpost",
                ["bandit"] = "Bandit",
                ["townset"] = "Town location has been set to {0}!",
                ["teleporting"] = "Teleporting to {0} in {1} seconds..."
            }, this);
        }
        #endregion

        public class TPTimer
        {
            public Timer timer;
            public float start;
            public float countdown;
            public string type;
            public BasePlayer source;
            public string targetName;
            public Vector3 targetLocation;
        }

        [Command("town")]
        private void CmdTownTeleport(IPlayer iplayer, string command, string[] args)
        {
            if (!enabled) return;
            if (iplayer.Id == "server_console") return;
            var player = iplayer.Object as BasePlayer;
            if (args.Length > 0)
            {
                if (args[0] == "set")
                {
                    if (!iplayer.HasPermission(permAdmin)) { Message(iplayer, "notauthorized"); return; }
                    if (teleport.ContainsKey("town")) teleport["town"] = player.transform.position;
                    else teleport.Add("town", player.transform.position);
                    SaveData();
                    switch (command)
                    {
                        case "town":
                            Message(iplayer, "townset", player.transform.position.ToString());
                            break;
                    }
                    return;
                }
            }
            if (teleport.ContainsKey(command))
            {
                if (!TeleportTimers.ContainsKey(player.userID))
                {
                    TeleportTimers.Add(player.userID, new TPTimer() { type = command, start = Time.realtimeSinceStartup, countdown = 5f, source = player, targetName = Lang(command), targetLocation = teleport[command] });
                    HandleTimer(player.userID, command, true);
                    Message(iplayer, "teleporting", command, "5");
                }
                else if (TeleportTimers[player.userID].countdown == 0)
                {
                    Teleport(player, teleport[command], command);
                }
            }
        }

        #region main
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (!enabled) return null;
            if (entity == null || hitinfo == null) return null;

            float damageAmount = 0f;
            string entity_name = entity.LookupPrefab().name;
            ulong owner = entity.OwnerID;

            string majority = hitinfo.damageTypes.GetMajorityDamageType().ToString();

            switch(majority)
            {
                case "Decay":
                    float before = hitinfo.damageTypes.Get(Rust.DamageType.Decay);
                    damageAmount = before * configData.Global.DecayMultiplier;

                    if (entity is BuildingBlock)
                    {
                        if ((bool)JPipes?.Call("IsPipe", entity))
                        {
                            if ((bool)JPipes?.Call("IsNoDecayEnabled"))
                            {
                                DoLog("Found a JPipe with nodecay enabled");
                                hitinfo.damageTypes.Scale(Rust.DamageType.Decay, 0f);
                                return null;
                            }
                        }

                        damageAmount = before * configData.Global.DecayMultiplier;
                    }

                    NextTick(() =>
                    {
                        DoLog($"Decay ({entity_name}) before: {before} after: {damageAmount}, item health {entity.health.ToString()}");
                        entity.health -= damageAmount;
                        if (entity.health == 0)
                        {
                            DoLog($"Entity {entity_name} completely decayed - destroying!");
                            if (entity == null) return;
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                    });
                    return true; // Cancels this hook (for decay only).  Decay handled on NextTick.
                default:
                    if(configData.Global.EnablePVE)
                    {
                        try
                        {
                            object CanTakeDamage = Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hitinfo });
                            if (CanTakeDamage != null && CanTakeDamage is bool && (bool)CanTakeDamage) return null;
                        }
                        catch { }

                        var source = (hitinfo.Initiator as BaseEntity).GetType().Name;
                        var target = (entity as BaseEntity).GetType().Name;

                        if (source == "BasePlayer" && target == "BasePlayer") return true; // Block player to player damage

                        if (source == "BasePlayer" && (target == "BuildingBlock" || target == "Door" || target == "wall.window"))
                        {
                            if(owner == (hitinfo.Initiator as BasePlayer).userID)
                            {
                                return null;
                            }
                            // Block damage to non-owned building
                            return true;
                        }
                    }
                    break;
            }
            return null;
        }
        #endregion

        #region helpers
        public void Teleport(BasePlayer player, Vector3 position, string type="")
        {
            HandleTimer(player.userID, type);

            if(player.net?.connection != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

            player.SetParent(null, true, true);
            player.EnsureDismounted();
            player.Teleport(position);
            player.UpdateNetworkGroup();
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);

            if(player.net?.connection != null) player.ClientRPCPlayer(null, player, "StartLoading");
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping()) return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player)) BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
            //player.inventory.crafting.CancelAll(true);
            //player.UpdatePlayerCollider(true, false);
        }

        public void HandleTimer(ulong userid, string type, bool start = false)
        {
            if (TeleportTimers.ContainsKey(userid))
            {
                if (start)
                {
                    TeleportTimers[userid].timer = timer.Once(TeleportTimers[userid].countdown, () => { Teleport(TeleportTimers[userid].source, TeleportTimers[userid].targetLocation, type); });
                }
                else
                {
                    if (TeleportTimers.ContainsKey(userid))
                    {
                        TeleportTimers[userid].timer.Destroy();
                        TeleportTimers.Remove(userid);
                    }
                }
            }
        }

        void FindMonuments()
        {
            string name = null;
            int i = 0;
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                name = Regex.Match(monument.name, @"\w{6}\/(.+\/)(.+)\.(.+)").Groups[2].Value.Replace("_", " ").Replace(" 1", "").Titleize();

                if(monument.name.Contains("compound"))
                {
                    i++;
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 50, ents);
                    foreach(BaseEntity entity in ents)
                    {
                        if(entity.PrefabName.Contains("piano"))
                        {
                            Vector3 outpost = entity.transform.position + new Vector3(1f, 0.1f, 1f);
                            if (teleport.ContainsKey("outpost")) teleport["outpost"] = outpost;
                            else teleport.Add("outpost", outpost);
                        }
                    }
                }
                else if(monument.name.Contains("bandit"))
                {
                    i++;
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 50, ents);
                    foreach(BaseEntity entity in ents)
                    {
                        if(entity.PrefabName.Contains("workbench"))
                        {
                            Vector3 bandit = Vector3.Lerp(monument.transform.position, entity.transform.position, 0.45f) + new Vector3(0, 1.5f, 0);
                            if (teleport.ContainsKey("bandit")) teleport["bandit"] = bandit;
                            else teleport.Add("bandit", bandit);
                        }
                    }
                }
                if (i > 1) break;
            }
            SaveData();
        }

        private void DoLog(string message)
        {
            if(configData.Global.Debug) Puts($"{message}");
        }
        #endregion

        #region config
        private class ConfigData
        {
            public Global Global = new Global();
            public VersionNumber Version;
        }

        private class Global
        {
            public float DecayMultiplier = 0.5f;
            public bool EnablePVE = true;
            public bool Debug = false;
        }

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }
}
