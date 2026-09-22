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
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "sword_iron", EquipmentSlot.Weapon,
                Rarity.Common, stats.Length > 0 ? stats : [("Attack", 10.0)], Now);

        private static EquipmentItem Artifact()
            => new(Guid.NewGuid(), Guid.NewGuid(), 1, "amulet_dawn", EquipmentSlot.Artifact,
                Rarity.Rare, [("Attack", 5.0)], Now);

        /// <summary>Усе, що міняє внесок предмета в силу героя, лишає подію для перерахунку.</summary>
        [Fact]
        public void EquipmentChanges_ShouldRaiseEquipmentChanged()
        {
            var item = Weapon();
            var hero = Guid.NewGuid();

            item.EquipTo(hero, 0, Now);
            Assert.Single(item.DomainEvents.OfType<EquipmentChanged>());
            item.ClearDomainEvents();

            item.Enhance(Now);
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
            Assert.False(item.IsWearable);
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
            Assert.True(item.IsWearable);
        }

        /// <summary>
        /// Поломка знімає зброю з героя: інакше герой бився б предметом,
        /// який нічого не дає, і втрату помітив би не одразу.
        /// </summary>
        [Fact]
        public void Break_ShouldUnequipTheWeapon()
        {
            var item = Weapon();
            item.EquipTo(Guid.NewGuid(), 0, Now);
            item.Enhance(Now);

            item.Break(Now);

            Assert.True(item.IsBroken);
            Assert.Null(item.EquippedByHeroId);
            Assert.False(item.IsWearable);

            // Рівень лишається: гравець утратив спробу, а не прогрес
            Assert.Equal(1, item.EnhancementLevel);
        }

        [Fact]
        public void EquipTo_ShouldRejectABrokenWeapon()
        {
            var item = Weapon();
            item.Break(Now);

            Assert.Throws<InvalidStateException>(() => item.EquipTo(Guid.NewGuid(), 0, Now));
        }

        [Fact]
        public void Repair_ShouldMakeTheWeaponWearableAgain()
        {
            var item = Weapon();
            item.Break(Now);

            item.Repair(Now);

            Assert.False(item.IsBroken);
            Assert.True(item.IsWearable);
        }

        [Fact]
        public void GetStatValue_ShouldScaleWithEnhancement()
        {
            var item = Weapon(("Attack", 10.0));
            item.Enhance(Now);
            item.Enhance(Now);

            Assert.Equal(12.0, item.GetStatValue("Attack", enhancementBonus: 0.1), 3);
        }

        /// <summary>Зламане не дає нічого — інакше поломку легко проґавити.</summary>
        [Fact]
        public void GetStatValue_ShouldReturnZero_WhenBroken()
        {
            var item = Weapon(("Attack", 10.0));
            item.Break(Now);

            Assert.Equal(0, item.GetStatValue("Attack", 0.1), 3);
        }

        [Fact]
        public void GetStatValue_ShouldReturnZero_ForAnUnknownStat()
            => Assert.Equal(0, Weapon().GetStatValue("Defense", 0.1), 3);

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

            Assert.Equal(7.5, item.GetStatValue("Attack", enhancementBonus: 0), 3);
        }

        [Fact]
        public void RaiseStat_ShouldThrow_ForAMissingStat()
            => Assert.Throws<EntityNotFoundException>(() => Artifact().RaiseStat("Defense", 1, Now));
    }
}
