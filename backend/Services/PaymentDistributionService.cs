using Dapper;
using MechanicApp.Server.Models;

namespace MechanicApp.Server.Services
{
    /// <summary>
    /// Distributes payment amounts across multiple repair orders proportionally or evenly.
    /// </summary>
    public class PaymentDistributionService(IDbService db) : IPaymentDistributionService
    {
        /// <inheritdoc />
        public async Task DistributePaymentToOrders(int paymentId, decimal amount, int[] repairOrderIds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paymentId);
            ArgumentNullException.ThrowIfNull(repairOrderIds);

            // Batch-fetch all order balances in a single query (avoids N+1)
            var orderBalances = await db.GetAll<OrderBalanceInfo>(
                @"SELECT r.""Id"",
                  r.""TotalCost"",
                  COALESCE((SELECT SUM(pro.""Amount"") FROM mechanic_db.""PaymentRepairOrders"" pro
                            WHERE pro.""RepairOrderId"" = r.""Id""), 0) AS ""TotalPaid""
                  FROM mechanic_db.""RepairOrders"" r
                  WHERE r.""Id"" = ANY(@Ids)",
                new { Ids = repairOrderIds }).ConfigureAwait(false);

            var orderInfos = orderBalances
                .Select(o => (OrderId: o.Id, Remaining: Math.Max(0, o.TotalCost - o.TotalPaid)))
                .ToList();

            decimal totalRemaining = orderInfos.Sum(o => o.Remaining);
            decimal allocated = 0;

            await db.ExecuteInTransactionAsync(async (conn, tx) =>
            {
                for (int i = 0; i < orderInfos.Count; i++)
                {
                    decimal orderAmount;
                    if (i == orderInfos.Count - 1)
                    {
                        orderAmount = amount - allocated;
                    }
                    else
                    {
                        decimal proportion = totalRemaining > 0
                            ? orderInfos[i].Remaining / totalRemaining
                            : 1.0m / orderInfos.Count;
                        orderAmount = Math.Round(amount * proportion, 2);
                    }

                    allocated += orderAmount;

                    await conn.ExecuteAsync(
                        @"INSERT INTO mechanic_db.""PaymentRepairOrders"" (""PaymentId"", ""RepairOrderId"", ""Amount"")
                          VALUES (@PaymentId, @RepairOrderId, @Amount)",
                        new { PaymentId = paymentId, RepairOrderId = orderInfos[i].OrderId, Amount = orderAmount },
                        transaction: tx).ConfigureAwait(false);
                }
            });
        }

        /// <inheritdoc />
        public async Task RedistributePaymentEvenly(int paymentId, decimal amount, int[] repairOrderIds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paymentId);
            ArgumentNullException.ThrowIfNull(repairOrderIds);
            await db.ExecuteInTransactionAsync(async (conn, tx) =>
            {
                await conn.ExecuteAsync(
                    @"DELETE FROM mechanic_db.""PaymentRepairOrders"" WHERE ""PaymentId"" = @Id",
                    new { Id = paymentId }, transaction: tx).ConfigureAwait(false);

                decimal amountPerOrder = Math.Round(amount / repairOrderIds.Length, 2);
                decimal allocated = 0;
                for (int i = 0; i < repairOrderIds.Length; i++)
                {
                    decimal orderAmount = (i == repairOrderIds.Length - 1)
                        ? amount - allocated
                        : amountPerOrder;
                    allocated += orderAmount;

                    await conn.ExecuteAsync(
                        @"INSERT INTO mechanic_db.""PaymentRepairOrders"" (""PaymentId"", ""RepairOrderId"", ""Amount"")
                          VALUES (@PaymentId, @RepairOrderId, @Amount)",
                        new { PaymentId = paymentId, RepairOrderId = repairOrderIds[i], Amount = orderAmount },
                        transaction: tx).ConfigureAwait(false);
                }
            });
        }
    }
}
