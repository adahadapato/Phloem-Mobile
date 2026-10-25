namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>A line of the "what can be sent" list. Same names as the API's <c>ParcelItemRuleKind</c>.</summary>
public enum ParcelItemRuleKind
{
    /// <summary>Never carried (e.g. weapons, cash).</summary>
    NotAllowed = 1,

    /// <summary>Carried only as the line's detail says (e.g. "sealed, 1 litre or less").</summary>
    Restricted = 2
}
