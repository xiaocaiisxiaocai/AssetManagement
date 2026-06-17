using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Assets;

public record AssetDto
{
    public int Id { get; init; }
    public string AssetNo { get; init; } = "";
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public string CategoryCode { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? LocationId { get; init; }
    public string? LocationName { get; init; }
    public int? CustodianId { get; init; }
    public string? CustodianName { get; init; }
    public string? Model { get; init; }
    public string? Brand { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public AssetStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<string> Images { get; init; } = new();
}

public record AssetQuery
{
    public string? AssetNo { get; init; }
    public string? Name { get; init; }
    public int? CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    public AssetStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record CreateAssetRequest
{
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    public int? LocationId { get; init; }
    public int? CustodianId { get; init; }
    public string? Model { get; init; }
    public string? Brand { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; } = 1;
    public List<string>? Images { get; init; }
}

public record UpdateAssetRequest
{
    public string Name { get; init; } = "";
    public int CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    public int? LocationId { get; init; }
    public int? CustodianId { get; init; }
    public string? Model { get; init; }
    public string? Brand { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; } = 1;
    public AssetStatus Status { get; init; } = AssetStatus.Available;
    public List<string>? Images { get; init; }
}

public record ImportPreviewRow
{
    public int Row { get; init; }
    public string Name { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public string? Model { get; init; }
    public string? Brand { get; init; }
    public decimal Price { get; init; }
    public bool IsValid { get; init; }
    public string Error { get; init; } = "";
}

public record ImportConfirmResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<ImportPreviewRow> Rows { get; init; } = new();
}
