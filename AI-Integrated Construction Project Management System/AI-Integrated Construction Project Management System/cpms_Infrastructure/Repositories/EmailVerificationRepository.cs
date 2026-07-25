using cpms_Application.Repository;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Repositories
{
    public class EmailVerificationRepository : GenericRepository<EmailVerification>, IEmailVerificationRepository
    {
        public EmailVerificationRepository(AppDbContext context) : base(context)
        {
        }
    }
}
