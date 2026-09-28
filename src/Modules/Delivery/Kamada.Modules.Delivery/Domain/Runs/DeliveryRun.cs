using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Delivery.Domain.Runs;

/// <summary>
/// A courier's delivery run (aggregate root): header, stops in delivery sequence and the stored route plan.
/// </summary>
/// <remarks>
/// <code>
/// Draft ──► Planned ──► InProgress ──► Completed
///   │          │
///   └──────────┴──► Cancelled
/// </code>
/// Stops and the route can be edited only in <c>Draft</c>. Any change of stops or endpoints clears the
/// stored plan, so an approved run always has a route that matches its stops.
/// </remarks>
internal sealed class DeliveryRun : Entity, IAuditable
{
    /// <summary>Upper bound of stops per run (the route optimizer has its own configurable limit).</summary>
    public const int MaxStops = 60;

    /// <summary>Failure reason recorded for stops still pending when a run is completed manually.</summary>
    public const string NotVisitedReason = "Nie odwiedzono – trasa zakończona";

    private readonly List<DeliveryRunOrder> _stops = [];

    private DeliveryRun(Guid id)
        : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly PlannedDate { get; private set; }

    public Guid CourierId { get; private set; }

    public string VehiclePlate { get; private set; } = string.Empty;

    public Guid StartLocationId { get; private set; }

    public Guid EndLocationId { get; private set; }

    public DeliveryRunStatus Status { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public double? OptimizedDurationSeconds { get; private set; }

    public double? OptimizedDistanceMeters { get; private set; }

    public DateTimeOffset? OptimizedAt { get; private set; }

    /// <summary>Solver that produced the stored order, or <c>Manual</c>.</summary>
    public string? OptimizationAlgorithm { get; private set; }

    public bool RouteIsOptimal { get; private set; }

    /// <summary>The plan was computed on straight-line estimates (routing provider unavailable).</summary>
    public bool RouteIsApproximate { get; private set; }

    public string? MatrixProvider { get; private set; }

    public IReadOnlyList<RunNavigationLink> NavigationLinks { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Stops ordered by delivery sequence.</summary>
    public IReadOnlyList<DeliveryRunOrder> Stops => [.. _stops.OrderBy(s => s.DeliverySequence)];

    public bool HasRoutePlan => OptimizedAt is not null;

    public static DeliveryRun Create(RunDetails details)
    {
        var run = new DeliveryRun(Guid.CreateVersion7()) { Status = DeliveryRunStatus.Draft };
        run.Apply(details);
        return run;
    }

    public Result UpdateDetails(RunDetails details)
    {
        if (Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(Status);
        }

        if (details.StartLocationId != StartLocationId || details.EndLocationId != EndLocationId)
        {
            ClearPlan();
        }

        Apply(details);
        return Result.Success();
    }

    /// <summary>Appends orders at the end of the sequence.</summary>
    public Result AddOrders(IReadOnlyCollection<Guid> orderIds)
    {
        if (Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(Status);
        }

        if (orderIds.Count == 0)
        {
            return Result.Success();
        }

        if (orderIds.Distinct().Count() != orderIds.Count || orderIds.Any(id => _stops.Any(s => s.OrderId == id)))
        {
            return DeliveryErrors.OrderAlreadyInThisRun;
        }

        if (_stops.Count + orderIds.Count > MaxStops)
        {
            return DeliveryErrors.TooManyStops(MaxStops);
        }

        var next = _stops.Count;
        foreach (var orderId in orderIds)
        {
            _stops.Add(DeliveryRunOrder.Create(Id, orderId, ++next));
        }

        ClearPlan();
        return Result.Success();
    }

    public Result RemoveOrder(Guid orderId)
    {
        if (Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(Status);
        }

        var stop = _stops.SingleOrDefault(s => s.OrderId == orderId);
        if (stop is null)
        {
            return DeliveryErrors.OrderNotInRun;
        }

        _stops.Remove(stop);
        var sequence = 0;
        foreach (var remaining in _stops.OrderBy(s => s.DeliverySequence))
        {
            remaining.MoveTo(++sequence);
        }

        ClearPlan();
        return Result.Success();
    }

    /// <summary>Stores a computed stop order (optimized or manual) with arrival estimates and navigation links.</summary>
    public Result ApplyRoute(PlannedRoute route, DateTimeOffset now)
    {
        if (Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(Status);
        }

        var planned = route.Stops.Select(s => s.OrderId).ToList();
        if (planned.Count != _stops.Count || planned.Distinct().Count() != planned.Count || planned.Any(id => _stops.All(s => s.OrderId != id)))
        {
            return DeliveryErrors.SequenceMustContainAllOrders;
        }

        for (var i = 0; i < route.Stops.Count; i++)
        {
            var plannedStop = route.Stops[i];
            _stops.Single(s => s.OrderId == plannedStop.OrderId).Plan(i + 1, plannedStop.ArrivalOffsetSeconds, plannedStop.Location);
        }

        OptimizedDurationSeconds = route.DurationSeconds;
        OptimizedDistanceMeters = route.DistanceMeters;
        OptimizationAlgorithm = route.Algorithm;
        RouteIsOptimal = route.IsOptimal;
        RouteIsApproximate = route.IsApproximate;
        MatrixProvider = route.MatrixProvider;
        NavigationLinks = [.. route.NavigationLinks];
        OptimizedAt = now;
        return Result.Success();
    }

    /// <summary><c>Draft → Planned</c>: from now on the courier sees the run.</summary>
    public Result Approve()
    {
        if (Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.InvalidTransition(Status, DeliveryRunStatus.Planned);
        }

        if (_stops.Count == 0)
        {
            return DeliveryErrors.RunHasNoStops;
        }

        if (!HasRoutePlan)
        {
            return DeliveryErrors.RouteNotPlanned;
        }

        Status = DeliveryRunStatus.Planned;
        return Result.Success();
    }

    /// <summary>Cancels a run that has not started; its orders go back to the pool (caller's job).</summary>
    public Result Cancel()
    {
        if (Status is not (DeliveryRunStatus.Draft or DeliveryRunStatus.Planned))
        {
            return DeliveryErrors.InvalidTransition(Status, DeliveryRunStatus.Cancelled);
        }

        Status = DeliveryRunStatus.Cancelled;
        return Result.Success();
    }

    /// <summary><c>Planned → InProgress</c> (the courier sets off).</summary>
    public Result Start(DateTimeOffset now)
    {
        if (Status != DeliveryRunStatus.Planned)
        {
            return DeliveryErrors.InvalidTransition(Status, DeliveryRunStatus.InProgress);
        }

        Status = DeliveryRunStatus.InProgress;
        StartedAt = now;
        return Result.Success();
    }

    /// <summary>Marks a stop delivered; the run completes automatically after its last stop.</summary>
    public Result<DeliveryRunOrder> MarkStopDelivered(Guid stopId, DateTimeOffset now) =>
        CompleteStop(stopId, now, stop => stop.MarkDelivered(now));

    /// <summary>Marks a stop failed (reason required); the run completes automatically after its last stop.</summary>
    public Result<DeliveryRunOrder> MarkStopFailed(Guid stopId, string reason, DateTimeOffset now) =>
        CompleteStop(stopId, now, stop => stop.MarkFailed(reason, now));

    /// <summary>
    /// <c>InProgress → Completed</c> before all stops are done: pending stops are marked failed with
    /// <see cref="NotVisitedReason"/>. Returns the orders of those stops.
    /// </summary>
    public Result<IReadOnlyList<Guid>> Complete(DateTimeOffset now)
    {
        if (Status != DeliveryRunStatus.InProgress)
        {
            return DeliveryErrors.InvalidTransition(Status, DeliveryRunStatus.Completed);
        }

        var notVisited = _stops.Where(s => !s.IsFinal).ToList();
        foreach (var stop in notVisited)
        {
            stop.MarkFailed(NotVisitedReason, now);
        }

        Status = DeliveryRunStatus.Completed;
        CompletedAt = now;
        return notVisited.Select(s => s.OrderId).ToList();
    }

    private Result<DeliveryRunOrder> CompleteStop(Guid stopId, DateTimeOffset now, Func<DeliveryRunOrder, Result> complete)
    {
        if (Status != DeliveryRunStatus.InProgress)
        {
            return DeliveryErrors.RunNotInProgress(Status);
        }

        var stop = _stops.SingleOrDefault(s => s.Id == stopId);
        if (stop is null)
        {
            return DeliveryErrors.StopNotFound;
        }

        var result = complete(stop);
        if (result.IsFailure)
        {
            return result.Error;
        }

        if (_stops.All(s => s.IsFinal))
        {
            Status = DeliveryRunStatus.Completed;
            CompletedAt = now;
        }

        return stop;
    }

    private void ClearPlan()
    {
        foreach (var stop in _stops)
        {
            stop.ClearPlan();
        }

        OptimizedDurationSeconds = null;
        OptimizedDistanceMeters = null;
        OptimizationAlgorithm = null;
        RouteIsOptimal = false;
        RouteIsApproximate = false;
        MatrixProvider = null;
        NavigationLinks = [];
        OptimizedAt = null;
    }

    private void Apply(RunDetails details)
    {
        Name = details.Name;
        PlannedDate = details.PlannedDate;
        CourierId = details.CourierId;
        VehiclePlate = details.VehiclePlate;
        StartLocationId = details.StartLocationId;
        EndLocationId = details.EndLocationId;
    }
}
