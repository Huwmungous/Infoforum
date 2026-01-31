using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace IFGlobal.Logging;

public record LogSearchRequest
{
    [JsonPropertyName("filters")] public List<FilterCondition>? Filters { get; init; }
    [JsonPropertyName("filter_logic")] public LogicalOperator FilterLogic { get; init; } = LogicalOperator.And;
    [JsonPropertyName("created_after")] public DateTime? CreatedAfter { get; init; }
    [JsonPropertyName("created_before")] public DateTime? CreatedBefore { get; init; }
    [JsonPropertyName("limit")] public int Limit { get; init; } = 1000;
    [JsonPropertyName("offset")] public int Offset { get; init; } = 0;
    [JsonPropertyName("order_by")] public string OrderBy { get; init; } = "created_at";
    [JsonPropertyName("order_direction")] public string OrderDirection { get; init; } = "DESC";

    /// <summary>
    /// Cursor-based pagination: fetch logs relative to this idx.
    /// If null, uses offset-based pagination.
    /// </summary>
    [JsonPropertyName("from_idx")] public int? FromIdx { get; init; }

    /// <summary>
    /// Direction for cursor-based pagination.
    /// 1 = backwards (older logs, idx &lt; fromIdx), 0 = forwards (newer logs, idx &gt; fromIdx).
    /// Only used when FromIdx is specified.
    /// </summary>
    [JsonPropertyName("back")] public int Back { get; init; } = 1;
}

public record AdvancedLogSearchRequest
{
    public List<FilterGroup>? FilterGroups { get; init; }
    public LogicalOperator GroupLogic { get; init; } = LogicalOperator.And;
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; } = 0;
    public string OrderBy { get; init; } = "created_at";
    public string OrderDirection { get; init; } = "DESC";
}

public record FilterGroup
{
    public List<FilterCondition> Conditions { get; init; } = new();
    public LogicalOperator Logic { get; init; } = LogicalOperator.And;
}

public record FilterCondition
{
    [JsonPropertyName("field")] public string Field { get; init; } = string.Empty;
    [JsonPropertyName("operator")] public FilterOperator Operator { get; init; } = FilterOperator.Equals;
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("value_to")] public string? ValueTo { get; init; } // For range queries
    [JsonPropertyName("values")] public List<string>? Values { get; init; } // For IN/NOT IN queries
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterOperator
{
    [EnumMember(Value = "Equals")] Equals,
    [EnumMember(Value = "NotEquals")] NotEquals,
    [EnumMember(Value = "Contains")] Contains,
    [EnumMember(Value = "NotContains")] NotContains,
    [EnumMember(Value = "StartsWith")] StartsWith,
    [EnumMember(Value = "EndsWith")] EndsWith,
    [EnumMember(Value = "GreaterThan")] GreaterThan,
    [EnumMember(Value = "GreaterThanOrEqual")] GreaterThanOrEqual,
    [EnumMember(Value = "LessThan")] LessThan,
    [EnumMember(Value = "LessThanOrEqual")] LessThanOrEqual,
    [EnumMember(Value = "In")] In,
    [EnumMember(Value = "NotIn")] NotIn,
    [EnumMember(Value = "Between")] Between,
    [EnumMember(Value = "IsNull")] IsNull,
    [EnumMember(Value = "IsNotNull")] IsNotNull
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LogicalOperator
{
    [EnumMember(Value = "And")] And,
    [EnumMember(Value = "Or")] Or
}