using AutoMapper;
using cpms_Application.Request.Material;
using cpms_Application.Request.Project;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.User;
using cpms_Application.Response.Project;
using cpms_Application.Response.UserAccount;
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
            //UserAccount
            //CreateMap<UserProfileResponse, UserAccount>();

            CreateMap<UpdateUserRoleRequest, UserAccount>();
            CreateMap<UpdateUserRequest, UserAccount>();
            CreateMap<UserAccount, UserProfileResponse>();
            CreateMap<UserAccount, AccountResponse>();


            // Project mapping
            CreateMap<CreateProjectRequest, Project>();
            CreateMap<Project, ProjectResponse>();

            CreateMap<CreatePurchaseOrderRequest, PurchaseOrder>();
            CreateMap<OrderLineItemDto, OrderLineItem>();

            CreateMap<CreateMaterialRequest, Material>();

            CreateMap<CreateSupplierRequest, Supplier>();
            
            CreateMap<CreateCatalogRequest, SupplierCatalog>();
        }
    }
}
