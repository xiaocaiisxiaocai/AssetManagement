namespace AssetManagement.Application.Auth;

public record RouteDto
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Component { get; init; } = "";
    public RouteMetaDto Meta { get; init; } = new();
    public List<RouteDto> Children { get; init; } = new();
}

public record RouteMetaDto
{
    public string Title { get; init; } = "";
    public string? Icon { get; init; }
    public int Order { get; init; }
    public string[]? Permissions { get; init; }
}

