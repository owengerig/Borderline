using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Borderline
{
    /// <summary>
    /// One hex cell: coordinate, owner, garrison (troops / vehicles / turrets),
    /// dwell-built walls, and its visuals (vertex-coloured hex mesh + TextMeshPro label).
    ///
    /// DefensePower = Troops*1 + Vehicles*4 + Turrets*6 + Fortification - armour damage.
    /// Damage is applied in this order: troops -> vehicles -> turrets -> walls,
    /// so the long-term, dwell-built walls are the last thing to fall.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HexTile : MonoBehaviour
    {
        public static readonly Color NeutralColor = new Color(0.42f, 0.44f, 0.47f);
        public static readonly Color PlayerColor = new Color(0.20f, 0.47f, 0.96f);
        public static readonly Color RivalColor = new Color(0.90f, 0.26f, 0.24f);

        [SerializeField] TextMeshPro label;
        [Tooltip("Fill radius as a fraction of the cell radius. Below 1 leaves a gap that reads as grid lines.")]
        [SerializeField, Range(0.7f, 1f)] float fillScale = 0.93f;
        [Tooltip("Defence at which the tile colour reaches its darkest 'fortress' shade.")]
        [SerializeField, Min(1)] int fullStrengthDefense = 120;

        public HexCoord Coord { get; private set; }
        public int OwnerID { get; private set; }
        public int Troops => troops;
        public int Vehicles => vehicles;
        public int Turrets => turrets;
        public int Fortification => fortification;
        public float DwellSeconds { get; private set; }
        public bool IsOccupied => OwnerID != Owner.None;

        public int DefensePower => Mathf.Max(0,
            troops * UnitStats.TroopPower +
            vehicles * UnitStats.VehiclePower +
            turrets * UnitStats.TurretPower +
            fortification - armorDamage);

        int troops, vehicles, turrets, fortification;
        int armorDamage;              // partial damage on the top armoured unit
        float fortificationProgress;  // fractional wall points
        float nextAutoTurretAt;

        Mesh mesh;
        Color[] colorBuffer;
        Color currentColor;
        Coroutine flashRoutine;
        readonly List<string> labelParts = new List<string>(3);

        public void Init(HexCoord coord, float cellRadius, Material material)
        {
            Coord = coord;
            name = $"Hex {coord.q},{coord.r}";

            var meshRenderer = GetComponent<MeshRenderer>();
            if (material != null) meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = 0;

            mesh = HexMesh.CreateHex(cellRadius * fillScale);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            colorBuffer = new Color[mesh.vertexCount];

            EnsureLabel(cellRadius);
            ResetToNeutral();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        // ------------------------------------------------------------------ state changes

        public void ResetToNeutral() => SetOwner(Owner.None, 0);

        /// <summary>Hands the hex to a new owner with a fresh garrison of troops. Walls and dwell are lost.</summary>
        public void SetOwner(int owner, int startingTroops)
        {
            OwnerID = owner;
            troops = owner == Owner.None ? 0 : Mathf.Max(0, startingTroops);
            vehicles = turrets = fortification = armorDamage = 0;
            fortificationProgress = 0f;
            nextAutoTurretAt = 0f;
            DwellSeconds = 0f;
            Refresh();
        }

        public bool CanAdd(UnitType type)
        {
            int count = type == UnitType.Vehicle ? vehicles : type == UnitType.Turret ? turrets : troops;
            return count < UnitStats.MaxPerTile(type);
        }

        /// <summary>Adds units (also repairs any partial armour damage).</summary>
        public void AddUnits(UnitType type, int amount)
        {
            int max = UnitStats.MaxPerTile(type);
            switch (type)
            {
                case UnitType.Vehicle: vehicles = Mathf.Min(max, vehicles + amount); break;
                case UnitType.Turret: turrets = Mathf.Min(max, turrets + amount); break;
                default: troops = Mathf.Min(max, troops + amount); break;
            }
            armorDamage = 0;
            Refresh();
        }

        /// <summary>
        /// Removes defence. Never takes DefensePower below <paramref name="floor"/>.
        /// Returns the damage actually dealt.
        /// </summary>
        public int ApplyDamage(int damage, int floor = 0)
        {
            int remaining = Mathf.Min(damage, Mathf.Max(0, DefensePower - floor));
            if (remaining <= 0) return 0;
            int dealt = remaining;

            int t = Mathf.Min(troops, remaining);
            troops -= t;
            remaining -= t;

            DamageArmored(ref vehicles, UnitStats.VehiclePower, ref remaining);
            DamageArmored(ref turrets, UnitStats.TurretPower, ref remaining);

            int f = Mathf.Min(fortification, remaining);
            fortification -= f;

            Refresh();
            return dealt;
        }

        void DamageArmored(ref int count, int power, ref int remaining)
        {
            while (remaining > 0 && count > 0)
            {
                int hp = power - armorDamage;
                if (remaining >= hp)
                {
                    count--;
                    remaining -= hp;
                    armorDamage = 0;
                }
                else
                {
                    armorDamage += remaining;
                    remaining = 0;
                }
            }
        }

        /// <summary>
        /// Call every frame while the owner's presence is on this hex.
        /// Returns true when a free turret was built this frame.
        /// </summary>
        public bool TickPresence(float deltaTime, DwellSettings settings)
        {
            if (!IsOccupied || settings == null) return false;

            DwellSeconds += deltaTime;
            bool changed = false;
            bool turretBuilt = false;

            fortificationProgress += settings.FortificationPerSecond(DwellSeconds) * deltaTime;
            if (fortificationProgress >= 1f)
            {
                int whole = Mathf.FloorToInt(fortificationProgress);
                fortificationProgress -= whole;
                int before = fortification;
                fortification = Mathf.Min(settings.maxFortification, fortification + whole);
                changed = fortification != before;
            }

            if (settings.secondsPerAutoTurret > 0f)
            {
                if (nextAutoTurretAt <= 0f) nextAutoTurretAt = settings.secondsPerAutoTurret;
                if (DwellSeconds >= nextAutoTurretAt)
                {
                    nextAutoTurretAt += settings.secondsPerAutoTurret;
                    if (CanAdd(UnitType.Turret))
                    {
                        turrets++;
                        turretBuilt = changed = true;
                    }
                }
            }

            if (changed) Refresh();
            return turretBuilt;
        }

        // ------------------------------------------------------------------ visuals

        public void Flash(Color color, float duration = 0.3f)
        {
            if (!isActiveAndEnabled || mesh == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        IEnumerator FlashRoutine(Color color, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                HexMesh.SetColor(mesh, Color.Lerp(color, currentColor, t / duration), colorBuffer);
                yield return null;
            }
            HexMesh.SetColor(mesh, currentColor, colorBuffer);
            flashRoutine = null;
        }

        void Refresh()
        {
            Color baseColor = OwnerID == Owner.Player ? PlayerColor
                            : OwnerID == Owner.Rival ? RivalColor
                            : NeutralColor;

            if (IsOccupied)
            {
                // Stronger hexes get darker / more saturated so fortresses stand out.
                float strength = Mathf.Clamp01(DefensePower / (float)fullStrengthDefense);
                Color dark = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 1f);
                baseColor = Color.Lerp(baseColor, dark, strength);
            }

            currentColor = baseColor;
            if (mesh != null && flashRoutine == null) HexMesh.SetColor(mesh, currentColor, colorBuffer);

            if (label == null) return;
            if (!IsOccupied)
            {
                label.text = string.Empty;
                return;
            }

            labelParts.Clear();
            if (vehicles > 0) labelParts.Add($"V{vehicles}");
            if (turrets > 0) labelParts.Add($"G{turrets}");
            if (fortification > 0) labelParts.Add($"W{fortification}");

            label.text = labelParts.Count == 0
                ? DefensePower.ToString()
                : $"{DefensePower}\n<size=55%>{string.Join(" ", labelParts)}</size>";
        }

        void EnsureLabel(float cellRadius)
        {
            if (label == null) label = GetComponentInChildren<TextMeshPro>(true);
            if (label == null)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                label = go.AddComponent<TextMeshPro>();
            }

            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.6f;
            label.fontSizeMax = 3.2f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.richText = true;
            label.rectTransform.sizeDelta = new Vector2(cellRadius * 1.5f, cellRadius * 1.1f);
            label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            label.sortingOrder = 10;
        }
    }
}
