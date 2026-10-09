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

        private static EquipmentItem Weapon(params (string Stat, double Value)[] stats)
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "sword_iron", EquipmentSlot.Artifact,
                Rarity.Common, stats.Length > 0 ? stats : [("Attack", 10.0)], Now);

        private static EquipmentItem Artifact()
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet_dawn", EquipmentSlot.Artifact,
                Rarity.Rare, [("Attack", 5.0)], Now);

        /// <summary>Сила рахується лише з вдягнутого: подія перерахунку — тільки коли зміна торкається того, що на герої.</summary>
        [Fact]
        public void EquipmentChanges_ShouldRaiseEquipmentChanged_OnlyWhileEquipped()
        {
            var item = Weapon();
            var hero = Guid.NewGuid();

            // На складі: рівень і майстерність сили не рухають
            item.GainExperience(40, 1, Now);
            item.RaiseMastery(Now);
            Assert.Empty(item.DomainEvents.OfType<EquipmentChanged>());

            item.EquipTo(hero, 0, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.GainExperience(110, 2, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.RaiseMastery(Now);
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
            var item = Weapon();
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

            item.GainExperience(40, 1, Now);
            item.GainExperience(110, 2, Now);

            Assert.Equal((2, 150L), (item.Level, item.Experience));
        }

        /// <summary>Рівень артефакта не падає: досвід лише додається.</summary>
        [Fact]
        public void GainExperience_ShouldRejectALowerLevel_OrNegativeExperience()
        {
            var item = Artifact();
            item.GainExperience(150, 2, Now);

            Assert.Throws<ArgumentOutOfRangeException>(() => item.GainExperience(10, 1, Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => item.GainExperience(-1, 2, Now));
        }

        /// <summary>Виставлений лот у заставі: його не прокачують ні рівнем, ні майстерністю.</summary>
        [Fact]
        public void Progress_ShouldBeRefused_WhileOnTheMarket()
        {
            var item = Artifact();
            item.PutOnMarket(Now);

            Assert.Throws<InvalidStateException>(() => item.GainExperience(40, 1, Now));
            Assert.Throws<InvalidStateException>(() => item.RaiseMastery(Now));
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

        /// <summary>Бонуси рівня й майстерності складаються: 10 × (1 + 2·0.05 + 1·0.1).</summary>
        [Fact]
        public void GetStatValue_ShouldAddLevelAndMasteryBonuses()
        {
            var item = Weapon(("Attack", 10.0));
            item.GainExperience(150, 2, Now);
            item.RaiseMastery(Now);

            Assert.Equal(12.0, item.GetStatValue("Attack", levelBonus: 0.05, masteryBonus: 0.1), 3);
        }

        [Fact]
        public void GetStatValue_ShouldReturnZero_ForAnUnknownStat()
            => Assert.Equal(0, Weapon().GetStatValue("Defense", 0.05, 0.1), 3);

        [Fact]
        public void AddStat_ShouldRejectADuplicate()
        {
            var item = Artifact();

            Assert.Throws<AlreadyExistsException>(() => item.AddStat("Attack", 3, Now));
        }

        [Fact]
        public void RaiseStat_ShouldIncreaseTheValue()
        {
            var item = Artifact();

            item.RaiseStat("Attack", 2.5, Now);

            Assert.Equal(7.5, item.GetStatValue("Attack", levelBonus: 0, masteryBonus: 0), 3);
        }

        [Fact]
        public void RaiseStat_ShouldThrow_ForAMissingStat()
            => Assert.Throws<EntityNotFoundException>(() => Artifact().RaiseStat("Defense", 1, Now));
    }
}
