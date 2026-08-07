using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій монстрів.</summary>
    public interface IMonsterRepository
    {
        /// <summary>Скільки монстрів зараз на сервері.</summary>
        Task<int> CountAsync(int serverId, CancellationToken cancellationToken = default);

        /// <summary>Знайти монстра за ідентифікатором.</summary>
        Task<Monster?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Додати монстра.</summary>
        Task AddAsync(Monster monster, CancellationToken cancellationToken = default);

        /// <summary>Прибрати монстра (вбитий).</summary>
        void Remove(Monster monster);
    }
}