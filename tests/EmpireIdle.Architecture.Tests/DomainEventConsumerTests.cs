using AwesomeAssertions;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Events;
using MediatR;
using System.Reflection;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Кожна доменна подія лягає рядком в outbox і проходить його транзакцію. Подія, яку ніхто
/// не слухає, — чисте сміття: запис, обробка й очищення заради нічого.
/// Трекер квестів підписаний на все підряд, тож рахується лише справжній мапер для події.
/// </summary>
public class DomainEventConsumerTests
{
    private static readonly Assembly DomainAssembly = typeof(IDomainEvent).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IRepository<>).Assembly;

    [Fact]
    public void EveryDomainEvent_ShouldHaveAConsumer()
    {
        var applicationTypes = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToList();

        // Закриті INotificationHandler<DomainEventNotification<T>>, окрім загального трекера квестів
        var handled = applicationTypes
            .Where(t => !t.IsGenericTypeDefinition)
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(DomainEventNotification<>))
            .Select(n => n.GetGenericArguments()[0]);

        // Квест-мапери: QuestSignalMapper<T> по ланцюжку базових класів
        var mapped = applicationTypes
            .Select(MappedEvent)
            .OfType<Type>();

        var consumed = handled.Concat(mapped).ToHashSet();

        var orphans = DomainAssembly.GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Where(t => !consumed.Contains(t))
            .Select(t => t.Name)
            .ToList();

        orphans.Should().BeEmpty("подію без споживача не варто піднімати — вона лише засмічує outbox");
    }

    private static Type? MappedEvent(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(QuestSignalMapper<>))
                return current.GetGenericArguments()[0];
        }

        return null;
    }
}
