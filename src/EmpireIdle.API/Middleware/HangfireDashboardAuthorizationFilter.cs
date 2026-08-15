using Hangfire.Dashboard;

namespace EmpireIdle.API.Middleware
{
    /// <summary>Пускає до дашборда Hangfire лише автентифікованих користувачів.</summary>
    public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
            => context.GetHttpContext().User.Identity?.IsAuthenticated == true;
    }
}
