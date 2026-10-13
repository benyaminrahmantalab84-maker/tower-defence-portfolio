using System;
using System.Collections.Generic;
using Game.Towers.Data;
using UnityEngine;

namespace Game.Towers
{
    public enum TowerUpgradeResult { Upgraded, AtMaxLevel, InvalidOwner, InvalidTowerType, Unavailable, SynchronizationFailed }

    [DisallowMultipleComponent]
    public sealed class TowerProgressionService : MonoBehaviour
    {
        [SerializeField, Tooltip("Include types that can upgrade before any towers are placed.")]
        private List<TowerDefinition> towerDefinitions = new List<TowerDefinition>();
        [SerializeField] private bool enableLogging = true;

        private sealed class Group
        {
            public int Level = 1;
            public readonly HashSet<TowerRoot> Towers = new HashSet<TowerRoot>();
        }
        private readonly Dictionary<TowerTypeId, TowerDefinition> definitions = new Dictionary<TowerTypeId, TowerDefinition>();
        private readonly Dictionary<TowerProgressionKey, Group> groups = new Dictionary<TowerProgressionKey, Group>();
        private readonly Dictionary<TowerRoot, TowerProgressionKey> registrations = new Dictionary<TowerRoot, TowerProgressionKey>();
        private readonly HashSet<TowerTypeId> conflictingTypes = new HashSet<TowerTypeId>();
        private readonly List<TowerRoot> synchronizationBuffer = new List<TowerRoot>();
        private bool catalogLoaded;
        private bool synchronizing;

        public event Action AvailabilityChanged;
        public event Action<OwnerId, TowerTypeId, int> LevelChanged;
        public int RegisteredTowerCount => registrations.Count;
        public bool EnableLogging => enableLogging;
        public int GetRegisteredTowerCount(OwnerId owner, TowerTypeId type) =>
            groups.TryGetValue(new TowerProgressionKey(owner, type), out Group group) ? group.Towers.Count : 0;

        // Call when a bootstrap reuses this scene/service for a new match.
        // Scene destruction already discards all state. Disabling a service only suspends it.
        public bool ResetMatch()
        {
            if (!isActiveAndEnabled || synchronizing) return false;
            foreach (Group group in groups.Values) group.Level = 1;
            synchronizationBuffer.Clear();
            foreach (TowerRoot tower in registrations.Keys) synchronizationBuffer.Add(tower);
            synchronizing = true;
            try
            {
                foreach (TowerRoot tower in synchronizationBuffer)
                    if (tower != null && tower.IsActive && registrations.ContainsKey(tower))
                        tower.Levels.ApplyProgressionLevel(1);
            }
            finally { synchronizing = false; synchronizationBuffer.Clear(); }
            if (enableLogging) Debug.Log("[Progression] New match: all owner/type levels reset to 1.", this);
            return true;
        }

        private void Awake() => EnsureCatalog();
        private void OnEnable() => AvailabilityChanged?.Invoke();
        private void OnDisable() => AvailabilityChanged?.Invoke();
        private void OnDestroy()
        {
            AvailabilityChanged?.Invoke();
            registrations.Clear(); groups.Clear(); definitions.Clear(); conflictingTypes.Clear();
        }

        private void EnsureCatalog()
        {
            if (catalogLoaded) return;
            catalogLoaded = true;
            foreach (TowerDefinition definition in towerDefinitions) RegisterDefinition(definition);
        }

        public bool RegisterDefinition(TowerDefinition definition)
        {
            if (definition == null || !definition.TowerTypeId.IsValid || definition.MaxLevel < 1) return false;
            TowerTypeId type = definition.TowerTypeId;
            if (conflictingTypes.Contains(type)) return false;
            if (definitions.TryGetValue(type, out TowerDefinition existing) && existing != definition)
            {
                conflictingTypes.Add(type);
                Debug.LogError($"[Progression] Duplicate TowerTypeId '{type}' on different definitions. Fix the data IDs.", this);
                return false;
            }
            definitions[type] = definition;
            return true;
        }

        public bool TryGetLevel(OwnerId owner, TowerDefinition definition, out int level)
        {
            EnsureCatalog();
            level = 1;
            if (!isActiveAndEnabled || !owner.IsValid || !RegisterDefinition(definition)) return false;
            level = GetGroup(new TowerProgressionKey(owner, definition.TowerTypeId)).Level;
            return level >= 1 && level <= definition.MaxLevel;
        }

        public bool TryGetLevel(OwnerId owner, TowerTypeId type, out int level)
        {
            EnsureCatalog();
            level = 1;
            return definitions.TryGetValue(type, out TowerDefinition definition) &&
                !conflictingTypes.Contains(type) && TryGetLevel(owner, definition, out level);
        }

        internal bool RegisterTower(TowerRoot tower)
        {
            if (tower == null || !tower.IsActive || tower.Owner == null || !tower.Owner.IsAvailable ||
                tower.Owner.ProgressionService != this || tower.Levels == null ||
                !TryGetLevel(tower.Owner.OwnerId, tower.Definition, out int level)) return false;
            var key = new TowerProgressionKey(tower.Owner.OwnerId, tower.Definition.TowerTypeId);
            if (registrations.TryGetValue(tower, out TowerProgressionKey previous))
            {
                if (previous.Equals(key)) return tower.Levels.ApplyProgressionLevel(level);
                UnregisterTower(tower);
            }
            if (!tower.Levels.ApplyProgressionLevel(level)) return false;
            if (!tower.IsActive || tower.Owner == null || !tower.Owner.IsAvailable ||
                tower.Owner.ProgressionService != this || !tower.Owner.OwnerId.Equals(key.Owner)) return false;
            // A level callback can re-enter registration through capture or lifecycle changes.
            if (registrations.ContainsKey(tower)) return registrations[tower].Equals(key);
            GetGroup(key).Towers.Add(tower);
            registrations.Add(tower, key);
            return true;
        }

        internal void UnregisterTower(TowerRoot tower)
        {
            if (ReferenceEquals(tower, null) || !registrations.TryGetValue(tower, out TowerProgressionKey key)) return;
            if (groups.TryGetValue(key, out Group group)) group.Towers.Remove(tower);
            registrations.Remove(tower);
        }

        public TowerUpgradeResult TryUpgradeTowerType(OwnerId owner, TowerDefinition definition)
        {
            EnsureCatalog();
            if (!RegisterDefinition(definition)) return TowerUpgradeResult.InvalidTowerType;
            return TryUpgradeTowerType(owner, definition.TowerTypeId);
        }

        public TowerUpgradeResult TryUpgradeTowerType(OwnerId owner, TowerTypeId type)
        {
            if (!isActiveAndEnabled || synchronizing) return TowerUpgradeResult.Unavailable;
            if (!owner.IsValid) return TowerUpgradeResult.InvalidOwner;
            EnsureCatalog();
            if (conflictingTypes.Contains(type) || !definitions.TryGetValue(type, out TowerDefinition definition))
                return TowerUpgradeResult.InvalidTowerType;
            Group group = GetGroup(new TowerProgressionKey(owner, type));
            if (group.Level < 1 || group.Level > definition.MaxLevel) return TowerUpgradeResult.SynchronizationFailed;
            if (group.Level == definition.MaxLevel)
            {
                if (enableLogging) Debug.Log($"[Progression] {owner} / {type} already at max level {group.Level}.", this);
                return TowerUpgradeResult.AtMaxLevel;
            }

            // Snapshot only this group: SetActive/level callbacks may disable or capture a tower.
            synchronizationBuffer.Clear();
            foreach (TowerRoot tower in group.Towers) synchronizationBuffer.Add(tower);
            int next = group.Level + 1;
            foreach (TowerRoot tower in synchronizationBuffer)
                if (tower != null && tower.IsActive && !tower.Levels.CanApplyLevel(next))
                { synchronizationBuffer.Clear(); return TowerUpgradeResult.SynchronizationFailed; }
            int previous = group.Level;
            group.Level = next;
            int synced = 0;
            synchronizing = true;
            try
            {
                foreach (TowerRoot tower in synchronizationBuffer)
                {
                    if (tower == null) { UnregisterTower(tower); continue; }
                    if (!registrations.TryGetValue(tower, out TowerProgressionKey key) ||
                        !key.Equals(new TowerProgressionKey(owner, type)) || !tower.IsActive) continue;
                    if (tower.Levels.ApplyProgressionLevel(next)) synced++;
                }
            }
            finally { synchronizing = false; synchronizationBuffer.Clear(); }
            if (enableLogging)
                Debug.Log($"[Progression] {owner} / {type}: Level {previous} -> {next}. Synced {synced} active towers.", this);
            LevelChanged?.Invoke(owner, type, next);
            return TowerUpgradeResult.Upgraded;
        }

        private Group GetGroup(TowerProgressionKey key)
        {
            if (!groups.TryGetValue(key, out Group group)) { group = new Group(); groups.Add(key, group); }
            return group;
        }

        public void GetConfigurationErrors(List<string> errors)
        {
            var ids = new Dictionary<TowerTypeId, TowerDefinition>();
            foreach (TowerDefinition definition in towerDefinitions)
            {
                if (definition == null || !definition.TowerTypeId.IsValid)
                { errors.Add("Progression catalog contains a missing definition or empty TowerTypeId."); continue; }
                if (ids.TryGetValue(definition.TowerTypeId, out TowerDefinition previous) && previous != definition)
                    errors.Add($"Duplicate TowerTypeId '{definition.Id}' on different catalog definitions.");
                ids[definition.TowerTypeId] = definition;
                if (definition.MaxLevel < 1) errors.Add($"'{definition.Id}' needs Max Level >= 1.");
            }
        }
    }
}
