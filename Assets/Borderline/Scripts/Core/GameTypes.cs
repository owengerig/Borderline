using System;
using UnityEngine;

namespace Borderline
{
    /// <summary>OwnerID values: 0 = Unoccupied, 1 = Player, 2 = Rival AI.</summary>
    public static class Owner
    {
        public const int None = 0;
        public const int Player = 1;
        public const int Rival = 2;
    }

    /// <summary>
    /// Garrison unit types.
    /// Troops   - cheap, bulk defence. First to die when a hex is hit.
    /// Vehicle  - armoured defence, and supports attacks launched next to the hex it sits on.
    /// Turret   - automated defence: adds defence and auto-fires at adjacent enemy hexes.
    ///            Turrets are also built for free over time where you "dwell" (see DwellSettings).
    /// </summary>
    public enum UnitType { Troops, Vehicle, Turret }

    public static class UnitStats
    {
        public const int TroopPower = 1;
        public const int VehiclePower = 4;
        public const int TurretPower = 6;

        public static int Power(UnitType type) => type switch
        {
            UnitType.Vehicle => VehiclePower,
            UnitType.Turret => TurretPower,
            _ => TroopPower,
        };

        /// <summary>Energy cost of one purchase.</summary>
        public static int Cost(UnitType type) => type switch
        {
            UnitType.Vehicle => 8,
            UnitType.Turret => 12,
            _ => 2,
        };

        /// <summary>Units granted per purchase (Troops: 2 energy = +3 defence, per the spec).</summary>
        public static int AmountPerPurchase(UnitType type) => type == UnitType.Troops ? 3 : 1;

        public static int MaxPerTile(UnitType type) => type switch
        {
            UnitType.Vehicle => 6,
            UnitType.Turret => 4,
            _ => 999,
        };

        public static string DisplayName(UnitType type) => type switch
        {
            UnitType.Vehicle => "Vehicle",
            UnitType.Turret => "Turret",
            _ => "Troops",
        };
    }

    /// <summary>Regenerating Action Energy / troop pool. One per side.</summary>
    [Serializable]
    public class EnergyPool
    {
        [Min(1)] public float max = 50f;
        [Min(0)] public float regenPerSecond = 1f;
        [Min(0)] public float startAmount = 20f;

        public float Current { get; private set; }
        public int Whole => Mathf.FloorToInt(Current);
        public float Fraction => max > 0f ? Current / max : 0f;

        public void Reset() => Current = Mathf.Min(startAmount, max);

        public void Tick(float deltaTime) => Current = Mathf.Min(max, Current + regenPerSecond * deltaTime);

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || Current < amount) return false;
            Current -= amount;
            return true;
        }
    }

    /// <summary>
    /// "The longer you stay, the stronger it gets."
    /// While a side's presence (later: the player's GPS position) sits on a hex it owns,
    /// that hex accumulates DwellSeconds. Walls (Fortification) grow continuously, and the
    /// growth RATE ramps up with total time spent there, so home/work hexes become fortresses.
    /// Every secondsPerAutoTurret of dwell, the crew also builds a free turret.
    ///
    /// Prototype values are in seconds so you can watch it happen. For the real GPS game,
    /// multiply the time values by ~60-3600 (e.g. rampSeconds = 3600 = +1x rate per hour).
    /// </summary>
    [Serializable]
    public class DwellSettings
    {
        [Tooltip("Wall points per second at the start of a stay.")]
        [Min(0)] public float baseFortificationPerSecond = 0.25f;

        [Tooltip("Every this many seconds of TOTAL dwell on a hex adds +1x to the growth rate.")]
        [Min(1)] public float rampSeconds = 60f;

        [Tooltip("Upper limit on the growth-rate multiplier.")]
        [Min(1)] public float maxRateMultiplier = 6f;

        [Tooltip("Dwell seconds per free auto-built turret (0 = off).")]
        [Min(0)] public float secondsPerAutoTurret = 45f;

        [Tooltip("Hard cap on wall points per hex.")]
        [Min(0)] public int maxFortification = 500;

        public float RateMultiplier(float dwellSeconds) => Mathf.Min(1f + dwellSeconds / rampSeconds, maxRateMultiplier);
        public float FortificationPerSecond(float dwellSeconds) => baseFortificationPerSecond * RateMultiplier(dwellSeconds);
    }

    public enum ActionResult { Ok, NotAdjacent, NotEnoughEnergy, InvalidTarget, AtCapacity }

    public struct AttackReport
    {
        public bool Captured;
        public int EnergySpent;
        public int SupportVehicles;
        public int AttackPower;
        public float EffectiveDefense;
        public int Damage;
        public int Survivors;
    }
}
