using FluentValidation;

namespace ComposeLab.Api.Features.Topology.ParseCompose;

/// <summary>
/// Bounds the size of the document. Everything else about the text — including it being empty or not
/// valid YAML — is a parse finding, because those are things the learner needs explained rather than
/// rejected.
/// </summary>
internal sealed class Validator : AbstractValidator<Query>
{
    private const int MaxCharacters = 256 * 1024;

    public Validator() =>
        RuleFor(query => query.Yaml)
            .MaximumLength(MaxCharacters)
            .WithMessage($"A Compose file may be at most {MaxCharacters} characters.");
}
