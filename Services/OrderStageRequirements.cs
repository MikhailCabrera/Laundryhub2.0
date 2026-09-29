using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public static class OrderStageRequirements
{
    public static bool RequiresWashing(LaundryOrder order) =>
        order.OrderServices.Any(os => os.Service?.RequiresWashing ?? true);

    public static bool RequiresDrying(LaundryOrder order) =>
        order.OrderServices.Any(os => os.Service?.RequiresDrying ?? true);
}
