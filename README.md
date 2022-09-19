# Omnibus
Simple all-in-one plugin for PVE, basic teleport,  and decay management for Rust

This plugin has a simplified config and implementation compared to other PVE, teleport, and decay plugins.

It will facilitate PVE (player to player and player to building), teleport to town/bandit/outpost, as well as basic decay control.

No home/sethome functionality exists.  Teleports are available only for bandit, outpost, and town (if set).

### Permissions
  - `omnibus.admin` -- Required only for /town set

### Commands
  - `/town` -- Go to town set by admin
  - `/town set` -- For admin to set town location
  - `/bandit` -- Teleport to Bandit Town
  - `/outpost` -- Teleport to the Outpost

### Config

```json
{
  "Global": {
    "DecayMultiplier": 0.5,
    "EnablePVE": true,
    "Debug": false
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 5
  }
}
```

  - `DecayMultiplier` -- Sets the global decay percentage.  1 is standard decay, 0.5 is 50%, etc.
  - `EnablePVE` -- Prevent damage from player to player and player to other players' buildings
  - `Debug` --  Log debug activity to oxide log and rcon.


### Notes
  1. If JPipes is available, we will skip decay management for JPipes.
  2. If any of NTeleportation, RTeleportation, Teleportication, TruePVE, NextGenPVE, or NoDecay are loaded when loading this plugin, action of this plugin will be disabled for those functions.
    - e.g., if NoDecay is also loaded, only decay functions will be disabled within this plugin.
  3. If both DecayMultiplier == 1 and EnablePVE == false, this plugin will be a complete waste of resources...

