using Microsoft.AspNetCore.Mvc.Testing;

namespace ComposeLab.Api.Tests.TestSupport;

/// <summary>
/// Boots the real HTTP pipeline. There is no database to substitute in this phase, so nothing is
/// faked: route matching, model binding, the validation filter, endpoint discovery, and Problem
/// Details all run as they do in production.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>;
