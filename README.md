# Omnibus
Simple all-in-one plugin for PVE and Decay management for Rust

This plugin has a simplified config and implementation compared to other PVE and decay plugins.

It will facilitate PVE (player to player and player to building) as well as basic decay control.

No permissions are required and there are no commands.

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
    "Patch": 1
  }
}
```

  - `DecayMultiplier` -- Sets the global decay percentage.  1 is standard decay, 0.5 is 50%, etc.
  - `EnablePVE` -- Prevent damage from player to player and player to other players' buildings
  - `Debug` --  Log debug activity to oxide log and rcon.


### Notes
  1. If JPipes is available, we will skip decay management for JPipes.
  2. If any of TruePVE, NextGenPVE, or NoDecay are loaded when loading this plugin, action of this plugin will be disabled.
  3. If both DecayMultiplier == 1 and EnablePVE == false, this plugin will be a complete waste of resources...

