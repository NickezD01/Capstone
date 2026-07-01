using AutoMapper;
using cpms_Application.Request.Category;
using cpms_Application.Request.Material;
using cpms_Application.Request.Project;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.User;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response.Category;
using cpms_Application.Response.Inventory; // 🚀 Nạp thêm namespace của InventoryReportResponse
using cpms_Application.Response.Project;
using cpms_Application.Response.UserAccount;
using cpms_Application.Response.Warehouse;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.MyMapper
{
    public class MapperConfigurationsProfile : Profile
    {
        public MapperConfigurationsProfile()
        {
            // ==========================================
            // USER ACCOUNT MAPPING
            // ==========================================
            CreateMap<UpdateUserRoleRequest, UserAccount>();
            CreateMap<UpdateUserRequest, UserAccount>();
            CreateMap<UserAccount, UserProfileResponse>();
            CreateMap<UserAccount, AccountResponse>();

            // ==========================================
            // PROJECT MAPPING
            // ==========================================
            CreateMap<CreateProjectRequest, Project>();
            CreateMap<Project, ProjectResponse>();

            // ==========================================
            // PURCHASING & SUPPLIER MAPPING
            // ==========================================
            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, OrderLineItem>();
            CreateMap<CreateMaterialRequest, Material>();
            // 1. Cần thiết cho hàm UpdateMaterialAsync (Map đè dữ liệu thay đổi vào thực thể gốc)
            CreateMap<UpdateMaterialRequest, Material>();

            // 2. Nếu bạn dùng AutoMapper cho hàm lấy toàn bộ kho (WarehouseFullResponse) thay vì .Select thủ công
            CreateMap<Warehouse, WarehouseResponse>()
                .ForMember(dest => dest.ManagerName, opt => opt.MapFrom(src => src.Manager != null ? src.Manager.LastName : null));

            CreateMap<InventoryRecord, InventoryRecordDto>();
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.TotalMaterials, opt => opt.MapFrom(src => src.Materials != null ? src.Materials.Count : 0));
            CreateMap<CreateSupplierRequest, Supplier>();
            CreateMap<CreateCatalogRequest, SupplierCatalog>();

            // ==========================================
            // WAREHOUSE & INVENTORY RECORD MAPPING (Cập nhật chuẩn ERD)
            // ==========================================
            CreateMap<CreateWarehouseRequest, Warehouse>();

            // 🚀 Cấu hình ánh xạ từ thực thể InventoryRecord sang DTO báo cáo
            // Tự động bóc tách thông tin liên kết từ Warehouse và Material, tính toán lượng hàng khả dụng
            CreateMap<InventoryRecord, InventoryReportResponse>()
                .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.WarehouseName))
                .ForMember(dest => dest.MaterialName, opt => opt.MapFrom(src => src.Material.MaterialName))
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Material.Unit))
                .ForMember(dest => dest.AvailableQuantity, opt => opt.MapFrom(src => src.QuantityOnHand - src.ReservedQuantity))
                .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => (src.QuantityOnHand - src.ReservedQuantity) <= src.ReorderLevel));
        }
    }
}