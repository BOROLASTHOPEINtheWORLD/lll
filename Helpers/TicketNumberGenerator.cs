using labsupport.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace labsupport.Helpers
{
    public static class TicketNumberHelper
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task<string> GenerateTicketNumberAsync(LabsupportContext context)
        {
            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.Now;
                var today = now.Date; // Используем только дату без времени

                // Получаем максимальный номер за сегодня
                var lastTicket = await context.Tickets
                    .Where(t => t.CreatedAt.HasValue && t.CreatedAt.Value.Date == today)
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefaultAsync();

                int sequence = 1;
                if (lastTicket != null && !string.IsNullOrEmpty(lastTicket.TicketNumber))
                {
                    var parts = lastTicket.TicketNumber.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out var lastSeq))
                    {
                        sequence = lastSeq + 1;
                    }
                }

                return $"LAB-{now:yyyyMMdd}-{sequence:D4}";
            

            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}