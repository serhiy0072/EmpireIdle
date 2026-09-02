using AwesomeAssertions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NetArchTest.Rules;
using System.Reflection;

namespace EmpireIdle.Architecture.Tests;

/// <summary>
/// Правила напрямку залежностей. Ламаються тихо — рефакторинг чи новий using
/// не дають жодного сигналу, доки хтось не відкриє граф проєкту вручну.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Village).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IRepository<>).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EmpireIdle.Application",
                "EmpireIdle.Infrastructure",
                "EmpireIdle.API")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "домен — центр залежностей і не знає ні про кого");
    }

    [Fact]
    public void Domain_ShouldNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "персистентність не має протікати в модель");
    }

    [Fact]
    public void Domain_ShouldNotDependOnMediatR()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("MediatR")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "доменні події — власний IDomainEvent, не INotification");
    }

    [Fact]
    public void Domain_ShouldNotDependOnAspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.Extensions.Hosting")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("EmpireIdle.Infrastructure", "EmpireIdle.API")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "залежність іде всередину: Infrastructure знає Application, не навпаки");
    }

    [Fact]
    public void Application_ShouldNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "хендлер працює через IRepository/IUnitOfWork, а не через DbContext");
    }

    [Fact]
    public void Application_ShouldNotDependOnAspNetCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "HttpContext, IActionResult і статус-коди лишаються в API");
    }

    [Fact]
    public void Application_ShouldNotDependOnStripe()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Stripe")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "платіжний провайдер ховається за IPaymentProvider — інакше зміна вендора " +
            "переписує бізнес-логіку");
    }

    [Fact]
    public void Api_ShouldNotDependOnStripe()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("Stripe")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "коміт cdd5ec2 виніс типи Stripe з API — тест тримає цю межу");
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("EmpireIdle.API")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
