namespace cpms_Domain.Models;

public class MrpPlanningRun : Base
{
    public long RunId { get; set; }
    public int ProjectId { get; set; }
    public int WarehouseId { get; set; }
    public int Version { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int CalculatedByUserId { get; set; }
    public string SnapshotJson { get; set; } = "[]";
    public string TransferRecommendationsJson { get; set; } = "[]";
    public virtual Project Project { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual UserAccount CalculatedBy { get; set; } = null!;
}
