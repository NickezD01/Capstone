using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.MaterialRequest
{
    public class MRPCalculationResponse
    {
        public int VariantId { get; set; }
        public int? WarehouseId { get; set; }
        public string InventoryScope { get; set; } = "ALL_WAREHOUSES";
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string Unit { get; set; } = null!; 
        public decimal TotalGrossRequired { get; set; }
        public decimal IssuedToProjectTasks { get; set; }
        public decimal RemainingGrossRequired { get; set; }
        public decimal CurrentInventory { get; set; }   
        public decimal ReservedQuantity { get; set; }  
        public decimal AvailableQuantity { get; set; }  
        public decimal OnOrderQuantity { get; set; }    
        public decimal NetQuantityRequired { get; set; }
        public DateTime EarliestStartDate { get; set; }
        public long PlanningRunId { get; set; }
        public int PlanningVersion { get; set; }
        public List<MRPTransferRecommendation> TransferRecommendations { get; set; } = new();
    }

    public class MRPTransferRecommendation
    {
        public int SourceWarehouseId { get; set; }
        public int DestinationWarehouseId { get; set; }
        public int VariantId { get; set; }
        public decimal SuggestedQuantity { get; set; }
    }
}
