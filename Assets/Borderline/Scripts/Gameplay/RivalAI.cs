using System.Collections.Generic;
using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// Simple rival:
    ///  - every actionInterval seconds, expands into a free hex bordering its territory (prefers compact shapes);
    ///  - sometimes (attackChance), or when boxed in, attacks the weakest player border hex;
    ///  - otherwise reinforces its weakest border with troops / vehicles / turrets;
    ///  - "commutes" between a home hex and a work hex, so both fortify through dwell like a real player would.
    /// It uses exactly the same rules and energy model as the player (GameManager.TryClaim/TryReinforce/TryAttack).
    /// </summary>
    public class RivalAI : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] float actionInterval = 2f;
        [SerializeField, Range(0f, 1f)] float attackChance = 0.25f;
        [SerializeField] EnergyPool energy = new EnergyPool();

        [Header("Commuting (dwell demo)")]
        [SerializeField, Min(5f)] float commuteInterval = 40f;
        [SerializeField, Min(2)] int ownedHexesBeforeCommuting = 10;

        [Header("Debug")]
        [Tooltip("Shows where the AI 'is'. Keep off in the real game: never reveal another player's location.")]
        [SerializeField] bool showPresenceMarker = false;

        GameManager game;
        HexGridManager grid;
        HexTile home, work, presence;
        PresenceMarker marker;
        float actionTimer, commuteTimer;
        bool running;

        readonly List<HexTile> candidates = new List<HexTile>();

        public EnergyPool Energy => energy;
        public HexTile Presence => presence;

        public void Begin(GameManager gameManager, HexTile homeTile)
        {
            game = gameManager;
            grid = gameManager.Grid;
            home = presence = homeTile;
            energy.Reset();
            running = true;

            if (showPresenceMarker)
            {
                marker = PresenceMarker.Create("Rival Presence", grid.CellRadius, grid.TileMaterial, new Color(1f, 0.5f, 0.9f), 4);
                marker.Follow(presence);
            }
        }

        void Update()
        {
            if (!running || game == null || game.IsGameOver) return;
            float dt = Time.deltaTime;

            energy.Tick(dt);
            if (presence != null && presence.OwnerID == Owner.Rival) presence.TickPresence(dt, game.Dwell);

            commuteTimer += dt;
            if (commuteTimer >= commuteInterval)
            {
                commuteTimer = 0f;
                Commute();
            }

            actionTimer += dt;
            if (actionTimer >= actionInterval)
            {
                actionTimer -= actionInterval;
                Act();
            }
        }

        void Act()
        {
            if (Random.value < attackChance && TryAttackWeakBorder()) return;
            if (TryExpand()) return;
            if (TryAttackWeakBorder()) return; // boxed in -> push into the player
            TryReinforceBorder();
        }

        bool TryExpand()
        {
            HexTile best = null;
            float bestScore = float.MinValue;
            foreach (HexTile t in grid.Tiles)
            {
                if (t.IsOccupied) continue;
                int own = grid.CountNeighborsOwnedBy(t, Owner.Rival);
                if (own == 0) continue;
                float score = own + Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best != null && game.TryClaim(Owner.Rival, energy, best) == ActionResult.Ok;
        }

        bool TryAttackWeakBorder()
        {
            HexTile weakest = null;
            foreach (HexTile t in grid.Tiles)
            {
                if (t.OwnerID != Owner.Player || !grid.IsAdjacentToOwner(t, Owner.Rival)) continue;
                if (weakest == null || t.DefensePower < weakest.DefensePower) weakest = t;
            }
            if (weakest == null) return false;

            int needed = Mathf.FloorToInt(weakest.DefensePower * game.DefenderBonus) + 1;
            int spend;
            if (energy.Whole >= needed + 2) spend = Mathf.Min(energy.Whole, needed + 2); // likely capture
            else if (energy.Whole >= 25) spend = energy.Whole / 2;                        // wear it down
            else return false;

            return game.TryAttack(Owner.Rival, energy, weakest, spend, out _) == ActionResult.Ok;
        }

        void TryReinforceBorder()
        {
            if (energy.Whole < 10) return; // keep a reserve for expansion
            HexTile weakest = null;
            foreach (HexTile t in grid.Tiles)
            {
                if (t.OwnerID != Owner.Rival || !grid.IsAdjacentToOwner(t, Owner.Player)) continue;
                if (weakest == null || t.DefensePower < weakest.DefensePower) weakest = t;
            }
            if (weakest == null) return;

            float roll = Random.value;
            UnitType type = roll < 0.1f ? UnitType.Turret : roll < 0.3f ? UnitType.Vehicle : UnitType.Troops;
            if (game.TryReinforce(Owner.Rival, energy, weakest, type) != ActionResult.Ok)
                game.TryReinforce(Owner.Rival, energy, weakest, UnitType.Troops);
        }

        void Commute()
        {
            if (home == null || home.OwnerID != Owner.Rival) home = PickAnyOwned();
            if (home == null) return;

            if ((work == null || work.OwnerID != Owner.Rival) && grid.CountOwned(Owner.Rival) >= ownedHexesBeforeCommuting)
                work = PickFarthestOwnedFrom(home);

            HexTile next = presence == home && work != null ? work : home;
            presence = next;
            if (marker != null) marker.Follow(presence);
        }

        HexTile PickAnyOwned()
        {
            candidates.Clear();
            foreach (HexTile t in grid.Tiles) if (t.OwnerID == Owner.Rival) candidates.Add(t);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        HexTile PickFarthestOwnedFrom(HexTile origin)
        {
            HexTile best = null;
            int bestDistance = -1;
            foreach (HexTile t in grid.Tiles)
            {
                if (t.OwnerID != Owner.Rival) continue;
                int d = HexCoord.Distance(t.Coord, origin.Coord);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }
            return best == origin ? null : best;
        }
    }
}
