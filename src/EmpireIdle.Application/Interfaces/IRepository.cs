using EmpireIdle.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Базовий інтерфейс репозиторію для всіх агрегатів.
    /// </summary>
    public interface IRepository<T> where T : Entity
    {
        /// <summary>Знайти за ідентифікатором.</summary>
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Додати новий запис.</summary>
        Task AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>Оновити існуючий запис.</summary>
        void Update(T entity);

        /// <summary>Видалити запис.</summary>
        void Delete(T entity);
    }
}
