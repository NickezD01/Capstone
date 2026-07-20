using cpms_Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IClaimService
    {
        ClaimDTO GetUserClaim();
    }
}
