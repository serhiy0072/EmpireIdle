using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Clans.Services
{
    /// <summary>
    /// Хто стає лідером, коли чинний іде: сам виходить із клану чи зник із гри.
    /// Спуск рангами — зам, далі генерали, офіцери, ветерани, рядові; серед рівних —
    /// хто був у грі найпізніше. Одне правило на обидва приводи.
    /// </summary>
    public sealed class ClanSuccession
    {
        private readonly IPlayerRepository _playerRepository;

        public ClanSuccession(IPlayerRepository playerRepository) => _playerRepository = playerRepository;

        /// <param name="activeSince">
        /// Перевага тим, хто був у грі відтоді; null — лише ранг і свіжість. Якщо активних
        /// немає, береться найстарший за рангом: клан не має лишатись без лідера.
        /// </param>
        /// <returns>null — окрім лідера, в клані нікого.</returns>
        public async Task<Guid?> ChooseAsync(Clan clan, Guid outgoingLeaderId, DateTime? activeSince,
            CancellationToken cancellationToken)
        {
            var others = clan.Members.Where(m => m.PlayerId != outgoingLeaderId).ToList();

            if (others.Count == 0)
                return null;

            var presence = await _playerRepository.GetLastSeenAsync(others.Select(m => m.PlayerId).ToList(), cancellationToken);
            var rankByRole = clan.Roles.ToDictionary(r => r.Id, r => r.Rank);

            var candidates = others
                .OrderByDescending(m => rankByRole.GetValueOrDefault(m.RoleId))
                .ThenByDescending(m => presence.GetValueOrDefault(m.PlayerId))
                .ToList();

            var successor = activeSince is { } since
                ? candidates.FirstOrDefault(m => presence.GetValueOrDefault(m.PlayerId) >= since) ?? candidates[0]
                : candidates[0];

            return successor.PlayerId;
        }
    }
}
