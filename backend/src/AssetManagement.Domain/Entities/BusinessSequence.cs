namespace AssetManagement.Domain.Entities;

/// <summary>跨进程安全的业务编号序列；NextValue 表示下一次可分配的流水号。</summary>
public class BusinessSequence
{
    public string SequenceKey { get; set; } = "";
    public int NextValue { get; set; }
}
