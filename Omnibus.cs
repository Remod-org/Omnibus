using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Omnibus", "RFC1920", "1.0.1")]
    [Description("Simple all-in-one plugin for PVE and decay management")]
    class Omnibus : RustPlugin
    {
        #region vars
        private ConfigData configData;
        private bool enabled = true;

        [PluginReference]
        private readonly Plugin JPipes, NoDecay, NextGenPVE, TruePVE;
        #endregion

        #region init
        void Loaded() => LoadConfigValues();

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
        }
        #endregion

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
                        if (hitinfo.Initiator == null)
                        {
                            AttackEntity turret;
                            if (IsAutoTurret(hitinfo, out turret))
                            {
                                hitinfo.Initiator = turret as BaseEntity;
                            }
                            return null;
                        }

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
        private bool IsAutoTurret(HitInfo hitinfo, out AttackEntity weapon)
        {
            // Check for turret initiator
            var turret = hitinfo.Weapon?.GetComponentInParent<AutoTurret>();
            if (turret != null)
            {
                DoLog($"Turret weapon '{hitinfo.Weapon?.ShortPrefabName}' is initiator");
                weapon = hitinfo.Weapon;
                return true;
            }

            weapon = null;
            return false;
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
