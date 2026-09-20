using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MobilityFinance.Web.Security;

namespace MobilityFinance.Web.Tests;

public sealed class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(
        AuthorizationPolicies.OperatorWork,
        PlatformRoles.Operator,
        PlatformRoles.PlatformAdmin)]
    [InlineData(
        AuthorizationPolicies.ReviewWork,
        PlatformRoles.Reviewer,
        PlatformRoles.PlatformAdmin)]
    [InlineData(
        AuthorizationPolicies.PlatformAdministration,
        PlatformRoles.PlatformAdmin)]
    public async Task PoliciesRequireTheExpectedRoles(
        string policyName,
        params string[] expectedRoles)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LocalAccounts:Accounts:0:Email"] = "operator@example.test",
                    ["LocalAccounts:Accounts:0:DisplayName"] = "Test Operator",
                    ["LocalAccounts:Accounts:0:Password"] = "password",
                    ["LocalAccounts:Accounts:0:Roles:0"] = PlatformRoles.Operator,
                })
            .Build();
        services.AddPlatformSecurity(configuration, useSecureCookies: false);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IAuthorizationPolicyProvider policyProvider =
            provider.GetRequiredService<IAuthorizationPolicyProvider>();
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(policyName);
        RolesAuthorizationRequirement requirement =
            Assert.IsType<RolesAuthorizationRequirement>(
                Assert.Single(policy!.Requirements));

        Assert.Equal(
            expectedRoles.Order(StringComparer.Ordinal),
            requirement.AllowedRoles.Order(StringComparer.Ordinal));
    }
}
