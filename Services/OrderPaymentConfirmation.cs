using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public static class OrderPaymentConfirmation
{
    /// <summary>
    /// Marks an order as payment-confirmed and advances its status.
    /// Valid source statuses: AwaitingPayment (normal path) and ReadyForDelivery
    /// (narrow race: webhook arrives after shop has already moved the order forward).
    /// Returns false and makes no changes if the order is already confirmed or
    /// in an incompatible status.
    /// </summary>
    public static bool ApplyConfirmedPayment(LaundryOrder order, DateTime utcNow)
    {
        if (order == null)
            return false;

        if (order.IsPaymentConfirmed)
            return false;

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            order.IsPaymentConfirmed = true;
            order.PaymentConfirmedAt = utcNow;
            order.UpdatedAt = utcNow;
            order.Status = OrderStatus.PaymentConfirmed;
            return true;
        }

        // Narrow race: webhook arrives after the order was moved to ReadyForDelivery.
        // Confirm payment without changing the status (the order is already further along).
        if (order.Status == OrderStatus.ReadyForDelivery)
        {
            order.IsPaymentConfirmed = true;
            order.PaymentConfirmedAt = utcNow;
            order.UpdatedAt = utcNow;
            // Status stays ReadyForDelivery — delivery can proceed immediately.
            return true;
        }

        return false;
    }
}
