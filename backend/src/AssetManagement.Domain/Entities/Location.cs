namespace AssetManagement.Domain.Entities;

public class Location
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string? QrCode { get; set; }
}
