using cpms_Application.Repository;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Repositories
{
    public class TaskMaterialRequirementRepository : GenericRepository<TaskMaterialRequirement>, ITaskMaterialRequirementRepository
    {
        public TaskMaterialRequirementRepository(AppDbContext context) : base(context)
        {
        }
    }
}
