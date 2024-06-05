using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Omnibus", "RFC1920", "1.0.9")]
    [Description("Simple all-in-one plugin for PVE, town teleport, and decay management")]
    internal class Omnibus : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private bool pveEnabled = true;
        private bool decayEnabled = true;
        private bool teleportEnabled = true;

        [PluginReference]
        private readonly Plugin Friends, Clans, JPipes;

        private readonly Dictionary<ulong, TPTimer> TeleportTimers = new Dictionary<ulong, TPTimer>();
        private Dictionary<string, Vector3> teleport = new Dictionary<string, Vector3>();
        private const string permAdmin = "omnibus.admin";
        private const string permPVE = "omnibus.pve";
        private const string permDecay = "omnibus.decay";
        private const string permTeleport = "omnibus.tp";
        #endregion

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        #region init
        private void Loaded() => LoadConfigValues();
        private void Unload() => SaveData();

        private void Init()
        {
            if (plugins.Exists("NoDecay"))
            {
                Puts("NoDecay will conflict.  Disabling Omnibus decay.");
                decayEnabled = false;
            }
            if (plugins.Exists("TruePVE"))
            {
                Puts("TruePVE will conflict.  Disabling Omnibus PVE.");
                pveEnabled = false;
            }
            if (plugins.Exists("NextGenPVE"))
            {
                Puts("NextGenPVE will conflict.  Disabling Omnibus PVE.");
                pveEnabled = false;
            }
            if (plugins.Exists("NTeleportation"))
            {
                Puts("NTeleportation will conflict.  Disabling Omnibus teleport.");
                teleportEnabled = false;
            }
            if (plugins.Exists("RTeleportation"))
            {
                Puts("RTeleportation will conflict.  Disabling Omnibus teleport.");
                teleportEnabled = false;
            }
            if (plugins.Exists("Teleportication"))
            {
                Puts("Teleportication will conflict.  Disabling Omnibus teleport.");
                teleportEnabled = false;
            }

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permDecay, this);
            permission.RegisterPermission(permPVE, this);
            permission.RegisterPermission(permTeleport, this);
            if (teleportEnabled)
            {
                AddCovalenceCommand("town", "CmdTownTeleport");
                AddCovalenceCommand("bandit", "CmdTownTeleport");
                AddCovalenceCommand("outpost", "CmdTownTeleport");
            }
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
                ["notset"] = "{0} location has not been set.",
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

        private bool PlayerCanTeleport(BasePlayer player)
        {
            if (player.IsNpc) return false;
            return configData.Global.RequirePermissionForTeleport && permission.UserHasPermission(player?.UserIDString, permTeleport);
        }

        private bool PlayerIsProtected(BasePlayer player)
        {
            if (player.IsNpc) return false;
            return configData.Global.RequirePermissionForPVE && permission.UserHasPermission(player?.UserIDString, permPVE);
        }

        [Command("town")]
        private void CmdTownTeleport(IPlayer iplayer, string command, string[] args)
        {
            if (!teleportEnabled) return;
            if (iplayer.Id == "server_console") return;

            BasePlayer player = iplayer.Object as BasePlayer;
            if (!PlayerCanTeleport(player))
            {
                return;
            }
            if (args.Length > 0 && args[0] == "set")
            {
                if (!iplayer.HasPermission(permAdmin)) { Message(iplayer, "notauthorized"); return; }
                teleport["town"] = player.transform.position;

                SaveData();
                switch (command)
                {
                    case "town":
                        Message(iplayer, "townset", player.transform.position.ToString());
                        break;
                }
                return;
            }
            if (teleport.ContainsKey(command))
            {
                if (teleport[command] != default)
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
                else
                {
                    Message(iplayer, "notset", command);
                }
            }
        }

        #region main
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (!(decayEnabled || pveEnabled)) return null;
            if (entity == null || hitinfo == null) return null;

            float damageAmount = 0f;
            string entity_name = entity.LookupPrefab().name;
            ulong owner = entity.OwnerID;

            switch (hitinfo?.damageTypes?.GetMajorityDamageType().ToString())
            {
                case "Decay":
                    if (!decayEnabled) return null;
                    if (configData.Global.RequirePermissionForDecay)
                    {
                        BasePlayer player = BaseNetworkable.serverEntities.Find(new NetworkableId(entity.OwnerID)) as BasePlayer;
                        if (player != null && !permission.UserHasPermission(player.UserIDString, permDecay))
                        {
                            return null;
                        }
                    }
                    float before = hitinfo.damageTypes.Get(Rust.DamageType.Decay);
                    damageAmount = before * configData.Global.DecayMultiplier;

                    if (entity is BuildingBlock)
                    {
                        if ((bool)JPipes?.Call("IsPipe", entity) && (bool)JPipes?.Call("IsNoDecayEnabled"))
                        {
                            DoLog("Found a JPipe with nodecay enabled");
                            hitinfo.damageTypes.Scale(Rust.DamageType.Decay, 0f);
                            return null;
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
                            if (entity == null)
                            {
                                return;
                            }

                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                    });
                    return true; // Cancels this hook (for decay only).  Decay handled on NextTick.
                default:
                    if (!pveEnabled) return null;
                    if (configData.Global.EnablePVE)
                    {
                        BaseEntity src = hitinfo?.Initiator;
                        BaseEntity tgt = entity;
                        string source = src?.GetType()?.Name;
                        string target = entity?.GetType()?.Name;

                        if (src is BasePlayer && PlayerIsProtected(src as BasePlayer))
                        {
                            try
                            {
                                object isfr = IsFriend((src as BasePlayer).userID, tgt.OwnerID);
                                if (!ReferenceEquals(isfr, null) && isfr is bool && (bool)isfr)
                                {
                                    DoLog("Players are friends, allowing damage");
                                    return null;
                                }
                            }
                            catch { }
                            DoLog("Players are NOT friends, blocking damage");
                            return true;
                        }
                        if (tgt is BasePlayer && PlayerIsProtected(tgt as BasePlayer))
                        {
                            try
                            {
                                object isfr = IsFriend((src as BasePlayer).userID, (tgt as BasePlayer).userID);
                                if (!ReferenceEquals(isfr, null) && isfr is bool && (bool)isfr)
                                {
                                    DoLog("Players are friends, allowing damage");
                                    return null;
                                }
                            }
                            catch { }
                            DoLog("Players are NOT friends, blocking damage");
                            return true;
                        }

                        try
                        {
                            object CanTakeDamage = Interface.CallHook("CanEntityTakeDamage", new object[] { entity, hitinfo });
                            if (CanTakeDamage != null && CanTakeDamage is bool && (bool)CanTakeDamage)
                            {
                                return null;
                            }
                        }
                        catch { }

                        if (source == "BasePlayer" && target == "BasePlayer")
                        {
                            return true; // Block player to player damage
                        }

                        if (source == "BasePlayer" && (target == "BuildingBlock" || target == "Door" || target == "wall.window"))
                        {
                            BasePlayer pl = hitinfo.Initiator as BasePlayer;
                            if (pl != null && owner == pl.userID)
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
        private object IsFriend(ulong playerid, ulong ownerid)
        {
            if (playerid == ownerid) return true;
            if (configData.Global.useFriends && Friends != null)
            {
                object fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    DoLog($"Friends plugin reports that {playerid} and {ownerid} are friends.");
                    return true;
                }
            }
            if (configData.Global.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan != null && ownerclan != null && playerclan == ownerclan)
                {
                    DoLog($"Clans plugin reports that {playerid} and {ownerid} are clanmates.");
                    return true;
                }
            }
            if (configData.Global.useTeams)
            {
                RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerid);
                if (playerTeam?.members.Contains(ownerid) == true)
                {
                    DoLog($"Rust teams reports that {playerid} and {ownerid} are on the same team.");
                    return true;
                }
            }
            return false;
        }
        public void Teleport(BasePlayer player, Vector3 position, string type="")
        {
            HandleTimer(player.userID, type);

            if (player.net?.connection != null)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }

            player.SetParent(null, true, true);
            player.EnsureDismounted();
            player.Teleport(position);
            player.UpdateNetworkGroup();
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);

            if (player.net?.connection != null)
            {
                player.ClientRPC(RpcTarget.Player("StartLoading", player));
            }
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
            {
                return;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
            {
                BasePlayer.sleepingPlayerList.Add(player);
            }

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
                    TeleportTimers[userid].timer = timer.Once(TeleportTimers[userid].countdown, () => Teleport(TeleportTimers[userid].source, TeleportTimers[userid].targetLocation, type));
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

        private void FindMonuments()
        {
            if (!teleport.ContainsKey("bandit")) teleport.Add("bandit", default(Vector3));
            if (!teleport.ContainsKey("outpost")) teleport.Add("outpost", default(Vector3));

            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.name.Contains("compound", System.Globalization.CompareOptions.OrdinalIgnoreCase))
                {
                    Vector3 mt = Vector3.zero;
                    Vector3 bbq = Vector3.zero;
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 50, ents);
                    foreach (BaseEntity entity in ents)
                    {
                        if (entity.PrefabName.Contains("marketterminal") && mt == Vector3.zero)
                        {
                            mt = entity.transform.position;
                        }
                        else if (entity.PrefabName.Contains("bbq"))
                        {
                            bbq = entity.transform.position;
                        }
                    }
                    if (mt != Vector3.zero && bbq != Vector3.zero)
                    {
                        teleport["outpost"] = Vector3.Lerp(mt, bbq, 0.3f) + new Vector3(1f, 0.1f, 1f);
                    }
                }
                else if (monument.name.Contains("bandit", System.Globalization.CompareOptions.OrdinalIgnoreCase))
                {
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 50, ents);
                    foreach (BaseEntity entity in ents)
                    {
                        if (entity.PrefabName.Contains("workbench"))
                        {
                            teleport["bandit"] = Vector3.Lerp(monument.transform.position, entity.transform.position, 0.45f) + new Vector3(0, 1.5f, 0);
                        }
                    }
                }
                if (teleport["outpost"] != default(Vector3) && teleport["bandit"] != default(Vector3)) break;
            }
            SaveData();
        }

        private void DoLog(string message)
        {
            if (configData.Global.Debug)
            {
                Puts($"{message}");
            }
        }
        #endregion

        #region config
        private class ConfigData
        {
            public Global Global;
            public VersionNumber Version;
        }

        private class Global
        {
            public float DecayMultiplier;
            public bool EnablePVE;
            public bool RequirePermissionForPVE;
            public bool RequirePermissionForDecay;
            public bool RequirePermissionForTeleport;
            public bool useClans;
            public bool useFriends;
            public bool useTeams;
            public bool Debug;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                Global = new Global()
                {
                    DecayMultiplier = 0.5f,
                    EnablePVE = true,
                    Debug = false,
                    RequirePermissionForTeleport = false,
                    RequirePermissionForDecay = false,
                    RequirePermissionForPVE = false,
                    useClans = false,
                    useFriends = false,
                    useTeams = false
                },
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigValues()
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
