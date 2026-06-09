using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public MaterialService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateMaterialAsync(CreateMaterialRequest request)
        {
            var material = _mapper.Map<Material>(request);
            await _uow.Materials.AddAsync(material);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Material created successfully");
        }

        public async Task<ApiResponse> GetAllMaterialsAsync()
        {
            var data = await _uow.Materials.GetAllAsync(null);
            return new ApiResponse().SetOk(data);
        }
    }
}
