namespace EmpireIdle.Application.Common.Events
{
    /// <summary>
    /// Відкладає realtime-пуші до коміту. Обробники подій працюють у транзакції outbox:
    /// пуш, надісланий одразу, долетів би до клієнта навіть тоді, коли транзакція потім
    /// відкотиться, а повтор повідомлення надіслав би його вдруге.
    /// Поза відкладанням (звичайний запит) пуш іде одразу.
    /// </summary>
    public sealed class NotificationBuffer
    {
        private List<Func<Task>>? _pending;

        /// <summary>Чи збираються пуші зараз у чергу.</summary>
        public bool IsDeferring => _pending is not null;

        /// <summary>Надсилає одразу або ставить у чергу, якщо триває відкладання.</summary>
        public Task SendAsync(Func<Task> send)
        {
            if (_pending is null)
                return send();

            _pending.Add(send);
            return Task.CompletedTask;
        }

        /// <summary>Починає збирати пуші — до FlushAsync або Discard.</summary>
        public void Defer() => _pending ??= [];

        /// <summary>
        /// Транзакція закомічена — надсилає зібране по черзі. Збій одного пушу не скасовує
        /// решту: стан уже збережено, клієнт дочитає його запитом.
        /// </summary>
        /// <returns>Помилки пушів, якщо були, — для логу викликача.</returns>
        public async Task<IReadOnlyList<Exception>> FlushAsync()
        {
            var pending = _pending ?? [];
            _pending = null;

            var failures = new List<Exception>();

            foreach (var send in pending)
            {
                try
                {
                    await send();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }

            return failures;
        }

        /// <summary>Транзакція відкотилась — зібране не надсилається.</summary>
        public void Discard() => _pending = null;
    }
}
