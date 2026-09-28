using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Delivery.Domain.Runs;

/// <summary>
/// A stop of a delivery run: one order at a position in the delivery sequence (1-based).
/// Coordinates and the estimated arrival are those the stored route was planned with.
/// </summary>
internal sealed class DeliveryRunOrder : Entity, IAuditable
{
    private DeliveryRunOrder(Guid id, Guid deliveryRunId, Guid orderId, int deliverySequence)
        : base(id)
    {
        DeliveryRunId = deliveryRunId;
        OrderId = orderId;
        DeliverySequence = deliverySequence;
        Status = DeliveryStopStatus.Pending;
    }

    public Guid DeliveryRunId { get; private set; }

    public Guid OrderId { get; private set; }

    public int DeliverySequence { get; private set; }

    public DeliveryStopStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    /// <summary>Estimated time from the start of the run to arrival at this stop.</summary>
    public double? EstimatedArrivalOffsetSeconds { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public GeoPoint? PlannedLocation => Latitude is { } lat && Longitude is { } lng ? new GeoPoint(lat, lng) : null;

    public bool IsFinal => Status != DeliveryStopStatus.Pending;

    internal static DeliveryRunOrder Create(Guid deliveryRunId, Guid orderId, int deliverySequence) =>
        new(Guid.CreateVersion7(), deliveryRunId, orderId, deliverySequence);

    internal void MoveTo(int deliverySequence) => DeliverySequence = deliverySequence;

    internal void Plan(int deliverySequence, double arrivalOffsetSeconds, GeoPoint location)
    {
        DeliverySequence = deliverySequence;
        EstimatedArrivalOffsetSeconds = arrivalOffsetSeconds;
        Latitude = location.Latitude;
        Longitude = location.Longitude;
    }

    internal void ClearPlan()
    {
        EstimatedArrivalOffsetSeconds = null;
        Latitude = null;
        Longitude = null;
    }

    internal Result MarkDelivered(DateTimeOffset now)
    {
        if (IsFinal)
        {
            return DeliveryErrors.StopAlreadyCompleted(Status);
        }

        Status = DeliveryStopStatus.Delivered;
        DeliveredAt = now;
        return Result.Success();
    }

    internal Result MarkFailed(string reason, DateTimeOffset now)
    {
        if (IsFinal)
        {
            return DeliveryErrors.StopAlreadyCompleted(Status);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeliveryErrors.FailureReasonRequired;
        }

        Status = DeliveryStopStatus.Failed;
        FailureReason = reason.Trim();
        FailedAt = now;
        return Result.Success();
    }
}
