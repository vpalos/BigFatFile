using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace BigFatFile.Server.Api.Parameters;

/// <summary>The query parameters of every listing: <c>cursor</c> and <c>limit</c>.</summary>
internal sealed class PageQuery
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    [FromQuery(Name = "cursor")]
    [Description("The nextCursor of the previous page. Leave it out for the first page.")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    [Range(1, MaxLimit)]
    [Description("The most items to return. The default is 100.")]
    public int? Limit { get; init; }
}
