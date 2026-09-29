using LaundryHub2._0.Data;
using LaundryHub2._0.Models;
using Microsoft.EntityFrameworkCore;

namespace LaundryHub2._0.Services;

public class DuplicateOrderNumberException : Exception
{
    public DuplicateOrderNumberException() : base("Could not reserve a unique order number.") { }
}

public class OrderNumberService
{
    private const int MaxAttempts = 3;
    private readonly ApplicationDbContext _context;

    public OrderNumberService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync()
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"LH-{DateTime.UtcNow:yyyyMMdd}-";
        var seq = await ReserveDailySeqAsync(today);
        return $"{prefix}{seq:D4}";
    }

    private async Task<long> ReserveDailySeqAsync(DateTime date)
    {
        var conn = _context.Database.GetDbConnection();
        var opened = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
            opened = true;
        }
        try
        {
            using (var insert = conn.CreateCommand())
            {
                insert.CommandText = "INSERT INTO `LaundryOrderSequences` (`OrderDate`, `NextSeq`) VALUES (@d, LAST_INSERT_ID(1)) ON DUPLICATE KEY UPDATE `NextSeq` = LAST_INSERT_ID(`NextSeq` + 1)";
                var p1 = insert.CreateParameter();
                p1.ParameterName = "@d";
                p1.Value = date.Date;
                insert.Parameters.Add(p1);
                await insert.ExecuteNonQueryAsync();
            }
            using (var select = conn.CreateCommand())
            {
                select.CommandText = "SELECT LAST_INSERT_ID()";
                var result = await select.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
        }
        finally
        {
            if (opened)
                await conn.CloseAsync();
        }
    }

    public async Task<LaundryOrder> CreateOrderWithUniqueNumberAsync(Func<string, LaundryOrder> build)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                var order = build(await GenerateAsync());
                _context.LaundryOrders.Add(order);
                await _context.SaveChangesAsync();
                return order;
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                _context.ChangeTracker.Clear();
                if (++attempt >= MaxAttempts)
                    throw new DuplicateOrderNumberException();
            }
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner != null)
        {
            if (inner.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase))
                return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
