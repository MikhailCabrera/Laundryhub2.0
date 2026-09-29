namespace LaundryHub2._0.Models;

public enum OrderStatus
{
    Pending = 0,
    RiderAssigned = 1,
    PickedUp = 2,
    InTransitToShop = 3,
    Weighing = 4,
    WeightConfirmed = 5,
    Washing = 6,
    Drying = 7,
    AwaitingPayment = 8,
    PaymentConfirmed = 9,
    ReadyForDelivery = 10,
    OutForDelivery = 11,
    Delivered = 12,
    Cancelled = 13,
    Abandoned = 14,
    DeliveryAttemptFailed = 15
}
