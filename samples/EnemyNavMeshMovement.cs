using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Towers.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavMeshMovement : MonoBehaviour
    {
        [SerializeField] private EnemyTargetRegistry registry;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField, Min(1f)] private float searchRadius = 2000f;
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.5f;
        [SerializeField, Range(1, 16)] private int maxPathChecksPerRefresh = 3;
        [SerializeField, Min(0.05f)] private float destinationUpdateInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float destinationMoveThreshold = 0.5f;

        private readonly List<EnemyTargetMarker> candidates = new List<EnemyTargetMarker>();
        private NavMeshPath path;
        private EnemyTargetMarker currentTarget;
        private Vector3 lastDestination;
        private int nextCandidate;
        private float nextRefresh;
        private float nextDestinationUpdate;
        private string lastProblem;

        public EnemyTargetMarker CurrentTarget => currentTarget;

        // Called while a pooled instance is inactive, before OnEnable starts target acquisition.
        public void SetRegistry(EnemyTargetRegistry value) => registry = value;

        private void Awake()
        {
            path = new NavMeshPath();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            currentTarget = null;
            candidates.Clear();
            nextCandidate = 0;
            // Spread the first expensive query across the refresh interval.
            nextRefresh = Time.time + Mathf.Repeat(GetInstanceID() * 0.618034f,
                Mathf.Max(0.05f, targetRefreshInterval));
            nextDestinationUpdate = Time.time;
        }

        private void OnDisable()
        {
            currentTarget = null;
            candidates.Clear();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }

        private void Update()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            { StopAndWarn("Assign an enabled NavMeshAgent placed on a compatible baked NavMesh."); return; }
            if (registry == null || !registry.isActiveAndEnabled)
            { StopAndWarn("Assign an active EnemyTargetRegistry."); return; }
            if (currentTarget != null && !currentTarget.IsSelectable)
            {
                currentTarget = null;
                agent.ResetPath();
                nextRefresh = Time.time;
            }

            if (Time.time >= nextRefresh)
            {
                if (nextCandidate == 0)
                {
                    registry.Collect(transform.position, searchRadius, candidates);
                    candidates.Sort(CompareDistance);
                }
                SelectReachableTarget();
                nextRefresh = Time.time + targetRefreshInterval;
            }

            if (currentTarget != null && Time.time >= nextDestinationUpdate)
            {
                Vector3 position = currentTarget.Position;
                if ((position - lastDestination).sqrMagnitude >= destinationMoveThreshold * destinationMoveThreshold)
                {
                    // Recheck reachability on the next bounded selection pass.
                    nextCandidate = 0;
                    nextRefresh = Time.time;
                }
                nextDestinationUpdate = Time.time + destinationUpdateInterval;
            }
        }

        private int CompareDistance(EnemyTargetMarker a, EnemyTargetMarker b)
        {
            float da = (a.Position - transform.position).sqrMagnitude;
            float db = (b.Position - transform.position).sqrMagnitude;
            int order = da.CompareTo(db);
            return order != 0 ? order : a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private void SelectReachableTarget()
        {
            int checks = 0;
            while (nextCandidate < candidates.Count && checks < maxPathChecksPerRefresh)
            {
                EnemyTargetMarker candidate = candidates[nextCandidate++];
                if (candidate == null || !candidate.IsSelectable) continue;
                checks++;
                Vector3 destination = candidate.Position;
                if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                if (!agent.SetPath(path)) continue;
                currentTarget = candidate;
                lastDestination = destination;
                nextCandidate = 0;
                lastProblem = null;
                return;
            }

            if (nextCandidate < candidates.Count) return; // Continue the ordered search next refresh.
            nextCandidate = 0;
            currentTarget = null;
            if (agent.hasPath) agent.ResetPath();
            WarnOnce(candidates.Count == 0
                ? "No active Base or Tower targets are registered within Search Radius."
                : "No registered Base or Tower has a complete NavMesh path within Search Radius.");
        }

        private void StopAndWarn(string problem)
        {
            currentTarget = null;
            candidates.Clear();
            nextCandidate = 0;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
            WarnOnce(problem);
        }

        private void WarnOnce(string problem)
        {
            if (lastProblem == problem) return;
            lastProblem = problem;
            Debug.LogWarning($"[EnemyNavMeshMovement] {name}: {problem}", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            searchRadius = Mathf.Max(1f, searchRadius);
            targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
            maxPathChecksPerRefresh = Mathf.Clamp(maxPathChecksPerRefresh, 1, 16);
            destinationUpdateInterval = Mathf.Max(0.05f, destinationUpdateInterval);
            destinationMoveThreshold = Mathf.Max(0.01f, destinationMoveThreshold);
        }
#endif
    }
}
