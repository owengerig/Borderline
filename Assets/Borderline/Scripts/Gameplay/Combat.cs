using System.Collections.Generic;
using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Attack resolution shared by the player and the AI.
    ///
    ///   AttackPower       = energy spent + (vehicles on attacker hexes bordering the target * support power)
    ///   EffectiveDefense  = target.DefensePower * defenderBonus (1.2x)
    ///   AttackPower > EffectiveDefense  -> capture; survivors = AttackPower - EffectiveDefense (min 1) become troops
    ///   otherwise                        -> target loses AttackPower / defenderBonus defence (min 1)
    /// </summary>
    public static class Combat
    {
        static readonly List<HexTile> NeighborBuffer = new List<HexTile>(6);

        public static AttackReport Resolve(HexGridManager grid, HexTile target, int attacker, int energySpent,
                                           float defenderBonus, int vehicleSupportPower, int maxSupportVehicles)
        {
            int supportVehicles = 0;
            if (vehicleSupportPower > 0)
            {
                grid.GetNeighbors(target, NeighborBuffer);
                foreach (var n in NeighborBuffer)
                    if (n.OwnerID == attacker) supportVehicles += n.Vehicles;
                supportVehicles = Mathf.Min(supportVehicles, maxSupportVehicles);
            }

            var report = new AttackReport
            {
                EnergySpent = energySpent,
                SupportVehicles = supportVehicles,
                AttackPower = energySpent + supportVehicles * vehicleSupportPower,
                EffectiveDefense = target.DefensePower * defenderBonus,
            };

            if (report.AttackPower > report.EffectiveDefense)
            {
                report.Captured = true;
                report.Damage = target.DefensePower;
                report.Survivors = Mathf.Max(1, Mathf.FloorToInt(report.AttackPower - report.EffectiveDefense));
                target.SetOwner(attacker, report.Survivors);
            }
            else
            {
                int damage = Mathf.Max(1, Mathf.FloorToInt(report.AttackPower / Mathf.Max(0.01f, defenderBonus)));
                report.Damage = target.ApplyDamage(damage);
            }

            return report;
        }
    }
}
