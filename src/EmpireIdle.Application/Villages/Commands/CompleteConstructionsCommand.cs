
using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    public record CompleteConstructionsCommand : IRequest;

    public class CompleteConstructionsCommandHandler : IRequestHandler<CompleteConstructionsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CompleteConstructionsCommandHandler> _logger;

        public CompleteConstructionsCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<CompleteConstructionsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Handle(CompleteConstructionsCommand request, CancellationToken cancellationToken)
        {
            var villages = await _villageRepository.GetAllAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var completed = 0;
            foreach (var village in villages)
                completed += village.CompleteDueConstructions(now);

            if (completed > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Completed {count} constructions", completed);
            }
        }
    }
}
