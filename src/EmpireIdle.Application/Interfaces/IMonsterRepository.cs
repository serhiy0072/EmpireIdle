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

        /// <summary>Усі монстри світу — для перегенерації мапи в розробці.</summary>
        Task<List<Monster>> GetAllAsync(int serverId, CancellationToken cancellationToken = default);

        /// <summary>Монстри за списком ідентифікаторів — одним запитом на всю ділянку мапи.</summary>
        Task<List<Monster>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Найближчий до точки монстр заданого типу — ціль для приручення зі звіринця.
        /// Рівна відстань — слабший перший; null — такого типу у світі зараз немає.
        /// </summary>
        Task<Monster?> GetNearestAsync(int serverId, string type, int x, int y, CancellationToken cancellationToken = default);

        /// <summary>Додати монстра.</summary>
        Task AddAsync(Monster monster, CancellationToken cancellationToken = default);

        /// <summary>Прибрати монстра (вбитий).</summary>
        void Remove(Monster monster);
    }
}