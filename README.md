# Omnibus
Simple all-in-one plugin for PVE, basic teleport, and decay management for Rust

This plugin has a simplified config and implementation compared to other PVE, teleport, and decay plugins.

It will facilitate PVE (player to player and player to building), teleport to town/bandit/outpost, as well as basic decay control.

No home/sethome functionality exists.  Teleports are available only for bandit, outpost, and town (if set).

### Permissions
  - `omnibus.admin` -- Required only for /town set
  - `omnibus.tp`    -- Required if RequirePermissionForTeleport is true and player needs teleport access
  - `omnibus.decay` -- Required if RequirePermissionForDecay is true and player needs decay protection
  - `omnibus.pve`   -- Required if RequirePermissionForPVE is true and player needs PVE access

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
    "Debug": false,
    "RequirePermissionForPVE": false,
    "RequirePermissionForDecay": false,
    "RequirePermissionForTeleport": false,
    "useClans": false,
    "useFriends": false,
    "useTeams": false
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 7
  }
}
```

  - `DecayMultiplier` -- Sets the global decay percentage.  1 is standard decay, 0.5 is 50%, etc.
  - `EnablePVE` -- Prevent damage from player to player and player to other players' buildings
  - `Debug` --  Log debug activity to oxide log and rcon.
  - `RequirePermissionForPVE` -- If true, players require omnibus.pve to prevent PVP damage
  - `RequirePermissionForDecay` -- If true, players require omnibus.decay to prevent decay damage
  - `RequirePermissionForTeleport` - If true, players require omnibus.tp to use /town, /outpost, and /bandit
  - `useClans` -- With the Clans plugin, membership will be checked for PVE damage
  - `useFriends` -- With the Friends plugin, membership will be checked for PVE damage
  - `useTeams` -- Using Rust Teams, membership will be checked for PVE damage


### Notes
  1. If JPipes is available, we will skip decay management for JPipes.
  2. If any of NTeleportation, RTeleportation, Teleportication, TruePVE, NextGenPVE, or NoDecay are loaded when loading this plugin, action of this plugin will be disabled for those functions.
    - e.g., if NoDecay is also loaded, only decay functions will be disabled within this plugin.
  3. If both DecayMultiplier == 1 and EnablePVE == false, this plugin will be a complete waste of resources...


### EXAMPLES
  
  - `Setting PVE for specific players`
    - Set RequirePermissionForPVE to true
    - Add permission omnibus.pve to players/groups needing to play as PVE - all others will be PVP.
    - Player's teammates/clan members/friends will be able to damage each other including buildings, etc.

  - `Disabling Teleport`
     - Set RequirePermissionForTeleport to true
     - DO NOT assign permission omnibus.tp to anyone
     - Teleport is effectively disabled

  - `Disabling Decay Prevention`
     - Set RequirePermissionForDecay to true
     - DO NOT assign permission omnibus.decay to anyone
     - Decay is effectively standard for all

