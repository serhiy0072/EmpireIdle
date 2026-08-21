using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>Знаходить походи, що дозріли, і обробляє кожен в окремому scope.</summary>
    public record CompleteDueMarchesCommand : IRequest;

    public sealed class CompleteDueMarchesCommandHandler : IRequestHandler<CompleteDueMarchesCommand>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly ILogger<CompleteDueMarchesCommandHandler> _logger;

        public CompleteDueMarchesCommandHandler(
            IMarchRepository marchRepository,
            IServiceScopeFactory scopeFactory,
            IServerContext serverContext,
            GameCatalog catalog,
            ILogger<CompleteDueMarchesCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _scopeFactory = scopeFactory;
            _serverContext = serverContext;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(CompleteDueMarchesCommand request, CancellationToken cancellationToken)
        {
            var due = await _marchRepository.GetDueAsync(DateTime.UtcNow, _catalog.Config.ScanBatchSize, cancellationToken);

            if (due.Count == 0)
                return;

            var processed = 0;
            var failed = 0;

            foreach (var march in due)
            {
                // Свій scope = свій DbContext. Збій на одному фізично не може
                // зачепити інші — на відміну від спільного ChangeTracker.
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(_serverContext.ServerId);
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                try
                {
                    await mediator.Send(new CompleteMarchCommand(march.Id), cancellationToken);
                    processed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Failed to process march {MarchId}", march.Id);
                }
            }

            _logger.LogInformation("Marches processed: {Processed}, failed: {Failed}", processed, failed);
        }
    }
}
