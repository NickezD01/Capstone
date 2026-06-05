using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.CustomExceptions
{
    public class ConflictExceptions(string message) : Exception(message)
    {
    }
}
