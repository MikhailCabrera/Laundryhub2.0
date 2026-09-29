using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public static class OrderPaymentConfirmation
{
    public static void ApplyConfirmedPayment(LaundryOrder order, DateTime utcNow)
    {
        order.IsPaymentConfirmed = true;
        order.PaymentConfirmedAt = utcNow;
        order.UpdatedAt = utcNow;

        if (order.Status == OrderStatus.AwaitingPayment || order.ProcessingCompletedAt.HasValue)
        {
            order.Status = OrderStatus.ReadyForDelivery;
            if (!order.ReadyForDeliveryNotifiedAt.HasValue)
            {
                order.ReadyForDeliveryNotifiedAt = utcNow;
            }
        }
        else
        {
            order.Status = OrderStatus.PaymentConfirmed;
        }
    }
}
