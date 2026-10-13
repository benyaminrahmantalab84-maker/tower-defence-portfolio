using System;

namespace Game.Towers
{
    // Serialized identity lives on data/context components. Runtime keys use value types.
    public readonly struct OwnerId : IEquatable<OwnerId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public OwnerId(string value) { Value = value; }
        public bool Equals(OwnerId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is OwnerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct TowerTypeId : IEquatable<TowerTypeId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public TowerTypeId(string value) { Value = value; }
        public bool Equals(TowerTypeId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is TowerTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    internal readonly struct TowerProgressionKey : IEquatable<TowerProgressionKey>
    {
        public readonly OwnerId Owner;
        public readonly TowerTypeId Type;
        public TowerProgressionKey(OwnerId owner, TowerTypeId type) { Owner = owner; Type = type; }
        public bool Equals(TowerProgressionKey other) => Owner.Equals(other.Owner) && Type.Equals(other.Type);
        public override bool Equals(object obj) => obj is TowerProgressionKey other && Equals(other);
        public override int GetHashCode() => unchecked(Owner.GetHashCode() * 397 ^ Type.GetHashCode());
    }
}
