using Xunit.Abstractions;
using Xunit.Sdk;

namespace DiscordBot.E2E;

/// <summary>
/// Orders test cases by method name (ordinal). <see cref="BrowserTests"/> names its methods
/// Test_A_.../Test_B_.../Test_C_.../Test_D_... specifically so this makes them run login, then
/// the smoke page, then the nested route, then the unauthenticated redirect - xUnit's default
/// discovery order is unspecified, and these tests share one host/browser session so the order
/// matters (plan §6 "Testing strategy": "Browser ... one happy path per migrated cluster").
/// </summary>
public sealed class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.Ordinal);
}
