using System;
using System.Collections.Generic;

namespace Game.Towers
{
    public enum MatchPhase { Uninitialized, Ready, Active, Completed }
    public enum MatchOutcomeKind { None, Winner, Draw }
    public enum MatchCommandStatus
    {
        Accepted, Duplicate, InvalidOwner, UnknownOwner, WrongPhase, Completed,
        StaleTick, PendingTickMustResolve, DispatchInProgress
    }

    // A value snapshot. No consumer can change a completed result.
    public readonly struct MatchResult
    {
        public MatchOutcomeKind Kind { get; }
        public OwnerId Winner { get; }
        public int Tick { get; }
        internal MatchResult(MatchOutcomeKind kind, OwnerId winner, int tick)
        {
            Kind = kind;
            Winner = winner;
            Tick = tick;
        }
    }

    // The only outcome policy. The caller must close each simulation tick after all
    // producers have submitted; no terminal event is emitted during submission.
    public sealed class MatchFlowState
    {
        private readonly OwnerId[] owners;
        private readonly Dictionary<OwnerId, int> indexes;
        private readonly bool[] eliminated;
        private readonly bool[] pendingEliminations;
        private readonly bool[] pendingArrivals;
        private MatchPhase phase;
        private MatchResult result;
        private int pendingTick = -1;
        private int lastResolvedTick = -1;
        private int survivorCount;
        private bool dispatching;

        public MatchPhase Phase => phase;
        public MatchResult Result => result;
        public int PendingTick => pendingTick;
        public int LastResolvedTick => lastResolvedTick;
        public int FactionCount => owners.Length;
        public int SurvivorCount => survivorCount;
        public event Action<OwnerId> FactionEliminated;
        public event Action<MatchResult> ResultResolved;
        public event Action<MatchPhase> PhaseChanged;

        public MatchFlowState(IReadOnlyList<OwnerId> factionOwners)
        {
            if (factionOwners == null || factionOwners.Count != 4)
                throw new ArgumentException("MatchFlow requires exactly four faction owners.", nameof(factionOwners));
            owners = new OwnerId[4];
            indexes = new Dictionary<OwnerId, int>(4);
            eliminated = new bool[4];
            pendingEliminations = new bool[4];
            pendingArrivals = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                OwnerId owner = factionOwners[i];
                if (!owner.IsValid || indexes.ContainsKey(owner))
                    throw new ArgumentException("Faction owners must be nonempty and unique.", nameof(factionOwners));
                owners[i] = owner;
                indexes.Add(owner, i);
            }
            Reset();
        }

        public OwnerId GetOwner(int index) => owners[index];

        public bool TryGetFactionStatus(OwnerId owner, out bool isEliminated)
        {
            if (indexes.TryGetValue(owner, out int index))
            {
                isEliminated = eliminated[index];
                return true;
            }
            isEliminated = false;
            return false;
        }

        public bool IsEliminationPending(OwnerId owner) =>
            indexes.TryGetValue(owner, out int index) && pendingEliminations[index];

        public bool IsArrivalPending(OwnerId owner) =>
            indexes.TryGetValue(owner, out int index) && pendingArrivals[index];

        public bool CanIssueGameplayCommands(OwnerId owner) =>
            phase == MatchPhase.Active && indexes.TryGetValue(owner, out int index) &&
            !eliminated[index] && !pendingEliminations[index];

        public bool StartMatch()
        {
            if (dispatching || phase != MatchPhase.Ready) return false;
            phase = MatchPhase.Active;
            dispatching = true;
            try { PhaseChanged?.Invoke(phase); }
            finally { dispatching = false; }
            return true;
        }

        // Explicit restart clears outcome, eliminations and the tick clock.
        public bool Reset()
        {
            if (dispatching) return false;
            Array.Clear(eliminated, 0, 4);
            Array.Clear(pendingEliminations, 0, 4);
            Array.Clear(pendingArrivals, 0, 4);
            pendingTick = -1;
            lastResolvedTick = -1;
            survivorCount = 4;
            result = default;
            phase = MatchPhase.Ready;
            dispatching = true;
            try { PhaseChanged?.Invoke(phase); }
            finally { dispatching = false; }
            return true;
        }

        public MatchCommandStatus SubmitElimination(OwnerId owner, int tick)
        {
            MatchCommandStatus status = CheckCommand(owner, tick, out int index);
            if (status != MatchCommandStatus.Accepted) return status;
            if (eliminated[index] || pendingEliminations[index]) return MatchCommandStatus.Duplicate;
            pendingTick = tick;
            pendingEliminations[index] = true;
            return MatchCommandStatus.Accepted;
        }

        public MatchCommandStatus SubmitArrival(OwnerId owner, int tick)
        {
            MatchCommandStatus status = CheckCommand(owner, tick, out int index);
            if (status != MatchCommandStatus.Accepted) return status;
            if (eliminated[index]) return MatchCommandStatus.WrongPhase;
            if (pendingArrivals[index]) return MatchCommandStatus.Duplicate;
            pendingTick = tick;
            pendingArrivals[index] = true;
            return MatchCommandStatus.Accepted;
        }

        // Invoke at the end of the simulation tick. Eliminations commit first,
        // then only surviving owners' arrivals can determine the result.
        public MatchCommandStatus ResolveTick(int tick)
        {
            if (dispatching) return MatchCommandStatus.DispatchInProgress;
            if (phase == MatchPhase.Completed) return MatchCommandStatus.Completed;
            if (phase != MatchPhase.Active) return MatchCommandStatus.WrongPhase;
            if (tick < 0 || tick <= lastResolvedTick) return MatchCommandStatus.StaleTick;
            if (pendingTick >= 0 && pendingTick != tick) return MatchCommandStatus.PendingTickMustResolve;

            int eliminatedMask = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!pendingEliminations[i]) continue;
                eliminated[i] = true;
                survivorCount--;
                eliminatedMask |= 1 << i;
            }
            int eligibleCount = 0;
            OwnerId eligibleOwner = default;
            for (int i = 0; i < 4; i++)
            {
                if (!pendingArrivals[i] || eliminated[i]) continue;
                eligibleCount++;
                eligibleOwner = owners[i];
            }
            lastResolvedTick = tick;
            pendingTick = -1;
            Array.Clear(pendingEliminations, 0, 4);
            Array.Clear(pendingArrivals, 0, 4);

            MatchOutcomeKind outcome = eligibleCount > 1 || survivorCount == 0
                ? MatchOutcomeKind.Draw : eligibleCount == 1 ? MatchOutcomeKind.Winner : MatchOutcomeKind.None;
            // Lock terminal state before notifying listeners, so callbacks cannot
            // submit another command and change this result.
            if (outcome != MatchOutcomeKind.None)
            {
                result = new MatchResult(outcome, outcome == MatchOutcomeKind.Winner ? eligibleOwner : default, tick);
                phase = MatchPhase.Completed;
            }
            dispatching = true;
            try
            {
                for (int i = 0; i < 4; i++)
                    if ((eliminatedMask & (1 << i)) != 0) FactionEliminated?.Invoke(owners[i]);
                if (outcome != MatchOutcomeKind.None)
                {
                    PhaseChanged?.Invoke(phase);
                    ResultResolved?.Invoke(result);
                }
            }
            finally { dispatching = false; }
            return MatchCommandStatus.Accepted;
        }

        private MatchCommandStatus CheckCommand(OwnerId owner, int tick, out int index)
        {
            index = -1;
            if (dispatching) return MatchCommandStatus.DispatchInProgress;
            if (!owner.IsValid) return MatchCommandStatus.InvalidOwner;
            if (!indexes.TryGetValue(owner, out index)) return MatchCommandStatus.UnknownOwner;
            if (phase == MatchPhase.Completed) return MatchCommandStatus.Completed;
            if (phase != MatchPhase.Active) return MatchCommandStatus.WrongPhase;
            if (tick < 0 || tick <= lastResolvedTick) return MatchCommandStatus.StaleTick;
            if (pendingTick >= 0 && pendingTick != tick) return MatchCommandStatus.PendingTickMustResolve;
            return MatchCommandStatus.Accepted;
        }

    }
}
