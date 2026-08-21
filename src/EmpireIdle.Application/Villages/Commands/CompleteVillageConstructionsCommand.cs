using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Завершує дозрілі будівництва одного села. Одиниця роботи сканера:
    /// конфлікт паралелізму коштує це село, а не весь прогін.
    /// </summary>
    public record CompleteVillageConstructionsCommand(Guid VillageId) : IRequest;

    public sealed class CompleteVillageConstructionsCommandHandler : IRequestHandler<CompleteVillageConstructionsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CompleteVillageConstructionsCommandHandler> _logger;

        public CompleteVillageConstructionsCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<CompleteVillageConstructionsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CompleteVillageConstructionsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Село могло зникнути між читанням id і обробкою — не помилка сканера
            var village = await _villageRepository.GetByIdAsync(request.VillageId, cancellationToken);
            if (village is null)
                return;

            var completed = village.CompleteDueConstructions(now, _catalog.Buildings);
            if (completed == 0)
                return;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Completed {Count} constructions in village {VillageId}", completed, village.Id);
        }
    }
}
