using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Borderline
{
    /// <summary>
    /// Owns the match: board setup, the player's energy, tap handling, the shared territory rules
    /// (claim / reinforce / attack - also used by RivalAI), presence & dwell fortification,
    /// automated turret fire, and win/lose.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene references (auto-found if left empty)")]
        [SerializeField] HexGridManager grid;
        [SerializeField] CameraController cameraController;
        [SerializeField] RivalAI rival;

        [Header("Player energy (+1/s, max 50)")]
        [SerializeField] EnergyPool playerEnergy = new EnergyPool();

        [Header("Start positions (offset col,row; rival is measured from the top-right corner)")]
        [SerializeField] Vector2Int playerHomeOffset = new Vector2Int(2, 2);
        [SerializeField] Vector2Int rivalHomeInset = new Vector2Int(2, 2);
        [SerializeField, Min(1)] int homeDefense = 10;

        [Header("Territory rules")]
        [SerializeField, Min(0)] int claimCost = 2;
        [SerializeField, Min(1)] int claimDefense = 5;
        [SerializeField, Min(1f)] float defenderBonus = 1.2f;
        [Tooltip("Attack power added per friendly vehicle bordering the target hex.")]
        [SerializeField, Min(0)] int vehicleSupportPower = 3;
        [SerializeField, Min(0)] int maxSupportVehicles = 6;

        [Header("Presence / dwell fortification")]
        [SerializeField] DwellSettings dwell = new DwellSettings();

        [Header("Automated defences (turrets)")]
        [SerializeField, Min(0.2f)] float turretFireInterval = 3f;
        [SerializeField, Min(0)] int turretDamagePerTurret = 1;

        public HexGridManager Grid => grid;
        public EnergyPool PlayerEnergy => playerEnergy;
        public DwellSettings Dwell => dwell;
        public float DefenderBonus => defenderBonus;
        public HexTile PlayerPresence { get; private set; }
        public UnitType SelectedUnit { get; set; } = UnitType.Troops;
        public float AttackFraction { get; set; } = 0.5f;
        public bool PlacingPresence { get; set; }
        public bool IsGameOver { get; private set; }
        public bool PlayerWon { get; private set; }

        /// <summary>Short feedback messages for the HUD.</summary>
        public event Action<string> Toast;
        /// <summary>Raised once when the match ends; argument = player won.</summary>
        public event Action<bool> GameEnded;

        static readonly Color ClaimFlash = Color.white;
        static readonly Color ReinforceFlash = new Color(0.7f, 1f, 0.7f);
        static readonly Color HitFlash = new Color(1f, 0.6f, 0.1f);
        static readonly Color PresenceColor = new Color(1f, 0.85f, 0.2f);

        PresenceMarker playerMarker;
        float turretTimer;
        readonly List<HexTile> neighborBuffer = new List<HexTile>(6);

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            if (grid == null) grid = FindFirstObjectByType<HexGridManager>();
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            if (rival == null) rival = FindFirstObjectByType<RivalAI>();
            Application.targetFrameRate = 60;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (cameraController != null) cameraController.Tapped -= HandleTap;
        }

        void Start()
        {
            if (grid == null)
            {
                Debug.LogError("[Borderline] No HexGridManager in the scene.");
                enabled = false;
                return;
            }

            grid.Generate();
            playerEnergy.Reset();

            HexTile home = grid.GetTileAtOffset(playerHomeOffset.x, playerHomeOffset.y);
            HexTile rivalHome = grid.GetTileAtOffset(grid.Width - 1 - rivalHomeInset.x, grid.Height - 1 - rivalHomeInset.y);
            home.SetOwner(Owner.Player, homeDefense);
            rivalHome.SetOwner(Owner.Rival, homeDefense);

            playerMarker = PresenceMarker.Create("Player Presence", grid.CellRadius, grid.TileMaterial, PresenceColor, 5);
            SetPlayerPresence(home, silent: true);

            if (cameraController != null)
            {
                cameraController.SetBounds(grid.WorldBounds);
                cameraController.FocusOn(home.transform.position);
                cameraController.Tapped += HandleTap;
            }

            if (rival != null) rival.Begin(this, rivalHome);
            RaiseToast("Tap gray hexes next to your blue territory to expand.");
        }

        void Update()
        {
            if (IsGameOver) return;
            float dt = Time.deltaTime;

            playerEnergy.Tick(dt);

            if (PlayerPresence != null && PlayerPresence.OwnerID == Owner.Player)
            {
                if (PlayerPresence.TickPresence(dt, dwell))
                    RaiseToast("Your crew built a turret where you're standing.");
            }

            turretTimer += dt;
            if (turretTimer >= turretFireInterval)
            {
                turretTimer = 0f;
                FireTurrets();
            }

            CheckGameOver();
        }

        // ------------------------------------------------------------------ player input

        void HandleTap(Vector2 worldPosition)
        {
            if (IsGameOver) return;
            if (!grid.TryGetTileAtWorld(worldPosition, out HexTile tile)) return;

            if (PlacingPresence)
            {
                PlacingPresence = false;
                SetPlayerPresence(tile);
                return;
            }

            if (tile.OwnerID == Owner.None) PlayerClaim(tile);
            else if (tile.OwnerID == Owner.Player) PlayerReinforce(tile);
            else PlayerAttack(tile);
        }

        void PlayerClaim(HexTile tile)
        {
            switch (TryClaim(Owner.Player, playerEnergy, tile))
            {
                case ActionResult.Ok: tile.Flash(ClaimFlash); break;
                case ActionResult.NotAdjacent: RaiseToast("You can only claim hexes that border your territory."); break;
                case ActionResult.NotEnoughEnergy: RaiseToast($"Need {claimCost} energy to claim."); break;
            }
        }

        void PlayerReinforce(HexTile tile)
        {
            UnitType type = SelectedUnit;
            switch (TryReinforce(Owner.Player, playerEnergy, tile, type))
            {
                case ActionResult.Ok:
                    tile.Flash(ReinforceFlash);
                    break;
                case ActionResult.NotEnoughEnergy:
                    RaiseToast($"{UnitStats.DisplayName(type)} costs {UnitStats.Cost(type)} energy.");
                    break;
                case ActionResult.AtCapacity:
                    RaiseToast($"This hex already has the maximum {UnitStats.MaxPerTile(type)} {UnitStats.DisplayName(type).ToLower()}s.");
                    break;
            }
        }

        void PlayerAttack(HexTile tile)
        {
            int spend = Mathf.Max(1, Mathf.FloorToInt(playerEnergy.Current * AttackFraction));
            switch (TryAttack(Owner.Player, playerEnergy, tile, spend, out AttackReport r))
            {
                case ActionResult.Ok:
                    string support = r.SupportVehicles > 0 ? $" (+{r.SupportVehicles} vehicle support)" : "";
                    if (r.Captured)
                    {
                        tile.Flash(ClaimFlash);
                        RaiseToast($"Captured! {r.AttackPower} attack{support} vs {r.EffectiveDefense:0.#} defence. {r.Survivors} troops hold it.");
                    }
                    else
                    {
                        tile.Flash(HitFlash);
                        RaiseToast($"Repelled. {r.AttackPower} attack{support} vs {r.EffectiveDefense:0.#} defence. Dealt {r.Damage} damage.");
                    }
                    break;
                case ActionResult.NotAdjacent: RaiseToast("You can only attack hexes that border your territory."); break;
                case ActionResult.NotEnoughEnergy: RaiseToast("Not enough energy to attack."); break;
            }
        }

        public void SetPlayerPresence(HexTile tile, bool silent = false)
        {
            PlayerPresence = tile;
            if (playerMarker != null) playerMarker.Follow(tile);
            if (silent || tile == null) return;
            RaiseToast(tile.OwnerID == Owner.Player
                ? "You're here. This hex fortifies faster the longer you stay."
                : "You're here, but you don't own this hex, so it won't fortify.");
        }

        // ------------------------------------------------------------------ shared rules (player + AI)

        public ActionResult TryClaim(int owner, EnergyPool pool, HexTile tile)
        {
            if (tile == null || tile.OwnerID != Owner.None) return ActionResult.InvalidTarget;
            if (!grid.IsAdjacentToOwner(tile, owner)) return ActionResult.NotAdjacent;
            if (!pool.TrySpend(claimCost)) return ActionResult.NotEnoughEnergy;
            tile.SetOwner(owner, claimDefense);
            return ActionResult.Ok;
        }

        public ActionResult TryReinforce(int owner, EnergyPool pool, HexTile tile, UnitType type)
        {
            if (tile == null || tile.OwnerID != owner) return ActionResult.InvalidTarget;
            if (!tile.CanAdd(type)) return ActionResult.AtCapacity;
            if (!pool.TrySpend(UnitStats.Cost(type))) return ActionResult.NotEnoughEnergy;
            tile.AddUnits(type, UnitStats.AmountPerPurchase(type));
            return ActionResult.Ok;
        }

        public ActionResult TryAttack(int attacker, EnergyPool pool, HexTile target, int energyToSpend, out AttackReport report)
        {
            report = default;
            if (target == null || !target.IsOccupied || target.OwnerID == attacker) return ActionResult.InvalidTarget;
            if (!grid.IsAdjacentToOwner(target, attacker)) return ActionResult.NotAdjacent;
            if (energyToSpend < 1 || !pool.TrySpend(energyToSpend)) return ActionResult.NotEnoughEnergy;

            report = Combat.Resolve(grid, target, attacker, energyToSpend, defenderBonus, vehicleSupportPower, maxSupportVehicles);
            return ActionResult.Ok;
        }

        // ------------------------------------------------------------------ automated defences

        void FireTurrets()
        {
            if (turretDamagePerTurret <= 0) return;
            foreach (HexTile tile in grid.Tiles)
            {
                if (tile.Turrets == 0 || !tile.IsOccupied) continue;

                grid.GetNeighbors(tile, neighborBuffer);
                HexTile weakest = null;
                foreach (HexTile n in neighborBuffer)
                {
                    if (!n.IsOccupied || n.OwnerID == tile.OwnerID || n.DefensePower <= 1) continue;
                    if (weakest == null || n.DefensePower < weakest.DefensePower) weakest = n;
                }

                // Turrets suppress but never capture: they can't take a hex below 1 defence.
                if (weakest != null && weakest.ApplyDamage(tile.Turrets * turretDamagePerTurret, floor: 1) > 0)
                    weakest.Flash(HitFlash, 0.2f);
            }
        }

        // ------------------------------------------------------------------ match state

        void CheckGameOver()
        {
            int player = grid.CountOwned(Owner.Player);
            int rivalCount = grid.CountOwned(Owner.Rival);
            if (player == 0) EndGame(false);
            else if (rivalCount == 0) EndGame(true);
        }

        void EndGame(bool playerWon)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            PlayerWon = playerWon;
            PlacingPresence = false;
            GameEnded?.Invoke(playerWon);
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        void RaiseToast(string message) => Toast?.Invoke(message);
    }
}
