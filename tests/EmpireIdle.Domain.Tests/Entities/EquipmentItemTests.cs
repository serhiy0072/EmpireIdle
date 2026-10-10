using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities
{
    /// <summary>Життєвий цикл екземпляра спорядження.</summary>
    public class EquipmentItemTests
    {
        private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private static EquipmentItem Artifact()
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet_dawn", EquipmentSlot.Artifact, Rarity.Rare, Now);

        /// <summary>Новий артефакт — без бонусів: вони з'являються лише із заточкою.</summary>
        [Fact]
        public void NewArtifact_ShouldHaveNoBonuses()
        {
            var item = Artifact();

            Assert.Empty(item.Stats);
            Assert.Equal((0, 0), (item.Level, item.Mastery));
        }

        /// <summary>Сила рахується лише з вдягнутого: подія перерахунку — тільки коли зміна торкається того, що на герої.</summary>
        [Fact]
        public void EquipmentChanges_ShouldRaiseEquipmentChanged_OnlyWhileEquipped()
        {
            var item = Artifact();
            var hero = Guid.NewGuid();

            // На складі: рівень і заточка сили не рухають
            item.GainExperience(100, 1, Now);
            item.ApplyMasteryRank(0, "Attack", 5, Now);
            Assert.Empty(item.DomainEvents.OfType<EquipmentChanged>());

            item.EquipTo(hero, 0, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.GainExperience(100, 2, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.ApplyMasteryRank(1, "UnitAttack", 5, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.Unequip(Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
        }

        [Fact]
        public void EquipTo_ShouldRememberTheSlotIndex()
        {
            var item = Artifact();
            var hero = Guid.NewGuid();

            item.EquipTo(hero, slotIndex: 2, Now);

            Assert.Equal(hero, item.EquippedByHeroId);
            Assert.Equal(2, item.SlotIndex);
        }

        [Fact]
        public void EquipTo_ShouldRejectAnAlreadyEquippedItem()
        {
            var item = Artifact();
            item.EquipTo(Guid.NewGuid(), 0, Now);

            Assert.Throws<InvalidStateException>(() => item.EquipTo(Guid.NewGuid(), 0, Now));
        }

        [Fact]
        public void Unequip_ShouldReleaseTheSlot()
        {
            var item = Artifact();
            item.EquipTo(Guid.NewGuid(), 3, Now);

            item.Unequip(Now);

            Assert.Null(item.EquippedByHeroId);
            Assert.Equal(0, item.SlotIndex);
        }

        [Fact]
        public void GainExperience_ShouldAccumulate_AndSetTheLevel()
        {
            var item = Artifact();

            item.GainExperience(100, 1, Now);
            item.GainExperience(110, 2, Now);

            Assert.Equal((2, 210L), (item.Level, item.Experience));
        }

        /// <summary>Рівень артефакта не падає: досвід лише додається.</summary>
        [Fact]
        public void GainExperience_ShouldRejectALowerLevel_OrNegativeExperience()
        {
            var item = Artifact();
            item.GainExperience(210, 2, Now);

            Assert.Throws<ArgumentOutOfRangeException>(() => item.GainExperience(10, 1, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => item.GainExperience(-1, 2, Now));
        }

        /// <summary>Перший ранг позиції створює бонус, наступний на тій самій позиції — підсилює його.</summary>
        [Fact]
        public void ApplyMasteryRank_ShouldCreateThenRaiseTheBonusOfAPosition()
        {
            var item = Artifact();

            item.ApplyMasteryRank(0, "Attack", 5, Now);
            item.ApplyMasteryRank(1, "UnitAttack", 10, Now);
            item.ApplyMasteryRank(0, "Attack", 15, Now);

            var attack = Assert.Single(item.Stats, s => s.Position == 0);
            Assert.Equal(("Attack", 20.0), (attack.StatKey, attack.Value));
            Assert.Equal(10.0, Assert.Single(item.Stats, s => s.Position == 1).Value, 3);
            Assert.Equal(3, item.Mastery);
        }

        /// <summary>Позиція закріплена за статом: інший стат туди не ляже — це ознака зламаного ролера.</summary>
        [Fact]
        public void ApplyMasteryRank_ShouldRejectAnotherStatOnATakenPosition()
        {
            var item = Artifact();
            item.ApplyMasteryRank(2, "CritChance", 1, Now);

            Assert.Throws<InvalidStateException>(() => item.ApplyMasteryRank(2, "AttackSpeed", 2, Now));
            Assert.Equal(1, item.Mastery);
        }

        /// <summary>Два випадкові бонуси без повтору: той самий стат на другій позиції — відмова.</summary>
        [Fact]
        public void ApplyMasteryRank_ShouldRejectTheSameStatOnAnotherPosition()
        {
            var item = Artifact();
            item.ApplyMasteryRank(2, "CritChance", 1, Now);

            Assert.Throws<AlreadyExistsException>(() => item.ApplyMasteryRank(3, "CritChance", 1, Now));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void ApplyMasteryRank_ShouldRejectAPositionOutsideTheFour(int position)
            => Assert.Throws<ArgumentOutOfRangeException>(() => Artifact().ApplyMasteryRank(position, "Attack", 5, Now));

        /// <summary>Виставлений лот у заставі: його не прокачують ні рівнем, ні заточкою.</summary>
        [Fact]
        public void Progress_ShouldBeRefused_WhileOnTheMarket()
        {
            var item = Artifact();
            item.PutOnMarket(Now);

            Assert.Throws<InvalidStateException>(() => item.GainExperience(100, 1, Now));
            Assert.Throws<InvalidStateException>(() => item.ApplyMasteryRank(0, "Attack", 5, Now));
        }

        /// <summary>Вдягнене не згодовується: гравець не має випадково роздягти героя.</summary>
        [Fact]
        public void EnsureCanBeFed_ShouldRefuseAnEquippedItem()
        {
            var item = Artifact();
            item.EquipTo(Guid.NewGuid(), 0, Now);

            var refusal = Assert.Throws<InvalidStateException>(item.EnsureCanBeFed);
            Assert.Equal(RefusalReasons.EquipmentFoodEquipped.Key, refusal.Reason);
        }

        [Fact]
        public void EnsureCanBeFed_ShouldAllowAFreeItem()
            => Artifact().EnsureCanBeFed();
    }
}
