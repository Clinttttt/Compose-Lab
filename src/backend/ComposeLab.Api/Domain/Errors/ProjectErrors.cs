using System.Globalization;
using ComposeLab.Api.Domain.Common;

namespace ComposeLab.Api.Domain.Errors;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) =>
        Error.NotFound("project.not_found", $"Project {projectId} was not found.");

    /// <summary>
    /// A stored document written against a topology contract this build cannot read. Reported rather than
    /// interpreted: guessing at an unknown shape would quietly corrupt the learner's architecture, and there
    /// is no migration path yet.
    /// </summary>
    public static Error UnsupportedTopologySchema(int storedVersion, int supportedVersion)
    {
        string stored = storedVersion.ToString(CultureInfo.InvariantCulture);
        string supported = supportedVersion.ToString(CultureInfo.InvariantCulture);

        return Error.Conflict(
            "project.unsupported_topology_schema",
            $"This project was saved with topology schema version {stored}, and this version of ComposeLab "
                + $"reads version {supported}. It has not been opened, to avoid misreading the saved "
                + "architecture.");
    }
}
