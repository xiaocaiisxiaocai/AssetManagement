using AssetManagement.Application.Common;

namespace AssetManagement.Application.Reports;

public record AssetSummaryDto
{
    public int Total { get; init; }
    public int Available { get; init; }
    public int Borrowed { get; init; }
    public int Maintenance { get; init; }
    public int Scrapped { get; init; }
    public decimal TotalValue { get; init; }
    public List<CategoryStatRow> ByCategory { get; init; } = new();
    public List<DeptStatRow> ByDept { get; init; } = new();
}

public record CategoryStatRow
{
    public int CategoryId { get; init; }
    public string CategoryCode { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int Total { get; init; }
    public int Available { get; init; }
    public int Borrowed { get; init; }
    public decimal TotalValue { get; init; }
    public decimal Percent { get; init; }
}

public record DeptStatRow
{
    public int DepartmentId { get; init; }
    public string DepartmentCode { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public int Total { get; init; }
    public int Available { get; init; }
    public int Borrowed { get; init; }
    public decimal TotalValue { get; init; }
    public decimal Percent { get; init; }
}

public record BorrowReportQuery
{
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? CategoryId { get; init; }
    public int? BorrowerId { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record BorrowReportRow
{
    public int FlowId { get; init; }
    public string FlowNo { get; init; } = "";
    public int AssetId { get; init; }
    public string AssetNo { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int BorrowerId { get; init; }
    public string Borrower { get; init; } = "";
    public string? BorrowerDept { get; init; }
    public string? ReturnDate { get; init; }
    public DateTime ApplyTime { get; init; }
    public string Status { get; init; } = "";
    public decimal Amount { get; init; }
}

public record OverdueReportRow
{
    public int FlowId { get; init; }
    public int AssetId { get; init; }
    public string AssetNo { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public int BorrowerId { get; init; }
    public string Borrower { get; init; } = "";
    public string? BorrowerDept { get; init; }
    public string ReturnDate { get; init; } = "";
    public int OverdueDays { get; init; }
    public bool IsSerious { get; init; }
}

public interface IReportService
{
    Task<AssetSummaryDto> GetSummaryAsync();
    Task<byte[]> ExportSummaryAsync();
    Task<PagedResult<BorrowReportRow>> QueryBorrowedAsync(BorrowReportQuery query);
    Task<byte[]> ExportBorrowedAsync(BorrowReportQuery query);
    Task<List<OverdueReportRow>> QueryOverdueAsync();
    Task<byte[]> ExportOverdueAsync();
    Task RemindOverdueAsync(int assetId, int? userId);
    Task<int> RemindOverdueBatchAsync(IReadOnlyCollection<int> assetIds, int? userId);
}
