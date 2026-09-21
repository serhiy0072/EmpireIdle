using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Tutorial.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Tutorial.Queries
{
    /// <summary>Прогрес навчання гравця. Без рядка в БД — порожній прогрес, а не 404: новачок ще нічого не бачив.</summary>
    public record GetTutorialProgressQuery(Guid PlayerId) : IRequest<TutorialProgressView>, IPlayerScopedRequest;

    public sealed class GetTutorialProgressQueryHandler : IRequestHandler<GetTutorialProgressQuery, TutorialProgressView>
    {
        private readonly ITutorialProgressRepository _repository;

        public GetTutorialProgressQueryHandler(ITutorialProgressRepository repository) => _repository = repository;

        public async Task<TutorialProgressView> Handle(GetTutorialProgressQuery request, CancellationToken cancellationToken)
        {
            var progress = await _repository.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken);

            return progress is null
                ? new TutorialProgressView([], null)
                : new TutorialProgressView(progress.SeenSteps, progress.SkippedAt);
        }
    }
}
